using System.Collections.Concurrent;
using FbTracker.Analysis;
using FbTracker.Configuration;
using FbTracker.Domain;
using FbTracker.Futbin;
using FbTracker.Notifications;
using FbTracker.Services;
using FbTracker.Storage;
using Microsoft.Extensions.Logging.Abstractions;

namespace FbTracker.Tests;

public class PipelineTests
{
    [Fact]
    public async Task Cooldown_is_atomic_and_separates_card_season_and_platform()
    {
        await using var gate = new MemorySignalGate();
        var key = new MarketKey(new(27, 8), Platform.Console);
        var tokens = await Task.WhenAll(Enumerable.Range(0, 50).Select(_ => Task.Run(() => gate.TryAcquireAsync(key, TimeSpan.FromMinutes(20), default))));
        string token = Assert.Single(tokens, t => t is not null)!;
        Assert.NotNull(await gate.TryAcquireAsync(key with { Card = new(27, 99) }, TimeSpan.FromMinutes(20), default));
        Assert.NotNull(await gate.TryAcquireAsync(key with { Card = new(26, 8) }, TimeSpan.FromMinutes(20), default));
        Assert.NotNull(await gate.TryAcquireAsync(key with { Platform = Platform.Pc }, TimeSpan.FromMinutes(20), default));
        await gate.ReleaseAsync(key, "some-other-lease", default);
        Assert.Null(await gate.TryAcquireAsync(key, TimeSpan.FromMinutes(20), default));
        await gate.ReleaseAsync(key, token, default);
        Assert.NotNull(await gate.TryAcquireAsync(key, TimeSpan.FromMinutes(20), default));
    }

    [Fact]
    public async Task Parallel_scan_awaits_every_card_excludes_untradeables_and_caches_catalog()
    {
        var rig = new Rig(true);
        var sessions = new[] { rig.Session(), rig.Session() };
        var first = await rig.Scanner.ScanAsync(sessions, default);
        Assert.Equal(new ScanResult(3, 3, 3, 0, 0), first);
        Assert.Equal(new[] { 101, 102, 103 }, rig.Store.Keys.Select(k => k.Card.FutbinId).Order());
        Assert.Equal(0, rig.Notifier.Attempts);
        Assert.Equal(3, sessions.Sum(s => s.CompletedMarketRequests));
        Assert.All(sessions, s => Assert.Equal(1, s.PeakConcurrency));
        await rig.Scanner.ScanAsync(sessions, default);
        Assert.Equal(1, sessions.Sum(s => s.CatalogRequests));
    }

    [Fact]
    public async Task Failed_notification_releases_lease_for_the_exact_card()
    {
        var rig = new Rig(false);
        rig.Options.CardLimit = 1;
        rig.Notifier.Fail = true;
        var session = rig.Session();
        Assert.Equal(1, (await rig.Scanner.ScanAsync([session], default)).Errors);
        rig.Notifier.Fail = false;
        Assert.Equal(1, (await rig.Scanner.ScanAsync([session], default)).Sent);
        Assert.Equal(0, (await rig.Scanner.ScanAsync([session], default)).Sent);
        Assert.Equal(2, rig.Notifier.Attempts);
    }

    [Fact]
    public async Task Source_block_aborts_the_cycle_before_scheduling_more_cards()
    {
        var rig = new Rig(true);
        var session = rig.Session();
        session.Block = true;
        await Assert.ThrowsAsync<SourceUnavailableException>(() => rig.Scanner.ScanAsync([session], default));
        Assert.Equal(1, session.MarketRequests);
        Assert.Empty(rig.Store.Keys);
    }

    [Fact]
    public async Task Repeated_catalog_page_is_rejected_and_failed_refresh_is_not_cached()
    {
        var options = Samples.Options();
        var cache = new CatalogCache(options, new FutbinParser(options), NullLogger<CatalogCache>.Instance);
        var session = new FakeSession(Samples.Catalog(Samples.Row(1)) + "<a href='/27/players?eUnt=1&amp;page=2'>2</a>");
        await Assert.ThrowsAsync<SourceFormatException>(() => cache.GetAsync(session, default));
        session.CatalogHtml = Samples.Catalog(Samples.Row(2));
        Assert.Equal(2, Assert.Single(await cache.GetAsync(session, default)).Key.FutbinId);
        Assert.Equal(3, session.CatalogRequests);
    }

    [Fact]
    public void Telegram_message_contains_exact_card_and_platform_and_escapes_source_text()
    {
        var card = Samples.Card() with { Name = "<script>&", Version = "TOTW" };
        var signal = new SignalAnalyzer(new()).Evaluate(card, Samples.Snapshot()).Signal!;
        var message = TelegramNotifier.Format(signal);
        Assert.Contains("&lt;script&gt;&amp;", message);
        Assert.Contains("TOTW", message);
        Assert.Contains("8", message);
        Assert.Contains("/27/player/8/", message);
        Assert.DoesNotContain("<script>", message);
    }

    private sealed class Rig
    {
        internal TrackerOptions Options { get; } = new() { Platforms = [Platform.Console], SourceTimeZone = "UTC" };
        internal FakeStore Store { get; } = new();
        internal FakeNotifier Notifier { get; } = new();
        internal Scanner Scanner { get; }
        internal Rig(bool dryRun)
        {
            Options.Notifications.DryRun = dryRun;
            var parser = new FutbinParser(Options);
            Scanner = new(Options, new CatalogCache(Options, parser, NullLogger<CatalogCache>.Instance), parser,
                new SignalAnalyzer(Options.Signals), Store, new MemorySignalGate(), Notifier, NullLogger<Scanner>.Instance);
        }
        internal FakeSession Session() => new(Samples.Catalog(Samples.Row(101), Samples.Row(102), Samples.Row(103), Samples.Row(104, "500K", "table-price-sbc")));
    }
    private sealed class FakeSession(string catalog) : IPageSession
    {
        public string CatalogHtml { get; set; } = catalog;
        public bool Block { get; set; }
        public int CatalogRequests, MarketRequests, CompletedMarketRequests, PeakConcurrency;
        private int _active;
        public async Task<PageDocument> GetAsync(Uri uri, CancellationToken ct)
        {
            int active = Interlocked.Increment(ref _active);
            PeakConcurrency = Math.Max(PeakConcurrency, active);
            try
            {
                await Task.Delay(15, ct);
                if (uri.AbsolutePath.EndsWith("/players"))
                {
                    CatalogRequests++;
                    return new(uri, uri, CatalogHtml, Samples.Now);
                }
                MarketRequests++;
                if (Block) throw new SourceUnavailableException("Blocked");
                CompletedMarketRequests++;
                return new(uri, uri, Samples.ProfitableMarket(Samples.Now), Samples.Now);
            }
            finally { Interlocked.Decrement(ref _active); }
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
    private sealed class FakeStore : IObservationStore
    {
        public ConcurrentBag<MarketKey> Keys { get; } = [];
        public Task AppendAsync(PlayerCard card, MarketSnapshot snapshot, SignalDecision decision, CancellationToken ct)
        { Keys.Add(snapshot.Key); return Task.CompletedTask; }
    }
    private sealed class FakeNotifier : ISignalNotifier
    {
        public bool Fail { get; set; }
        public int Attempts;
        public Task SendAsync(TradingSignal signal, CancellationToken ct)
        {
            Interlocked.Increment(ref Attempts);
            if (Fail) throw new HttpRequestException("Synthetic failure");
            return Task.CompletedTask;
        }
    }
}

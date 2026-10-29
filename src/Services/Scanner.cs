using System.Collections.Concurrent;
using FbTracker.Analysis;
using FbTracker.Configuration;
using FbTracker.Domain;
using FbTracker.Futbin;
using FbTracker.Notifications;
using FbTracker.Storage;

namespace FbTracker.Services;

public sealed record ScanResult(int Cards, int Snapshots, int Candidates, int Sent, int Errors);

public sealed class Scanner(TrackerOptions options, CatalogCache catalog, FutbinParser parser,
    SignalAnalyzer analyzer, IObservationStore store, ISignalGate gate, ISignalNotifier notifier, ILogger<Scanner> logger)
{
    private readonly SemaphoreSlim _scanLock = new(1, 1);
    public async Task<ScanResult> ScanAsync(IReadOnlyList<IPageSession> sessions, CancellationToken ct)
    {
        await _scanLock.WaitAsync(ct);
        try
        {
            var all = await catalog.GetAsync(sessions[0], ct);
            var eligible = all.Where(c => c.Tradability == Tradability.Tradeable);
            if (options.CardLimit > 0) eligible = eligible.Take(options.CardLimit);
            var queue = new ConcurrentQueue<PlayerCard>(eligible);
            int cardCount = queue.Count, snapshots = 0, candidates = 0, sent = 0, errors = 0;
            using var cycle = CancellationTokenSource.CreateLinkedTokenSource(ct);
            async Task Worker(IPageSession session)
            {
                try
                {
                    while (queue.TryDequeue(out var card))
                    {
                        cycle.Token.ThrowIfCancellationRequested();
                        try
                        {
                            var document = await session.GetAsync(new Uri(card.Url.AbsoluteUri.TrimEnd('/') + "/market"), cycle.Token);
                            foreach (var snapshot in parser.ParseMarket(card, document))
                            {
                                var decision = analyzer.Evaluate(card, snapshot);
                                await store.AppendAsync(card, snapshot, decision, cycle.Token);
                                Interlocked.Increment(ref snapshots);
                                if (decision.Signal is not { } signal) continue;
                                Interlocked.Increment(ref candidates);
                                if (options.Notifications.DryRun)
                                {
                                    logger.LogInformation("Dry-run candidate {Key}: profit {Profit}, observed BIN/h {Rate}",
                                        signal.Key, signal.ExpectedNetProfit, signal.Liquidity.ObservedBuyNowPerHour);
                                    continue;
                                }
                                var token = await gate.TryAcquireAsync(signal.Key, TimeSpan.FromMinutes(options.Notifications.CooldownMinutes), cycle.Token);
                                if (token is null) continue;
                                try { await notifier.SendAsync(signal, cycle.Token); Interlocked.Increment(ref sent); }
                                catch
                                {
                                    // Release only our lease, even if cancellation has already been requested.
                                    using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                                    await gate.ReleaseAsync(signal.Key, token, cleanup.Token);
                                    throw;
                                }
                            }
                        }
                        catch (OperationCanceledException) when (cycle.IsCancellationRequested) { throw; }
                        catch (SourceUnavailableException) { throw; }
                        catch (SourceFormatException) { throw; }
                        catch (Exception error)
                        {
                            Interlocked.Increment(ref errors);
                            logger.LogWarning("Card {Key} failed ({ErrorType}): no stale data reused", card.Key, error.GetType().Name);
                        }
                    }
                }
                catch { await cycle.CancelAsync(); throw; }
            }
            await Task.WhenAll(sessions.Select(Worker));
            var result = new ScanResult(cardCount, snapshots, candidates, sent, errors);
            logger.LogInformation("Scan complete: {Result}", result);
            return result;
        }
        finally { _scanLock.Release(); }
    }
}

public sealed class ScanHealth
{
    public DateTimeOffset? LastSuccessfulScan { get; set; }
    public string State { get; set; } = "starting";
}

public sealed class ScannerWorker(TrackerOptions options, Scanner scanner, ScanHealth health,
    ILogger<ScannerWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        SessionPool pool;
        try { pool = await SessionPool.CreateAsync(options, stoppingToken); }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
        catch (Exception error)
        {
            // Driver/provider errors can contain proxy credentials; do not let the host log the original exception.
            throw new InvalidOperationException($"Could not initialize source sessions ({error.GetType().Name}). Check browser installation and proxy configuration.");
        }
        await using var poolLifetime = pool;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                health.State = "scanning";
                var result = await scanner.ScanAsync(pool.Sessions, stoppingToken);
                health.State = result.Errors == 0 ? "ready" : "degraded";
                if (result.Errors == 0) health.LastSuccessfulScan = DateTimeOffset.UtcNow;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error)
            {
                health.State = "source_unavailable";
                string reason = error is SourceFormatException or SourceUnavailableException ? error.Message : error.GetType().Name;
                logger.LogError("Scan failed: {Reason}; retry after the interval/cooldown", reason);
            }
            try { await Task.Delay(TimeSpan.FromSeconds(options.ScanIntervalSeconds), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}

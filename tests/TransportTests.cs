using FbTracker.Configuration;
using FbTracker.Futbin;

namespace FbTracker.Tests;

public class TransportTests
{
    [Theory]
    [InlineData(403)]
    [InlineData(429)]
    public async Task Block_has_no_same_request_retry_and_pauses_the_shared_pool(int status)
    {
        var gate = new RequestGate(1);
        await using var session = new FakeTransport(new() { SessionIntervalMilliseconds = 1, RetryCount = 2 }, gate, status);
        await Assert.ThrowsAsync<SourceUnavailableException>(() => session.GetAsync(new("https://www.futbin.com/27/players?eUnt=1"), default));
        Assert.Equal(1, session.Requests);
        using var cancelled = new CancellationTokenSource(40);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => gate.WaitAsync(cancelled.Token));
    }

    [Fact]
    public async Task Unexpected_host_is_never_fetched()
    {
        await using var session = new FakeTransport(new(), new RequestGate(1), 200);
        await Assert.ThrowsAsync<ArgumentException>(() => session.GetAsync(new("https://example.com/27/players"), default));
        Assert.Equal(0, session.Requests);
    }

    [Fact]
    public async Task Cooldown_can_interrupt_already_queued_waiters()
    {
        var gate = new RequestGate(200);
        await gate.WaitAsync(default);
        using var ct = new CancellationTokenSource(300);
        var waiter = gate.WaitAsync(ct.Token);
        await gate.PauseAsync(TimeSpan.FromSeconds(60), default);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiter);
    }

    [Fact]
    public async Task Challenge_even_with_status_200_is_not_market_data()
    {
        await using var session = new Responses(new HttpPageResponse(200, new("https://www.futbin.com/27/players"),
            "<html>challenge</html>", Mitigation: "challenge", RayId: "a522c12b1d69ccbe-HEL"));
        var error = await Assert.ThrowsAsync<SourceUnavailableException>(() => session.GetAsync(new("https://www.futbin.com/27/players"), default));
        Assert.Contains("Cloudflare challenge", error.Message);
        Assert.Contains("a522c12b1d69ccbe-HEL", error.Message);
    }

    [Fact]
    public async Task Redirects_preserve_requested_identity_and_reject_external_targets_before_fetch()
    {
        var original = new Uri("https://www.futbin.com/27/player/8/old/market");
        var canonical = new Uri("https://www.futbin.com/27/player/8/new/market");
        await using var valid = new Responses(new HttpPageResponse(301, original, "", Location: canonical.AbsoluteUri), new(200, canonical, "market"));
        var page = await valid.GetAsync(original, default);
        Assert.Equal(original, page.RequestedUri);
        Assert.Equal(canonical, page.FinalUri);
        await using var invalid = new Responses(new HttpPageResponse(302, original, "", Location: "https://example.com/"));
        await Assert.ThrowsAsync<SourceUnavailableException>(() => invalid.GetAsync(original, default));
        Assert.Equal(1, invalid.Requests);
    }

    [Fact]
    public void Obsolete_browser_configuration_is_rejected()
    {
        var options = new TrackerOptions { Transport = new() { Mode = "Browser" } };
        Assert.Throws<ArgumentException>(options.Validate);
    }

    private sealed class Responses(params HttpPageResponse[] responses) : PageSession(new() { SessionIntervalMilliseconds = 1 }, new(1))
    {
        public int Requests;
        protected override Task<HttpPageResponse> FetchAsync(Uri uri, CancellationToken ct) => Task.FromResult(responses[Requests++]);
        public override ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeTransport(TransportOptions options, RequestGate gate, int status) : PageSession(options, gate)
    {
        public int Requests;
        protected override Task<HttpPageResponse> FetchAsync(Uri uri, CancellationToken ct)
        { Requests++; return Task.FromResult(new HttpPageResponse(status, uri, "test")); }
        public override ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

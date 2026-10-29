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

    private sealed class FakeTransport(TransportOptions options, RequestGate gate, int status) : PageSession(options, gate)
    {
        public int Requests;
        protected override Task<(int, Uri, string, string?)> FetchAsync(Uri uri, CancellationToken ct)
        { Requests++; return Task.FromResult<(int, Uri, string, string?)>((status, uri, "test", null)); }
        public override ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

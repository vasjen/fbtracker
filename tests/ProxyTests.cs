using System.Net;
using System.Net.Sockets;
using System.Text;
using FbTracker.Configuration;
using FbTracker.Futbin;

namespace FbTracker.Tests;

public class ProxyTests
{
    [Fact]
    public async Task Proxy_file_limits_workers_and_empty_file_never_falls_back_to_direct_access()
    {
        string path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, "[{\"Server\":\"http://127.0.0.1:8081\"},{\"Server\":\"http://127.0.0.1:8082\"}]");
            var settings = new TrackerOptions { MaxConcurrency = 1, Transport = new() { ProxiesFile = path } };
            await using var pool = await SessionPool.CreateAsync(settings, default);
            Assert.Single(pool.Sessions);
            await File.WriteAllTextAsync(path, "[]");
            await Assert.ThrowsAsync<SourceUnavailableException>(() => SessionPool.CreateAsync(settings, default));
            settings.Transport.Proxies = [new() { Server = "http://127.0.0.1:8083" }];
            Assert.Throws<ArgumentException>(settings.Validate);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Curl_sends_proxy_basic_credentials_over_the_proxy_connection_only()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var server = RejectTunnel(listener, received, Task.CompletedTask, timeout.Token);
        await using var session = new CurlPageSession(new()
        { PythonExecutable = Environment.GetEnvironmentVariable("FBTRACKER_TEST_PYTHON") ?? "python3" },
            new RequestGate(1), new() { Server = Address(listener), Username = "test-user", Password = "test-password" });
        var error = await Assert.ThrowsAsync<SourceUnavailableException>(() =>
            session.GetAsync(new("https://www.futbin.com/27/players"), timeout.Token));
        await server;
        string expected = Convert.ToBase64String(Encoding.UTF8.GetBytes("test-user:test-password"));
        Assert.Contains($"Proxy-Authorization: Basic {expected}", await received.Task, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("test-password", error.Message);
    }

    [Theory]
    [InlineData("CurlCffi")]
    [InlineData("Http")]
    public async Task Pool_routes_concurrent_requests_through_distinct_real_proxy_connections(string mode)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using var first = new TcpListener(IPAddress.Loopback, 0);
        using var second = new TcpListener(IPAddress.Loopback, 0);
        first.Start(); second.Start();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var one = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var two = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var servers = Task.WhenAll(RejectTunnel(first, one, release.Task, timeout.Token),
            RejectTunnel(second, two, release.Task, timeout.Token));
        var options = new TrackerOptions
        {
            MaxConcurrency = 4,
            Transport = new()
            {
                Mode = mode, PythonExecutable = Environment.GetEnvironmentVariable("FBTRACKER_TEST_PYTHON") ?? "python3",
                GlobalIntervalMilliseconds = 1, SessionIntervalMilliseconds = 1,
                Proxies = [new() { Server = Address(first) }, new() { Server = Address(second) }]
            }
        };
        await using var pool = await SessionPool.CreateAsync(options, timeout.Token);
        Assert.Equal(2, pool.Sessions.Count); // min(configured workers, proxies)
        var requests = pool.Sessions.Select(s => Record.ExceptionAsync(() =>
            s.GetAsync(new("https://www.futbin.com/27/players?eUnt=1"), timeout.Token))).ToArray();
        try
        {
            // Both CONNECT requests must arrive before either is released: actual parallel network I/O.
            var headers = await Task.WhenAll(one.Task, two.Task).WaitAsync(timeout.Token);
            Assert.All(headers, h => Assert.StartsWith("CONNECT www.futbin.com:443 HTTP/1.1", h));
        }
        finally { release.TrySetResult(); }
        await servers;
        Assert.All(await Task.WhenAll(requests), e => Assert.NotNull(e));
    }

    [Fact]
    public async Task Cancellation_stops_the_curl_worker_while_waiting_for_a_proxy()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
        await using var session = new CurlPageSession(new()
        { PythonExecutable = Environment.GetEnvironmentVariable("FBTRACKER_TEST_PYTHON") ?? "python3" },
            new RequestGate(1), new() { Server = Address(listener) });
        var request = session.GetAsync(new("https://www.futbin.com/27/players"), cancel.Token);
        using var connection = await listener.AcceptTcpClientAsync(timeout.Token);
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request.WaitAsync(timeout.Token));
    }

    private static string Address(TcpListener listener) => $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}";

    private static async Task RejectTunnel(TcpListener listener, TaskCompletionSource<string> received,
        Task release, CancellationToken ct)
    {
        using var client = await listener.AcceptTcpClientAsync(ct);
        await using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
        var headers = new StringBuilder();
        while (await reader.ReadLineAsync(ct) is { Length: > 0 } line) headers.AppendLine(line);
        received.TrySetResult(headers.ToString());
        await release.WaitAsync(ct);
        // No TLS interception, no external network, no disabled certificate validation.
        await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 502 Bad Gateway\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"), ct);
    }
}

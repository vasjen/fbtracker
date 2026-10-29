using System.Diagnostics;
using System.Text;
using System.Text.Json;
using FbTracker.Configuration;

namespace FbTracker.Futbin;

// A persistent libcurl HTTP session, not a browser. Secrets travel through stdin only.
public sealed class CurlPageSession(TransportOptions options, RequestGate global, ProxyOptions? proxy)
    : PageSession(options, global)
{
    private Process? _process;
    private Task? _stderr;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    protected override async Task<HttpPageResponse> FetchAsync(Uri uri, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(Options.RequestTimeoutSeconds + 5));
        try
        {
            if (_process is null || _process.HasExited)
            {
                await StopAsync();
                var start = new ProcessStartInfo(Options.PythonExecutable)
                {
                    UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true,
                    RedirectStandardError = true, CreateNoWindow = true,
                    StandardInputEncoding = new UTF8Encoding(false), StandardOutputEncoding = Encoding.UTF8
                };
                start.ArgumentList.Add("-u");
                start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "Transport", "http_worker.py"));
                _process = Process.Start(start) ?? throw new SourceUnavailableException("Could not start the HTTP transport.");
                // Drain stderr without storing or logging third-party exceptions that might contain credentials.
                _stderr = DrainAsync(_process.StandardError);
                using var ready = await ExchangeAsync(new { timeout = Options.RequestTimeoutSeconds, proxy }, timeout.Token);
                if (!ready.RootElement.TryGetProperty("ready", out var flag) || !flag.GetBoolean())
                    throw new SourceUnavailableException("HTTP transport initialization failed; install Transport/requirements.txt.");
            }
            using var reply = await ExchangeAsync(new { url = uri.AbsoluteUri }, timeout.Token);
            if (reply.RootElement.TryGetProperty("error", out _))
                throw new SourceUnavailableException("libcurl HTTP request failed. Check TLS, network and proxy credentials.");
            return reply.Deserialize<HttpPageResponse>(Json)
                ?? throw new SourceUnavailableException("Invalid HTTP transport response.");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await StopAsync();
            throw;
        }
        catch (OperationCanceledException)
        {
            await StopAsync();
            throw new SourceUnavailableException("HTTP transport timed out.");
        }
        catch (SourceUnavailableException) { await StopAsync(); throw; }
        catch (Exception)
        {
            await StopAsync();
            throw new SourceUnavailableException("HTTP transport failed; check PythonExecutable and Transport/requirements.txt.");
        }
    }

    private async Task<JsonDocument> ExchangeAsync(object message, CancellationToken ct)
    {
        await _process!.StandardInput.WriteLineAsync(JsonSerializer.Serialize(message, Json).AsMemory(), ct);
        await _process.StandardInput.FlushAsync(ct);
        string? line = await _process.StandardOutput.ReadLineAsync(ct);
        return line is null ? throw new SourceUnavailableException("HTTP transport exited; check Python dependencies.")
            : JsonDocument.Parse(line);
    }

    private static async Task DrainAsync(StreamReader reader)
    {
        char[] buffer = new char[1024];
        try { while (await reader.ReadAsync(buffer) > 0) { } }
        catch (IOException) { }
        catch (ObjectDisposedException) { }
    }

    private async ValueTask StopAsync()
    {
        if (_process is null) return;
        try
        {
            if (!_process.HasExited) _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync();
            if (_stderr is not null) await _stderr;
        }
        finally { _process.Dispose(); _process = null; _stderr = null; }
    }

    public override ValueTask DisposeAsync() => StopAsync();
}

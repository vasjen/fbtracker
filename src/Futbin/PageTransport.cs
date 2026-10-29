using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FbTracker.Configuration;
using FbTracker.Domain;
using Microsoft.Playwright;

namespace FbTracker.Futbin;

public interface IPageSession : IAsyncDisposable
{
    Task<PageDocument> GetAsync(Uri uri, CancellationToken cancellationToken);
}

public sealed class SourceUnavailableException(string message) : Exception(message);

public sealed class RequestGate(int intervalMilliseconds)
{
    private readonly object _sync = new();
    private DateTimeOffset _next;
    private DateTimeOffset _blockedUntil;
    public async Task WaitAsync(CancellationToken ct)
    {
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            TimeSpan wait;
            lock (_sync)
            {
                var now = DateTimeOffset.UtcNow;
                var due = _next > _blockedUntil ? _next : _blockedUntil;
                wait = due - now;
                if (wait <= TimeSpan.Zero)
                {
                    _next = now.AddMilliseconds(intervalMilliseconds);
                    return;
                }
            }
            // Recheck after waking: a response may have blocked the pool while we waited.
            await Task.Delay(wait, ct);
        }
    }
    public Task PauseAsync(TimeSpan duration, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_sync) { var until = DateTimeOffset.UtcNow + duration; if (until > _blockedUntil) _blockedUntil = until; }
        return Task.CompletedTask;
    }
}

public abstract class PageSession(TransportOptions options, RequestGate global) : IPageSession
{
    protected TransportOptions Options { get; } = options;
    private readonly SemaphoreSlim _singleRequest = new(1, 1);
    private readonly RequestGate _local = new(options.SessionIntervalMilliseconds);
    protected abstract Task<(int Status, Uri FinalUri, string Html, string? RetryAfter)> FetchAsync(Uri uri, CancellationToken ct);

    public async Task<PageDocument> GetAsync(Uri uri, CancellationToken cancellationToken)
    {
        if (!FutbinParser.IsFutbinUri(uri)) throw new ArgumentException("Only HTTPS FUTBIN requests are allowed.");
        await _singleRequest.WaitAsync(cancellationToken);
        try
        {
            for (int attempt = 0; ; attempt++)
            {
                await _local.WaitAsync(cancellationToken);
                await global.WaitAsync(cancellationToken);
                var response = await FetchAsync(uri, cancellationToken);
                if (response.Status == 200)
                {
                    if (!FutbinParser.IsFutbinUri(response.FinalUri)) throw new SourceUnavailableException("Unexpected redirect.");
                    return new(uri, response.FinalUri, response.Html, DateTimeOffset.UtcNow);
                }
                if (response.Status is 403 or 429)
                {
                    var cooldown = TimeSpan.FromSeconds(Options.BlockCooldownSeconds);
                    if (int.TryParse(response.RetryAfter, out int seconds) && seconds > 0)
                        cooldown = TimeSpan.FromSeconds(Math.Max(seconds, cooldown.TotalSeconds));
                    else if (DateTimeOffset.TryParse(response.RetryAfter, out var date) && date - DateTimeOffset.UtcNow > cooldown)
                        cooldown = date - DateTimeOffset.UtcNow;
                    // A block slows the whole pool; rotating to another proxy is not a retry strategy.
                    await global.PauseAsync(cooldown, cancellationToken);
                    throw new SourceUnavailableException($"FUTBIN returned {response.Status}; pool cooldown applied.");
                }
                if (response.Status < 500 || attempt >= Options.RetryCount)
                    throw new SourceUnavailableException($"FUTBIN returned HTTP {response.Status}.");
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt + 1)), cancellationToken);
            }
        }
        finally { _singleRequest.Release(); }
    }
    public abstract ValueTask DisposeAsync();
}

public sealed class HttpPageSession : PageSession
{
    private readonly HttpClient _http;
    public HttpPageSession(TransportOptions options, RequestGate global, ProxyOptions? proxy) : base(options, global)
    {
        var handler = new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All, AllowAutoRedirect = false };
        if (proxy is not null)
            handler.Proxy = new WebProxy(proxy.Server) { Credentials = proxy.Username is null ? null : new NetworkCredential(proxy.Username, proxy.Password) };
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("FbTracker/27.0");
        _http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-GB,en;q=0.9");
    }
    protected override async Task<(int, Uri, string, string?)> FetchAsync(Uri uri, CancellationToken ct)
    {
        using var response = await _http.GetAsync(uri, ct);
        return ((int)response.StatusCode, uri, await response.Content.ReadAsStringAsync(ct), response.Headers.RetryAfter?.ToString());
    }
    public override ValueTask DisposeAsync() { _http.Dispose(); return ValueTask.CompletedTask; }
}

public sealed class BrowserPageSession(TransportOptions options, RequestGate global, IBrowser browser,
    IBrowserContext context) : PageSession(options, global)
{
    protected override async Task<(int, Uri, string, string?)> FetchAsync(Uri uri, CancellationToken ct)
    {
        var page = await context.NewPageAsync().WaitAsync(ct);
        try
        {
            var response = await page.GotoAsync(uri.AbsoluteUri, new() { WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = Options.RequestTimeoutSeconds * 1000 }).WaitAsync(ct);
            if (response is null) throw new SourceUnavailableException("No browser navigation response.");
            response.Headers.TryGetValue("retry-after", out var retryAfter);
            if (response.Status == 200)
            {
                // Wait for the public page structure; do not wait for perpetual advertising traffic.
                string selector = uri.AbsolutePath.EndsWith("/players") ? ".players-table" : ".price-box-original-player";
                await page.WaitForSelectorAsync(selector, new() { State = WaitForSelectorState.Attached,
                    Timeout = Options.RequestTimeoutSeconds * 1000 }).WaitAsync(ct);
                // Dates are formatted by page scripts. Waiting for Load makes that transformation deterministic.
                await page.WaitForLoadStateAsync(LoadState.Load, new() { Timeout = Options.RequestTimeoutSeconds * 1000 }).WaitAsync(ct);
            }
            return (response.Status, new Uri(page.Url), await page.ContentAsync().WaitAsync(ct), retryAfter);
        }
        catch (TimeoutException)
        {
            throw new SourceUnavailableException("FUTBIN page did not render the expected structure before the timeout.");
        }
        finally { await page.CloseAsync(); }
    }
    public override async ValueTask DisposeAsync() { await context.CloseAsync(); await browser.CloseAsync(); }
}

public sealed class SessionPool : IAsyncDisposable
{
    public IReadOnlyList<IPageSession> Sessions { get; }
    private readonly IPlaywright? _playwright;
    private SessionPool(IReadOnlyList<IPageSession> sessions, IPlaywright? playwright) { Sessions = sessions; _playwright = playwright; }

    public static async Task<SessionPool> CreateAsync(TrackerOptions settings, CancellationToken ct)
    {
        var options = settings.Transport;
        var proxies = await ResolveProxiesAsync(options, ct);
        int count = proxies.Length == 0 ? settings.MaxConcurrency : Math.Min(settings.MaxConcurrency, proxies.Length);
        var sessions = new List<IPageSession>();
        IPlaywright? playwright = null;
        var global = new RequestGate(options.GlobalIntervalMilliseconds);
        try
        {
            if (options.Mode == "Browser") playwright = await Playwright.CreateAsync()
                .WaitAsync(TimeSpan.FromSeconds(options.RequestTimeoutSeconds), ct);
            for (int i = 0; i < count; i++)
            {
                ProxyOptions? proxy = proxies.Length == 0 ? null : proxies[i];
                if (options.Mode == "Http") { sessions.Add(new HttpPageSession(options, global, proxy)); continue; }
                var browser = await playwright!.Chromium.LaunchAsync(new()
                {
                    Headless = options.Headless, Channel = options.BrowserChannel,
                    Timeout = options.RequestTimeoutSeconds * 1000,
                    Proxy = proxy is null ? null : new Microsoft.Playwright.Proxy { Server = proxy.Server, Username = proxy.Username, Password = proxy.Password }
                }).WaitAsync(ct);
                try
                {
                    var context = await browser.NewContextAsync(new() { Locale = "en-GB", TimezoneId = settings.SourceTimeZone }).WaitAsync(ct);
                    await context.RouteAsync("**/*", route => route.Request.ResourceType is "image" or "font" or "media"
                        ? route.AbortAsync() : route.ContinueAsync());
                    sessions.Add(new BrowserPageSession(options, global, browser, context));
                }
                catch { await browser.CloseAsync(); throw; }
            }
            return new(sessions, playwright);
        }
        catch { foreach (var session in sessions) await session.DisposeAsync(); playwright?.Dispose(); throw; }
    }

    private static async Task<ProxyOptions[]> ResolveProxiesAsync(TransportOptions options, CancellationToken ct)
    {
        var result = options.Proxies;
        if (!string.IsNullOrEmpty(options.ProxyProviderUrl))
        {
            // Compatible with the previous provider's POST/list.data response. Never log this URL or credentials.
            if (!Uri.TryCreate(options.ProxyProviderUrl + options.ProxyProviderApiKey, UriKind.Absolute, out var uri) || uri.Scheme != "https")
                throw new ArgumentException("Proxy provider must use HTTPS.");
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds) };
            using var response = await http.PostAsJsonAsync(uri, new { type = "ipv4", page = 1, page_size = 100, sort = 1 }, ct);
            if (!response.IsSuccessStatusCode) throw new SourceUnavailableException("Proxy provider failed.");
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            result = json.RootElement.GetProperty("list").GetProperty("data").EnumerateArray().Select(p => new ProxyOptions
            {
                Server = $"http://{p.GetProperty("ip").GetString()}:{p.GetProperty("http_port").ToString()}",
                Username = p.GetProperty("login").GetString(), Password = p.GetProperty("password").GetString()
            }).ToArray();
            if (result.Length == 0) throw new SourceUnavailableException("Configured provider returned no proxies; direct fallback disabled.");
        }
        foreach (var proxy in result)
            if (!Uri.TryCreate(proxy.Server, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https" or "socks5") ||
                !string.IsNullOrEmpty(uri.UserInfo) || uri.AbsolutePath != "/" || !string.IsNullOrEmpty(uri.Query))
                throw new ArgumentException("Invalid proxy server; put credentials in Username/Password.");
        return result;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var session in Sessions) await session.DisposeAsync();
        _playwright?.Dispose();
    }
}

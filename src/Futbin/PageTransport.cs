using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FbTracker.Configuration;
using FbTracker.Domain;
using System.Text.RegularExpressions;

namespace FbTracker.Futbin;

public interface IPageSession : IAsyncDisposable
{
    Task<PageDocument> GetAsync(Uri uri, CancellationToken cancellationToken);
}

public sealed class SourceUnavailableException(string message) : Exception(message);

public sealed record HttpPageResponse(int Status, Uri FinalUri, string Html, string? RetryAfter = null,
    string? Location = null, string? Mitigation = null, string? RayId = null);

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
    protected abstract Task<HttpPageResponse> FetchAsync(Uri uri, CancellationToken ct);

    public async Task<PageDocument> GetAsync(Uri uri, CancellationToken cancellationToken)
    {
        if (!FutbinParser.IsFutbinUri(uri)) throw new ArgumentException("Only HTTPS FUTBIN requests are allowed.");
        await _singleRequest.WaitAsync(cancellationToken);
        try
        {
            var current = uri;
            int redirects = 0, attempt = 0;
            while (true)
            {
                await _local.WaitAsync(cancellationToken);
                await global.WaitAsync(cancellationToken);
                var response = await FetchAsync(current, cancellationToken);
                if (response.Status is 301 or 302 or 303 or 307 or 308)
                {
                    if (++redirects > 5 || response.Location is null ||
                        !Uri.TryCreate(current, response.Location, out var next) || !FutbinParser.IsFutbinUri(next))
                        throw new SourceUnavailableException("Unsafe or excessive source redirects.");
                    current = next;
                    continue;
                }
                bool challenged = string.Equals(response.Mitigation, "challenge", StringComparison.OrdinalIgnoreCase);
                if (response.Status is 403 or 429 || challenged)
                {
                    var cooldown = TimeSpan.FromSeconds(Options.BlockCooldownSeconds);
                    if (int.TryParse(response.RetryAfter, out int seconds) && seconds > 0)
                        cooldown = TimeSpan.FromSeconds(Math.Max(seconds, cooldown.TotalSeconds));
                    else if (DateTimeOffset.TryParse(response.RetryAfter, out var date) && date - DateTimeOffset.UtcNow > cooldown)
                        cooldown = date - DateTimeOffset.UtcNow;
                    await global.PauseAsync(cooldown, cancellationToken);
                    string ray = response.RayId is { } id && Regex.IsMatch(id, @"\A[a-zA-Z0-9-]{1,80}\z") ? $" Ray ID: {id}." : "";
                    string kind = challenged ? "Cloudflare challenge" : response.Status == 429 ? "rate limit" : "access denied";
                    throw new SourceUnavailableException($"FUTBIN HTTP {response.Status} ({kind}); pool paused for {cooldown.TotalSeconds:F0}s.{ray}");
                }
                if (response.Status == 200)
                {
                    if (!FutbinParser.IsFutbinUri(response.FinalUri)) throw new SourceUnavailableException("Unexpected redirect.");
                    return new(uri, response.FinalUri, response.Html, DateTimeOffset.UtcNow);
                }
                if (response.Status < 500 || attempt++ >= Options.RetryCount)
                    throw new SourceUnavailableException($"FUTBIN returned HTTP {response.Status}.");
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), cancellationToken);
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
        var handler = new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All, AllowAutoRedirect = false,
            UseProxy = proxy is not null, CookieContainer = new CookieContainer() };
        if (proxy is not null)
            handler.Proxy = new WebProxy(proxy.Server) { Credentials = proxy.Username is null ? null : new NetworkCredential(proxy.Username, proxy.Password) };
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds) };
        _http.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml");
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("FbTracker/27.0");
        _http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-GB,en;q=0.9");
    }
    protected override async Task<HttpPageResponse> FetchAsync(Uri uri, CancellationToken ct)
    {
        using var response = await _http.GetAsync(uri, ct);
        return new((int)response.StatusCode, uri, await response.Content.ReadAsStringAsync(ct), response.Headers.RetryAfter?.ToString(),
            response.Headers.Location?.ToString(), Header(response, "cf-mitigated"), Header(response, "cf-ray"));
    }
    private static string? Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) ? values.FirstOrDefault() : null;
    public override ValueTask DisposeAsync() { _http.Dispose(); return ValueTask.CompletedTask; }
}

public sealed class SessionPool : IAsyncDisposable
{
    public IReadOnlyList<IPageSession> Sessions { get; }
    private SessionPool(IReadOnlyList<IPageSession> sessions) { Sessions = sessions; }

    public static async Task<SessionPool> CreateAsync(TrackerOptions settings, CancellationToken ct)
    {
        settings.Validate();
        var options = settings.Transport;
        var proxies = await ResolveProxiesAsync(options, ct);
        int count = proxies.Length == 0 ? settings.MaxConcurrency : Math.Min(settings.MaxConcurrency, proxies.Length);
        var sessions = new List<IPageSession>();
        var global = new RequestGate(options.GlobalIntervalMilliseconds);
        try
        {
            for (int i = 0; i < count; i++)
            {
                ProxyOptions? proxy = proxies.Length == 0 ? null : proxies[i];
                sessions.Add(options.Mode == "Http" ? new HttpPageSession(options, global, proxy)
                    : new CurlPageSession(options, global, proxy));
            }
            return new(sessions);
        }
        catch { foreach (var session in sessions) await session.DisposeAsync(); throw; }
    }

    private static async Task<ProxyOptions[]> ResolveProxiesAsync(TransportOptions options, CancellationToken ct)
    {
        var result = options.Proxies;
        if (!string.IsNullOrWhiteSpace(options.ProxiesFile))
        {
            await using var file = File.OpenRead(options.ProxiesFile);
            result = await JsonSerializer.DeserializeAsync<ProxyOptions[]>(file,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, ct) ?? [];
            if (result.Length == 0) throw new SourceUnavailableException("Proxy file is empty; direct fallback disabled.");
        }
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
            if (proxy is null || !Uri.TryCreate(proxy.Server, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https" or "socks5") ||
                !string.IsNullOrEmpty(uri.UserInfo) || uri.AbsolutePath != "/" || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
                throw new ArgumentException("Invalid proxy server; put credentials in Username/Password.");
        return result;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var session in Sessions) await session.DisposeAsync();
    }
}

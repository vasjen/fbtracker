using FbTracker.Analysis;
using FbTracker.Configuration;
using FbTracker.Futbin;
using FbTracker.Notifications;
using FbTracker.Services;
using FbTracker.Storage;
using StackExchange.Redis;

if (args.Contains("--install-browser") || args.Contains("--install-browser-with-deps"))
{
    Environment.ExitCode = Microsoft.Playwright.Program.Main(args.Contains("--install-browser-with-deps")
        ? ["install", "--with-deps", "chromium"] : ["install", "chromium"]);
    return;
}
bool once = args.Contains("--once");
bool dryRun = args.Contains("--dry-run");
var configurationArgs = args.Where(a => a is not ("--once" or "--dry-run") &&
    !a.StartsWith("--limit=") && !a.StartsWith("--catalog-pages=")).ToArray();
var builder = WebApplication.CreateBuilder(configurationArgs);
// Standard JSON -> user secrets -> environment -> CLI configuration precedence is preserved.
builder.Configuration.AddUserSecrets<TrackerOptions>(optional: true).AddEnvironmentVariables().AddCommandLine(configurationArgs);
var options = builder.Configuration.GetSection("Tracker").Get<TrackerOptions>() ?? new();
if (dryRun) options.Notifications.DryRun = true;
foreach (string arg in args)
{
    if (arg.StartsWith("--limit=")) options.CardLimit = int.Parse(arg[8..]);
    if (arg.StartsWith("--catalog-pages=")) options.MaxCatalogPages = int.Parse(arg[16..]);
}
options.Validate();
ISignalGate gate = options.Notifications.RedisConnection is { Length: > 0 } redis
    ? new RedisSignalGate(await ConnectionMultiplexer.ConnectAsync(redis)) : new MemorySignalGate();
await using var gateLifetime = gate;
builder.Services.AddSingleton(options);
builder.Services.AddSingleton(options.Notifications);
builder.Services.AddSingleton(options.Signals);
builder.Services.AddSingleton(gate);
builder.Services.AddSingleton<FutbinParser>();
builder.Services.AddSingleton<CatalogCache>();
builder.Services.AddSingleton<SignalAnalyzer>();
builder.Services.AddSingleton<IObservationStore>(new ObservationStore(Path.GetFullPath(options.ObservationDirectory, builder.Environment.ContentRootPath)));
builder.Services.AddSingleton<ISignalNotifier, TelegramNotifier>();
builder.Services.AddSingleton<Scanner>();
builder.Services.AddSingleton<ScanHealth>();
if (!once) builder.Services.AddHostedService<ScannerWorker>();
await using var app = builder.Build();
app.MapGet("/health", (ScanHealth state) => Results.Json(state,
    statusCode: (state.State is "ready" or "scanning") && state.LastSuccessfulScan is not null ? 200 : 503));
if (once)
{
    using var stopping = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; stopping.Cancel(); };
    try
    {
        app.Logger.LogInformation("Starting FC {Season} scan ({Mode}, dry-run: {DryRun})", options.Season, options.Transport.Mode, options.Notifications.DryRun);
        await using var pool = await SessionPool.CreateAsync(options, stopping.Token);
        var result = await app.Services.GetRequiredService<Scanner>().ScanAsync(pool.Sessions, stopping.Token);
        Environment.ExitCode = result.Errors > 0 ? 1 : 0;
    }
    catch (OperationCanceledException) when (stopping.IsCancellationRequested) { Environment.ExitCode = 130; }
    catch (Exception error)
    {
        // Do not log exception messages which could contain proxy or Telegram credentials.
        string reason = error is SourceFormatException or SourceUnavailableException ? error.Message : error.GetType().Name;
        app.Logger.LogError("Scan did not complete: {Reason}. Check source access, browser installation and configuration.", reason);
        Environment.ExitCode = 1;
    }
}
else await app.RunAsync();

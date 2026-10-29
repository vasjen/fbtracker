using FbTracker.Domain;
using StackExchange.Redis;

namespace FbTracker.Notifications;

public interface ISignalGate : IAsyncDisposable
{
    Task<string?> TryAcquireAsync(MarketKey key, TimeSpan cooldown, CancellationToken ct);
    Task ReleaseAsync(MarketKey key, string token, CancellationToken ct);
}

public sealed class MemorySignalGate : ISignalGate
{
    private readonly Dictionary<MarketKey, (string Token, DateTimeOffset Expiry)> _entries = [];
    private readonly object _sync = new();
    public Task<string?> TryAcquireAsync(MarketKey key, TimeSpan cooldown, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_sync)
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var expired in _entries.Where(p => p.Value.Expiry <= now).Select(p => p.Key).ToArray()) _entries.Remove(expired);
            if (_entries.ContainsKey(key)) return Task.FromResult<string?>(null);
            string token = Guid.NewGuid().ToString("N");
            _entries[key] = (token, now + cooldown);
            return Task.FromResult<string?>(token);
        }
    }
    public Task ReleaseAsync(MarketKey key, string token, CancellationToken ct)
    {
        lock (_sync) { if (_entries.TryGetValue(key, out var value) && value.Token == token) _entries.Remove(key); }
        return Task.CompletedTask;
    }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public sealed class RedisSignalGate(IConnectionMultiplexer connection) : ISignalGate
{
    private static string Key(MarketKey key) => $"fbtracker:signal:{key}";
    public async Task<string?> TryAcquireAsync(MarketKey key, TimeSpan cooldown, CancellationToken ct)
    {
        string token = Guid.NewGuid().ToString("N");
        return await connection.GetDatabase().StringSetAsync(Key(key), token, cooldown, When.NotExists).WaitAsync(ct) ? token : null;
    }
    public async Task ReleaseAsync(MarketKey key, string token, CancellationToken ct) =>
        _ = await connection.GetDatabase().ScriptEvaluateAsync(
            "if redis.call('get', KEYS[1]) == ARGV[1] then return redis.call('del', KEYS[1]) else return 0 end",
            [Key(key)], [token]).WaitAsync(ct);
    public async ValueTask DisposeAsync() { await connection.CloseAsync(); connection.Dispose(); }
}

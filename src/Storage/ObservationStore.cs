using System.Text.Json;
using System.Text.Json.Serialization;
using FbTracker.Domain;

namespace FbTracker.Storage;

public interface IObservationStore
{
    Task AppendAsync(PlayerCard card, MarketSnapshot snapshot, SignalDecision decision, CancellationToken ct);
}

public sealed class ObservationStore(string directory) : IObservationStore
{
    private readonly SemaphoreSlim _write = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new() { Converters = { new JsonStringEnumConverter() } };
    public async Task AppendAsync(PlayerCard card, MarketSnapshot snapshot, SignalDecision decision, CancellationToken ct)
    {
        var line = JsonSerializer.Serialize(new { SchemaVersion = 1, Card = card, Snapshot = snapshot, Decision = decision }, JsonOptions);
        await _write.WaitAsync(ct);
        try
        {
            Directory.CreateDirectory(directory);
            await File.AppendAllTextAsync(Path.Combine(directory, $"{snapshot.ObservedAt:yyyy-MM-dd}.jsonl"), line + "\n", ct);
        }
        finally { _write.Release(); }
    }
}

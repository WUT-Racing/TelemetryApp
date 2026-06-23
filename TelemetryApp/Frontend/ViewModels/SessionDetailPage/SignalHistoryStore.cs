using TelemetryApp.Backend.Decoding;

namespace TelemetryApp.Frontend.ViewModels;

public sealed class SignalHistoryStore
{
    private const ulong DefaultMaxHistoryMs = 3UL * 60UL * 60UL * 1000UL; // 3 godziny

    private readonly object _sync = new();
    private readonly Dictionary<string, List<SignalSample>> _historyBySignal = new(StringComparer.OrdinalIgnoreCase);
    private readonly ulong _maxHistoryMs;

    public SignalHistoryStore(ulong maxHistoryMs = DefaultMaxHistoryMs)
    {
        _maxHistoryMs = maxHistoryMs;
    }

    public void Clear()
    {
        lock (_sync)
        {
            _historyBySignal.Clear();
        }
    }

    public IReadOnlyList<SignalSample> AppendBatch(DecodedSignalBatch batch)
    {
        if (batch.Signals.Length == 0)
            return Array.Empty<SignalSample>();

        // Jeden blok ma jeden timestamp. Jeżeli w paczce ten sam sygnał pojawi się kilka razy,
        // do historii dashboardu dopisujemy tylko ostatnią wartość z tego bloku.
        var latestInBatch = new Dictionary<string, SignalSample>(StringComparer.OrdinalIgnoreCase);

        foreach (var signal in batch.Signals)
        {
            var sample = new SignalSample(
                signal.CanId,
                signal.Key,
                signal.Name,
                signal.Unit,
                signal.Timestamp,
                signal.BlockSequence,
                signal.Value,
                signal.DisplayValue,
                signal.IsEnumLike);

            latestInBatch[MakeKey(signal.CanId, signal.Key)] = sample;
        }

        lock (_sync)
        {
            foreach (var pair in latestInBatch)
            {
                if (!_historyBySignal.TryGetValue(pair.Key, out var samples))
                {
                    samples = new List<SignalSample>(capacity: 4096);
                    _historyBySignal[pair.Key] = samples;
                }

                samples.Add(pair.Value);
                PruneOldSamples(samples, pair.Value.TimestampMs);
            }
        }

        return latestInBatch.Values.ToArray();
    }

    public IReadOnlyList<SignalSample> GetHistory(ushort canId, string key)
    {
        lock (_sync)
        {
            return _historyBySignal.TryGetValue(MakeKey(canId, key), out var samples)
                ? samples.ToArray()
                : Array.Empty<SignalSample>();
        }
    }

    public SignalSample? GetLatest(ushort canId, string key)
    {
        lock (_sync)
        {
            if (!_historyBySignal.TryGetValue(MakeKey(canId, key), out var samples) || samples.Count == 0)
                return null;

            return samples[^1];
        }
    }

    public IReadOnlyList<SignalSample> GetAllHistory()
    {
        lock (_sync)
        {
            return _historyBySignal.Values
                .SelectMany(samples => samples)
                .OrderBy(sample => sample.TimestampMs)
                .ThenBy(sample => sample.BlockSequence)
                .ThenBy(sample => sample.CanId)
                .ThenBy(sample => sample.Key, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }

    private void PruneOldSamples(List<SignalSample> samples, ulong newestTimestampMs)
    {
        if (samples.Count == 0 || newestTimestampMs < _maxHistoryMs)
            return;

        var minTimestamp = newestTimestampMs - _maxHistoryMs;
        var removeCount = 0;

        while (removeCount < samples.Count && samples[removeCount].TimestampMs < minTimestamp)
            removeCount++;

        if (removeCount > 0)
            samples.RemoveRange(0, removeCount);
    }

    private static string MakeKey(ushort canId, string key) => $"{canId:X3}|{key}";
}

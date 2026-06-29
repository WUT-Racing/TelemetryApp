using TelemetryApp.Backend.Decoding;
using TelemetryApp.Backend.Performance;

namespace TelemetryApp.Frontend.ViewModels;

public sealed class SignalHistoryStore
{
    private readonly object _sync = new();
    private readonly Dictionary<string, List<SignalSample>> _historyBySignal = new(StringComparer.OrdinalIgnoreCase);

    private ulong _maxHistoryMs;
    private ulong _minSampleIntervalMs;
    private int _maxSamplesPerSignal;

    public SignalHistoryStore(TelemetryPerformanceOptions? options = null)
    {
        ApplyOptions(options ?? TelemetryPerformanceOptions.CreateDefault(), pruneExisting: false);
    }

    public void ApplyOptions(TelemetryPerformanceOptions options, bool pruneExisting = true)
    {
        options.Normalize();

        lock (_sync)
        {
            _maxHistoryMs = (ulong)options.DashboardHistoryMinutes * 60UL * 1000UL;
            _minSampleIntervalMs = (ulong)options.MinSampleIntervalMs;
            _maxSamplesPerSignal = Math.Max(500, options.MaxSamplesPerSignal);

            if (!pruneExisting)
                return;

            foreach (var samples in _historyBySignal.Values)
            {
                if (samples.Count == 0)
                    continue;

                PruneOldSamples(samples, samples[^1].TimestampMs);
                PruneByCount(samples);
            }
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            _historyBySignal.Clear();
        }
    }

    public int GetTotalSampleCount()
    {
        lock (_sync)
        {
            return _historyBySignal.Values.Sum(samples => samples.Count);
        }
    }

    public int GetTrackedSignalCount()
    {
        lock (_sync)
        {
            return _historyBySignal.Count;
        }
    }

    public IReadOnlyList<SignalSample> AppendBatch(DecodedSignalBatch batch)
    {
        if (batch.Signals.Length == 0)
            return Array.Empty<SignalSample>();

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

        var acceptedSamples = new List<SignalSample>(latestInBatch.Count);

        lock (_sync)
        {
            foreach (var pair in latestInBatch)
            {
                if (!_historyBySignal.TryGetValue(pair.Key, out var samples))
                {
                    samples = new List<SignalSample>(capacity: 1024);
                    _historyBySignal[pair.Key] = samples;
                }

                if (!AddThrottled(samples, pair.Value))
                    continue;

                PruneOldSamples(samples, pair.Value.TimestampMs);
                PruneByCount(samples);

                acceptedSamples.Add(pair.Value);
            }
        }

        return acceptedSamples;
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

    private bool AddThrottled(List<SignalSample> samples, SignalSample sample)
    {
        if (samples.Count == 0)
        {
            samples.Add(sample);
            return true;
        }

        var last = samples[^1];

        if (sample.TimestampMs <= last.TimestampMs)
        {
            samples[^1] = sample;
            return true;
        }

        var deltaMs = sample.TimestampMs - last.TimestampMs;

        if (deltaMs < _minSampleIntervalMs)
            return false;

        samples.Add(sample);
        return true;
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

    private void PruneByCount(List<SignalSample> samples)
    {
        var overflow = samples.Count - _maxSamplesPerSignal;

        if (overflow <= 0)
            return;

        samples.RemoveRange(0, overflow);
    }

    private static string MakeKey(ushort canId, string key)
    {
        return $"{canId:X3}|{key}";
    }
}
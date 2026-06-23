namespace TelemetryApp.Backend.Decoding;

public readonly record struct DecodedSignal(
    string Key,
    string Name,
    string? Unit,
    double Value,
    long RawValue,
    string? DisplayValue,
    bool IsEnumLike,
    ushort CanId,
    bool Network,
    ulong Timestamp,
    uint BlockSequence);

public sealed class DecodedSignalBatch
{
    public required uint BlockSequence { get; init; }
    public required ulong Timestamp { get; init; }
    public required DecodedSignal[] Signals { get; init; }

    public int Count => Signals.Length;
}

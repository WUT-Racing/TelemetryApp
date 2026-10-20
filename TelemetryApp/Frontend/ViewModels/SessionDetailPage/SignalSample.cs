namespace TelemetryApp.Frontend.ViewModels;

public readonly record struct SignalSample(
    ushort CanId,
    string Key,
    string Name,
    string? Unit,
    ulong TimestampMs,
    uint BlockSequence,
    double Value,
    string? DisplayValue,
    bool IsEnumLike);

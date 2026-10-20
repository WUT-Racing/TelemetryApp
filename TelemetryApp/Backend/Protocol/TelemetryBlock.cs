namespace TelemetryApp.Backend.Protocol;

public sealed class TelemetryBlock
{
    public required byte Version { get; init; }
    public required uint Sequence { get; init; }
    public required ulong Timestamp { get; init; }
    public required CanRecord[] Records { get; init; }
    public required uint Crc32 { get; init; }

    // Dokładne bajty odebrane ze STM, po walidacji. To zapisujemy do raw logu.
    public required byte[] RawBytes { get; init; }

    public int Count => Records.Length;
}

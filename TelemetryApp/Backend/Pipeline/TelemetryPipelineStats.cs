namespace TelemetryApp.Backend.Pipeline;

public sealed class TelemetryPipelineStats
{
    public long BytesReceived { get; init; }
    public long BlocksReceived { get; init; }
    public long RecordsReceived { get; init; }
    public long SignalsDecoded { get; init; }
    public long CrcErrors { get; init; }
    public long ParseErrors { get; init; }
    public long LostBlocks { get; init; }
    public uint? LastSequence { get; init; }
    public ulong? LastTimestamp { get; init; }
    public string? LogFilePath { get; init; }

    public override string ToString()
    {
        return $"Bytes={BytesReceived}, Blocks={BlocksReceived}, Records={RecordsReceived}, Signals={SignalsDecoded}, " +
               $"CRC={CrcErrors}, Parse={ParseErrors}, LostBlocks={LostBlocks}, LastSeq={LastSequence}";
    }
}

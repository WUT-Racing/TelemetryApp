namespace TelemetryApp.Backend.Protocol;

public static class TelemetryProtocolConstants
{
    public const ushort Magic = 0xA55A;

    public const int MagicSize = 2;
    public const int VersionSize = 1;
    public const int SequenceSize = 4;
    public const int TimestampSize = 8;
    public const int CountSize = 2;
    public const int RecordSize = 10;
    public const int Crc32Size = 4;

    public const int HeaderSize =
        MagicSize +
        VersionSize +
        SequenceSize +
        TimestampSize +
        CountSize;

    // Średnio będzie około 211 rekordów na blok 100 ms przy 2105 ramek/s.
    // 1024 daje zapas na bursty, ale chroni parser przed uszkodzonym Count.
    public const int MaxRecordsPerBlock = 1024;

    public const int MaxBlockSize = HeaderSize + MaxRecordsPerBlock * RecordSize + Crc32Size;
}

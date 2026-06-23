namespace TelemetryApp.Backend.Protocol;

public readonly record struct CanRecord(
    ushort CanId,
    bool Network,
    ulong Data)
{
    public const ushort CanIdMask = 0x07FF;
    public const int NetworkBit = 11;

    public static CanRecord FromPacked(ushort header, ulong data)
    {
        var canId = (ushort)(header & CanIdMask);
        var network = ((header >> NetworkBit) & 0x01) != 0;
        return new CanRecord(canId, network, data);
    }

    public ushort ToPackedHeader()
    {
        return (ushort)((CanId & CanIdMask) | (Network ? 1 << NetworkBit : 0));
    }
}

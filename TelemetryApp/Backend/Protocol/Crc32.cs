namespace TelemetryApp.Backend.Protocol;

public static class Crc32
{
    private const uint Polynomial = 0xEDB88320u;
    private static readonly uint[] Table = CreateTable();

    public static uint Compute(ReadOnlySpan<byte> data)
    {
        uint crc = 0xFFFFFFFFu;

        foreach (var b in data)
        {
            var index = (crc ^ b) & 0xFF;
            crc = (crc >> 8) ^ Table[index];
        }

        return ~crc;
    }

    private static uint[] CreateTable()
    {
        var table = new uint[256];

        for (uint i = 0; i < table.Length; i++)
        {
            uint crc = i;

            for (int bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0
                    ? (crc >> 1) ^ Polynomial
                    : crc >> 1;
            }

            table[i] = crc;
        }

        return table;
    }
}

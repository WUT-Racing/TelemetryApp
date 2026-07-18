using TelemetryApp.Backend.Protocol;
using Xunit;

namespace TelemetryApp.Backend.Tests;

public class CanRecordTests
{
    [Fact]
    public void FromPacked_ValidHeader_ExtractsIdAndNetworkCorrectly()
    {
        // ID: 0x123 (0001 0010 0011)
        // Bit Network (11 bit): 1
        // Header: (1 << 11) | 0x123 = 0x800 | 0x123 = 0x0923
        ushort header = 0x0923;
        ulong data = 0xDEADBEEFCAFEBABE;

        var record = CanRecord.FromPacked(header, data);

        Assert.Equal(0x0123, record.CanId);
        Assert.True(record.Network);
        Assert.Equal(data, record.Data);
    }

    [Fact]
    public void FromPacked_HeaderWithoutNetworkBit_ExtractsCorrectly()
    {
        ushort header = 0x0555; // 0x0555 < 0x07FF, bit 11 to 0.
        ulong data = 0x1234567890;

        var record = CanRecord.FromPacked(header, data);

        Assert.Equal(0x0555, record.CanId);
        Assert.False(record.Network);
        Assert.Equal(data, record.Data);
    }

    [Fact]
    public void ToPackedHeader_NetworkTrue_Sets11thBit()
    {
        var record = new CanRecord(CanId: 0x0123, Network: true, Data: 0);

        var header = record.ToPackedHeader();

        Assert.Equal(0x0923, header); // 0x0123 | 0x0800
    }

    [Fact]
    public void ToPackedHeader_IgnoresExcessBitsInCanId()
    {
        // ID wychodzące za maskę
        var record = new CanRecord(CanId: 0xFFFF, Network: false, Data: 0);

        var header = record.ToPackedHeader();

        // Maska 0x07FF ucina wyższe bity
        Assert.Equal(0x07FF, header); 
    }
}
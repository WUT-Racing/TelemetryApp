using System.Text;
using TelemetryApp.Backend.Protocol;
using Xunit;

namespace TelemetryApp.Backend.Tests;

public class Crc32Tests
{
    [Fact]
    public void Compute_StandardTestVector_ReturnsCorrectCrc()
    {
        var data = Encoding.ASCII.GetBytes("123456789");
        var crc = Crc32.Compute(data);
        Assert.Equal(0xCBF43926u, crc);
    }

    [Fact]
    public void Compute_EmptyArray_ReturnsZeroCrc()
    {
        var data = Array.Empty<byte>();
        var crc = Crc32.Compute(data);
        Assert.Equal(0x00000000u, crc);
    }

    [Fact]
    public void Compute_SameData_ReturnsSameCrc()
    {
        var data = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0x01, 0x02, 0x03 };
        var crc1 = Crc32.Compute(data);
        var crc2 = Crc32.Compute(data);
        Assert.Equal(crc1, crc2);
    }
}
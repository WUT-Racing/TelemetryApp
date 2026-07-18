using System.Buffers.Binary;
using System.Text;
using TelemetryApp.Backend.Protocol;
using TelemetryApp.Backend.Storage;
using Xunit;

namespace TelemetryApp.Backend.Tests;

public class RawTelemetryLogTests : IDisposable
{
    private readonly string _tempDirectory;

    public RawTelemetryLogTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "TelemetryApp_StorageTests_", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            try { Directory.Delete(_tempDirectory, true); }
            catch { /* Ignorowanie zablokowanych plików */ }
        }
    }

    private string GetTempFilePath() => Path.Combine(_tempDirectory, $"{Guid.NewGuid():N}.rfdlog");

    private static TelemetryBlock CreateValidTelemetryBlock()
    {
        var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        ushort count = 1;
        byte version = 1;
        uint sequence = 42;
        ulong timestamp = 1000;
        
        writer.Write(TelemetryProtocolConstants.Magic);
        writer.Write(version);
        writer.Write(sequence);
        writer.Write(timestamp);
        writer.Write(count);

        writer.Write((ushort)0x0123); // CanId i Network
        writer.Write((ulong)0xDEADBEEF); // Data

        var payload = ms.ToArray();
        var crc = Crc32.Compute(payload);
        writer.Write(crc);

        var rawBytes = ms.ToArray();

        return new TelemetryBlock
        {
            Version = version,
            Sequence = sequence,
            Timestamp = timestamp,
            Crc32 = crc,
            RawBytes = rawBytes,
            Records = new[] { new CanRecord(0x0123, false, 0xDEADBEEF) }
        };
    }

    [Fact]
    public async Task Writer_CreatesFileAndWritesCorrectHeader()
    {
        var filePath = GetTempFilePath();

        await using (var writer = new RawTelemetryLogWriter(filePath))
        {
            // Konstruktor zapisuje nagłówek
        }

        Assert.True(File.Exists(filePath));
        var bytes = await File.ReadAllBytesAsync(filePath);
        
        // Magiczne słowo "RFDLOG01" 8 bajtów
        var magic = Encoding.ASCII.GetString(bytes.AsSpan(0, 8));
        Assert.Equal("RFDLOG01", magic);
    }

    [Fact]
    public async Task ReadWrite_ValidBlock_PersistsAndRestoresCorrectly()
    {
        var filePath = GetTempFilePath();
        var originalBlock = CreateValidTelemetryBlock();

        await using (var writer = new RawTelemetryLogWriter(filePath))
        {
            await writer.WriteBlockAsync(originalBlock);
            await writer.FlushAsync();
        }

        TelemetryBlock? readBlock;
        await using (var reader = await RawTelemetryLogReader.OpenAsync(filePath))
        {
            readBlock = await reader.ReadNextBlockAsync();
        }

        Assert.NotNull(readBlock);
        Assert.Equal(originalBlock.Sequence, readBlock.Sequence);
        Assert.Equal(originalBlock.Timestamp, readBlock.Timestamp);
        Assert.Equal(originalBlock.Count, readBlock.Count);
        Assert.Equal(originalBlock.RawBytes, readBlock.RawBytes);
    }

    [Fact]
    public async Task ReadAllBlocksAsync_ReturnsAllWrittenBlocks()
    {
        var filePath = GetTempFilePath();
        var block1 = CreateValidTelemetryBlock();
        var block2 = CreateValidTelemetryBlock();

        await using (var writer = new RawTelemetryLogWriter(filePath))
        {
            await writer.WriteBlockAsync(block1);
            await writer.WriteBlockAsync(block2);
        }

        var readBlocks = new List<TelemetryBlock>();
        await using var reader = await RawTelemetryLogReader.OpenAsync(filePath);
        
        await foreach (var block in reader.ReadAllBlocksAsync())
        {
            readBlocks.Add(block);
        }

        Assert.Equal(2, readBlocks.Count);
    }

    [Fact]
    public async Task OpenAsync_InvalidMagicString_ThrowsInvalidDataException()
    {
        var filePath = GetTempFilePath();
        
        // Plik nie w formacie RFDLOG01
        await File.WriteAllBytesAsync(filePath, Encoding.ASCII.GetBytes("BADDATA_1234567890"));

        var ex = await Assert.ThrowsAsync<InvalidDataException>(() => RawTelemetryLogReader.OpenAsync(filePath));
        Assert.Contains("To nie jest poprawny plik", ex.Message);
    }

    [Fact]
    public async Task ReadNextBlockAsync_CorruptedLength_ThrowsInvalidDataException()
    {
        var filePath = GetTempFilePath();
        await using (var writer = new RawTelemetryLogWriter(filePath))
        {
            // Konstruktor zapisze poprawny nagłówek
        }
        
        // Dopisujemy zły rozmiar bloku
        await using (var fs = new FileStream(filePath, FileMode.Append))
        {
            var badLength = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(badLength, -1);
            await fs.WriteAsync(badLength);
        }

        await using var reader = await RawTelemetryLogReader.OpenAsync(filePath);

        var ex = await Assert.ThrowsAsync<InvalidDataException>(async () => await reader.ReadNextBlockAsync());
        Assert.Contains("Niepoprawna długość bloku", ex.Message);
    }
}
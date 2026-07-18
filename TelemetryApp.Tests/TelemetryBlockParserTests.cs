using System.Buffers.Binary;
using TelemetryApp.Backend.Protocol;
using Xunit;

namespace TelemetryApp.Backend.Tests;

public class TelemetryBlockParserTests
{
    // Generator pakietów
    private static byte[] BuildValidRawBlock(ushort count, out uint expectedCrc, byte version = 1, uint sequence = 100, ulong timestamp = 1000)
    {
        var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        writer.Write(TelemetryProtocolConstants.Magic); // 2 bytes
        writer.Write(version); // 1 byte
        writer.Write(sequence); // 4 bytes
        writer.Write(timestamp); // 8 bytes
        writer.Write(count); // 2 bytes

        // Zgodność z RecordSize (10 bytes)
        for (int i = 0; i < count; i++)
        {
            writer.Write((ushort)0x0123); // Header
            writer.Write((ulong)0xDEADBEEF); // Data
        }

        var payloadBytes = ms.ToArray();
        expectedCrc = Crc32.Compute(payloadBytes);
        
        writer.Write(expectedCrc);

        return ms.ToArray();
    }

    [Fact]
    public void Push_ValidCompleteBlock_ReturnsValidResult()
    {
        var parser = new TelemetryBlockParser();
        var rawBlock = BuildValidRawBlock(count: 2, out var expectedCrc);

        var results = parser.Push(rawBlock);

        Assert.Single(results);
        var result = results[0];
        
        Assert.True(result.IsValid);
        Assert.Null(result.Error);
        Assert.NotNull(result.Block);
        
        Assert.Equal(1, result.Block.Version);
        Assert.Equal(100u, result.Block.Sequence);
        Assert.Equal(2, result.Block.Count);
        Assert.Equal(expectedCrc, result.Block.Crc32);
        Assert.Equal(rawBlock, result.Block.RawBytes);
    }

    [Fact]
    public void Push_ChunkedBlock_ReturnsValidResultOnlyOnLastChunk()
    {
        var parser = new TelemetryBlockParser();
        var rawBlock = BuildValidRawBlock(count: 3, out _);
        
        // Pakiet na 2 chunki
        var chunk1 = rawBlock.AsSpan(0, 15).ToArray();
        var chunk2 = rawBlock.AsSpan(15).ToArray();

        var results1 = parser.Push(chunk1);
        Assert.Empty(results1); // Parser dostaje 15 bajtów i oczekuje na resztę

        var results2 = parser.Push(chunk2);
        Assert.Single(results2); // Reszta, pakiet odczytany
        Assert.True(results2[0].IsValid);
    }

    [Fact]
    public void Push_GarbageBeforeMagic_SynchronizesAndReturnsValidBlock()
    {
        var parser = new TelemetryBlockParser();
        var rawBlock = BuildValidRawBlock(count: 1, out _);
        
        // Symulacja szumu
        var garbage = new byte[] { 0xFF, 0x00, 0x12, 0x34 };
        var streamBytes = garbage.Concat(rawBlock).ToArray();

        var results = parser.Push(streamBytes);

        Assert.Single(results); // Odcięcie szumu, synchronizacja do do 0xA55A, dekodowanie pakietu
        Assert.True(results[0].IsValid);
    }

    [Fact]
    public void Push_InvalidCrc_ReturnsErrorResultAndConsumesBlock()
    {
        var parser = new TelemetryBlockParser();
        var rawBlock = BuildValidRawBlock(count: 2, out _);
        
        // Zmiana ostatniego bajtu CRC na zły
        rawBlock[^1] ^= 0xFF; 

        var results = parser.Push(rawBlock);

        Assert.Single(results);
        var result = results[0];
        
        Assert.False(result.IsValid);
        Assert.NotNull(result.Error);
        Assert.Equal("CRC_MISMATCH", result.Error.Code);
        Assert.Contains("CRC nie pasuje", result.Error.Message);
    }

    [Fact]
    public void Push_CountExceedsLimit_ReturnsErrorResultAndSkipsByte()
    {
        var parser = new TelemetryBlockParser();
        
        // Duży count do sprawdzenia ochrony RAM
        ushort hugeCount = 5000;
        var rawBlock = BuildValidRawBlock(count: hugeCount, out _);

        var results = parser.Push(rawBlock);

        Assert.Single(results);
        var result = results[0];

        Assert.False(result.IsValid);
        Assert.NotNull(result.Error);
        Assert.Equal("COUNT_TOO_LARGE", result.Error.Code);
        Assert.Contains("przekracza limit", result.Error.Message);
    }

    [Fact]
    public void Push_MultipleBlocksInSinglePush_ParsesAll()
    {
        var parser = new TelemetryBlockParser();
        var block1 = BuildValidRawBlock(count: 1, out _);
        var block2 = BuildValidRawBlock(count: 2, out _);
        
        // Dwa pakiety w jeden odczyt ze strumienia
        var combined = block1.Concat(block2).ToArray();

        var results = parser.Push(combined);

        Assert.Equal(2, results.Count);
        Assert.True(results[0].IsValid);
        Assert.Equal(1, results[0].Block!.Count);
        
        Assert.True(results[1].IsValid);
        Assert.Equal(2, results[1].Block!.Count);
    }
}
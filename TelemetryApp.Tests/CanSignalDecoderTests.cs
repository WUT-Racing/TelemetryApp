using Xunit;
using TelemetryApp.Backend.Decoding;
using TelemetryApp.Backend.Protocol;

namespace TelemetryApp.Backend.Tests;

public class CanSignalDecoderTests
{
    private static CanDefinitionSet CreateDefinitionSet(CanFrameDefinition frame)
    {
        var file = new CanDefinitionFile
        {
            Frames = new List<CanFrameDefinition> { frame }
        };
        return new CanDefinitionSet(file);
    }

    private static TelemetryBlock CreateTelemetryBlock(ushort canId, ulong data)
    {
        return new TelemetryBlock
        {
            Version = 1,
            Sequence = 1,
            Timestamp = 1000,
            Crc32 = 0,
            RawBytes = [],
            Records = new[] 
            {
                new CanRecord 
                {
                    CanId = canId,
                    Data = data,
                    Network = false
                }
            }
        };
    }

    [Fact]
    public void Decode_SimpleUnsigned_FactorAndOffsetApplied()
    {
        var frame = new CanFrameDefinition
        {
            Id = 0x100,
            Endianness = "little",
            Signals = new List<CanSignalDefinition>
            {
                new CanSignalDefinition
                {
                    Key = "RPM",
                    Name = "Silnik RPM",
                    ByteStart = 0,
                    BitStart = 0,
                    BitLength = 16,
                    DataType = "unsigned",
                    Factor = 0.5,
                    Offset = 100
                }
            }
        };

        var decoder = new CanSignalDecoder(CreateDefinitionSet(frame));
        
        // Little Endian: 0x10, 0x00, 0x00...  (Wartość 16)
        // Raw(16) * 0.5 + 100 = 108
        var block = CreateTelemetryBlock(0x100, 0x0000000000000010); 

        var result = decoder.Decode(block);

        Assert.Single(result.Signals);
        var signal = result.Signals[0];
        Assert.Equal("RPM", signal.Key);
        Assert.Equal(16, signal.RawValue);
        Assert.Equal(108.0, signal.Value);
    }

    [Fact]
    public void Decode_SignedValue_CorrectlyExtendsSignBit()
    {
        var frame = new CanFrameDefinition
        {
            Id = 0x200,
            Endianness = "little",
            Signals = new List<CanSignalDefinition>
            {
                new CanSignalDefinition
                {
                    Key = "Temp",
                    ByteStart = 2,
                    BitStart = 0,
                    BitLength = 8,
                    DataType = "signed",
                    Factor = 1.0,
                    Offset = 0
                }
            }
        };

        var decoder = new CanSignalDecoder(CreateDefinitionSet(frame));
        
        // 0x00, 0x00, 0xFF, 0x00... (0xFF -1 dla signed 8 bit)
        ulong rawData = 0xFF0000;
        var block = CreateTelemetryBlock(0x200, rawData); 

        var result = decoder.Decode(block);

        var signal = result.Signals[0];
        Assert.Equal(-1, signal.RawValue); // Maskowanie traktuje 0xFF jako ujemne
        Assert.Equal(-1.0, signal.Value);
    }

    [Fact]
    public void Decode_EnumSignal_ResolvesLabelSuccessfully()
    {
        var frame = new CanFrameDefinition
        {
            Id = 0x300,
            Endianness = "little",
            Signals = new List<CanSignalDefinition>
            {
                new CanSignalDefinition
                {
                    Key = "State",
                    ByteStart = 0,
                    BitStart = 0,
                    BitLength = 8,
                    DataType = "enum",
                    Enum = new Dictionary<string, CanEnumItemDefinition>
                    {
                        { "0", new CanEnumItemDefinition { IsSimple = true, StringValue = "Off" } },
                        { "1", new CanEnumItemDefinition { IsSimple = true, StringValue = "On" } },
                        { "2", new CanEnumItemDefinition { IsSimple = false, Label = "Error" } }
                    }
                }
            }
        };

        var decoder = new CanSignalDecoder(CreateDefinitionSet(frame));
        var block = CreateTelemetryBlock(0x300, 0x02);

        var result = decoder.Decode(block);

        var signal = result.Signals[0];
        Assert.True(signal.IsEnumLike);
        Assert.Equal(2, signal.RawValue);
        Assert.Equal("Error", signal.DisplayValue);
    }

    [Fact]
    public void Decode_BitfieldSignal_FormatsMultipleActiveBits()
    {
        var frame = new CanFrameDefinition
        {
            Id = 0x400,
            Endianness = "little",
            Signals = new List<CanSignalDefinition>
            {
                new CanSignalDefinition
                {
                    Key = "Errors",
                    ByteStart = 0,
                    BitStart = 0,
                    BitLength = 8,
                    DataType = "bitfield",
                    Enum = new Dictionary<string, CanEnumItemDefinition>
                    {
                        { "0", new CanEnumItemDefinition { IsSimple = true, StringValue = "Overheat" } },
                        { "1", new CanEnumItemDefinition { IsSimple = true, StringValue = "LowOil" } },
                        { "2", new CanEnumItemDefinition { IsSimple = true, StringValue = "FuelLeak" } }
                    }
                }
            }
        };

        var decoder = new CanSignalDecoder(CreateDefinitionSet(frame));
        
        // 101b = 5 dziesiętnie
        var block = CreateTelemetryBlock(0x400, 0x05); 

        var result = decoder.Decode(block);

        var signal = result.Signals[0];
        Assert.Equal("Overheat, FuelLeak", signal.DisplayValue); 
    }

    [Fact]
    public void Decode_SignalOutOfBounds_ThrowsInvalidOperationException()
    {
        var frame = new CanFrameDefinition
        {
            Id = 0x500,
            Endianness = "little",
            Signals = new List<CanSignalDefinition>
            {
                new CanSignalDefinition
                {
                    Key = "InvalidSignal",
                    ByteStart = 8, // Poza zakresem
                    BitStart = 0,
                    BitLength = 8,
                }
            }
        };

        var decoder = new CanSignalDecoder(CreateDefinitionSet(frame));
        var block = CreateTelemetryBlock(0x500, 0x00); 

        // Zepsuty bit mapping
        var ex = Assert.Throws<InvalidOperationException>(() => decoder.Decode(block));
        Assert.Contains("ma niepoprawny byte_start", ex.Message);
    }
}
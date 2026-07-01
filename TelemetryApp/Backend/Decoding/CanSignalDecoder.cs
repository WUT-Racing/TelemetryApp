using System.Buffers.Binary;
using TelemetryApp.Backend.Protocol;

namespace TelemetryApp.Backend.Decoding;

public sealed class CanSignalDecoder
{
    private readonly CanDefinitionSet _definitions;

    public CanSignalDecoder(CanDefinitionSet definitions)
    {
        _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
    }

    public DecodedSignalBatch Decode(TelemetryBlock block)
    {
        var signals = new List<DecodedSignal>(block.Records.Length * 4);

        foreach (var record in block.Records)
        {
            if (!_definitions.TryGetFrame(record.CanId, out var frame))
                continue;

            Span<byte> dataBytes = stackalloc byte[8];
            BinaryPrimitives.WriteUInt64LittleEndian(dataBytes, record.Data);

            foreach (var signalDefinition in frame.Signals)
            {
                if (signalDefinition.Reserved || string.Equals(signalDefinition.DataType, "reserved", StringComparison.OrdinalIgnoreCase))
                    continue;

                var endianness = signalDefinition.Endianness ?? frame.Endianness ?? "little";
                var rawUnsigned = ExtractRawValue(dataBytes, signalDefinition, endianness);
                var rawSigned = ToSigned(rawUnsigned, signalDefinition.BitLength, signalDefinition.DataType);
                var physical = rawSigned * signalDefinition.Factor + signalDefinition.Offset;
                var isEnumLike = IsEnumLike(signalDefinition);
                var displayValue = isEnumLike ? FormatEnumValue(signalDefinition, rawUnsigned, rawSigned) : null;

                signals.Add(new DecodedSignal(
                    signalDefinition.Key,
                    signalDefinition.Name,
                    signalDefinition.Unit,
                    physical,
                    rawSigned,
                    displayValue,
                    isEnumLike,
                    record.CanId,
                    record.Network,
                    block.Timestamp,
                    block.Sequence));
            }
        }

        return new DecodedSignalBatch
        {
            BlockSequence = block.Sequence,
            Timestamp = block.Timestamp,
            Signals = signals.ToArray()
        };
    }


    private static bool IsEnumLike(CanSignalDefinition signal)
    {
        if (string.Equals(signal.DataType, "enum", StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.Equals(signal.DataType, "bitfield", StringComparison.OrdinalIgnoreCase))
            return true;

        return signal.Enum is { Count: > 0 };
    }

    private static string FormatEnumValue(CanSignalDefinition signal, ulong rawUnsigned, long rawSigned)
    {
        if (signal.Enum is null || signal.Enum.Count == 0)
            return rawSigned.ToString();

        if (string.Equals(signal.DataType, "bitfield", StringComparison.OrdinalIgnoreCase))
        {
            var activeLabels = new List<string>();

            foreach (var pair in signal.Enum)
            {
                if (!int.TryParse(pair.Key, out var bitIndex) || bitIndex < 0 || bitIndex >= 64)
                    continue;

                if ((rawUnsigned & (1UL << bitIndex)) == 0)
                    continue;

                activeLabels.Add(ReadEnumLabel(pair.Value) ?? pair.Key);
            }

            return activeLabels.Count == 0 ? "brak aktywnych" : string.Join(", ", activeLabels);
        }

        var key = rawUnsigned.ToString();

        if (signal.Enum.TryGetValue(key, out var value))
            return ReadEnumLabel(value) ?? key;

        return $"nieznane ({rawSigned})";
    }

    // Zamiast JsonElement używamy dedykowanego modelu
    private static string? ReadEnumLabel(CanEnumItemDefinition value)
    {
        if (value.IsSimple)
            return value.StringValue;

        return value.Label ?? value.Key ?? value.StringValue;
    }

    private static ulong ExtractRawValue(ReadOnlySpan<byte> data, CanSignalDefinition signal, string endianness)
    {
        ValidateSignalRange(signal);

        if (string.Equals(endianness, "big", StringComparison.OrdinalIgnoreCase))
            return ExtractBigEndian(data, signal);

        return ExtractLittleEndian(data, signal);
    }

    private static ulong ExtractLittleEndian(ReadOnlySpan<byte> data, CanSignalDefinition signal)
    {
        var payload = BinaryPrimitives.ReadUInt64LittleEndian(data);
        var startBit = signal.ByteStart * 8 + signal.BitStart;
        return (payload >> startBit) & CreateMask(signal.BitLength);
    }

    private static ulong ExtractBigEndian(ReadOnlySpan<byte> data, CanSignalDefinition signal)
    {
        // W obecnym JSON-ie ramki big-endian są bajtowo wyrównane: bit_start=0, bit_length=16.
        // Ta ścieżka obsługuje takie pola bez ryzyka pomylenia kolejności bajtów.
        if (signal.BitStart == 0 && signal.BitLength % 8 == 0)
        {
            var byteLength = signal.BitLength / 8;
            ulong raw = 0;

            for (int i = 0; i < byteLength; i++)
            {
                raw = (raw << 8) | data[signal.ByteStart + i];
            }

            return raw & CreateMask(signal.BitLength);
        }

        // Fallback dla rzadkich pól bitowych big-endian. Jeżeli dojdą takie sygnały,
        // warto potwierdzić ich definicję na przykładach z realnego CAN-a.
        ulong value = 0;
        for (int i = 0; i < signal.BitLength; i++)
        {
            var absoluteBit = signal.ByteStart * 8 + signal.BitStart + i;
            var byteIndex = absoluteBit / 8;
            var bitInByte = 7 - (absoluteBit % 8);
            var bit = (data[byteIndex] >> bitInByte) & 1;
            value = (value << 1) | (uint)bit;
        }

        return value;
    }

    private static long ToSigned(ulong raw, int bitLength, string dataType)
    {
        var isSigned = string.Equals(dataType, "signed", StringComparison.OrdinalIgnoreCase);

        if (!isSigned)
            return unchecked((long)raw);

        if (bitLength <= 0 || bitLength >= 64)
            return unchecked((long)raw);

        var signBit = 1UL << (bitLength - 1);

        if ((raw & signBit) == 0)
            return unchecked((long)raw);

        var extensionMask = ~CreateMask(bitLength);
        return unchecked((long)(raw | extensionMask));
    }

    private static ulong CreateMask(int bitLength)
    {
        if (bitLength <= 0)
            return 0;

        if (bitLength >= 64)
            return ulong.MaxValue;

        return (1UL << bitLength) - 1UL;
    }

    private static void ValidateSignalRange(CanSignalDefinition signal)
    {
        if (signal.ByteStart < 0 || signal.ByteStart >= 8)
            throw new InvalidOperationException($"Sygnał {signal.Key} ma niepoprawny byte_start={signal.ByteStart}.");

        if (signal.BitStart < 0 || signal.BitStart > 7)
            throw new InvalidOperationException($"Sygnał {signal.Key} ma niepoprawny bit_start={signal.BitStart}.");

        if (signal.BitLength <= 0 || signal.BitLength > 64)
            throw new InvalidOperationException($"Sygnał {signal.Key} ma niepoprawny bit_length={signal.BitLength}.");

        var endBitExclusive = signal.ByteStart * 8 + signal.BitStart + signal.BitLength;
        if (endBitExclusive > 64)
            throw new InvalidOperationException($"Sygnał {signal.Key} wychodzi poza 8 bajtów danych CAN.");
    }
}

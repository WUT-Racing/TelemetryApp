using System.Buffers.Binary;

namespace TelemetryApp.Backend.Protocol;

public sealed class TelemetryBlockParser
{
    private readonly List<byte> _buffer = new(capacity: TelemetryProtocolConstants.MaxBlockSize * 2);

    public List<TelemetryParseResult> Push(ReadOnlySpan<byte> bytes)
    {
        for (int i = 0; i < bytes.Length; i++)
            _buffer.Add(bytes[i]);

        var results = new List<TelemetryParseResult>();

        while (true)
        {
            var magicIndex = IndexOfMagic();

            if (magicIndex < 0)
            {
                // Zostawiamy ostatni bajt, bo może być pierwszym bajtem MAGIC.
                if (_buffer.Count > TelemetryProtocolConstants.MagicSize - 1)
                {
                    _buffer.RemoveRange(0, _buffer.Count - (TelemetryProtocolConstants.MagicSize - 1));
                }

                return results;
            }

            if (magicIndex > 0)
            {
                _buffer.RemoveRange(0, magicIndex);
            }

            if (_buffer.Count < TelemetryProtocolConstants.HeaderSize)
                return results;

            var count = ReadUInt16LittleEndian(TelemetryProtocolConstants.MagicSize +
                                               TelemetryProtocolConstants.VersionSize +
                                               TelemetryProtocolConstants.SequenceSize +
                                               TelemetryProtocolConstants.TimestampSize);

            if (count > TelemetryProtocolConstants.MaxRecordsPerBlock)
            {
                _buffer.RemoveAt(0);
                results.Add(TelemetryParseResult.Invalid(
                    "COUNT_TOO_LARGE",
                    $"Count={count} przekracza limit {TelemetryProtocolConstants.MaxRecordsPerBlock}."));
                continue;
            }

            var blockLength = TelemetryProtocolConstants.HeaderSize +
                              count * TelemetryProtocolConstants.RecordSize +
                              TelemetryProtocolConstants.Crc32Size;

            if (_buffer.Count < blockLength)
                return results;

            var rawBlock = _buffer.GetRange(0, blockLength).ToArray();
            _buffer.RemoveRange(0, blockLength);

            var expectedCrc = BinaryPrimitives.ReadUInt32LittleEndian(
                rawBlock.AsSpan(blockLength - TelemetryProtocolConstants.Crc32Size, TelemetryProtocolConstants.Crc32Size));

            var actualCrc = Crc32.Compute(rawBlock.AsSpan(0, blockLength - TelemetryProtocolConstants.Crc32Size));

            if (expectedCrc != actualCrc)
            {
                results.Add(TelemetryParseResult.Invalid(
                    "CRC_MISMATCH",
                    $"CRC nie pasuje. Expected=0x{expectedCrc:X8}, Actual=0x{actualCrc:X8}.",
                    rawBlock));
                continue;
            }

            results.Add(TelemetryParseResult.Valid(ParseValidBlock(rawBlock, count, expectedCrc)));
        }
    }

    private TelemetryBlock ParseValidBlock(byte[] rawBlock, ushort count, uint crc)
    {
        var span = rawBlock.AsSpan();
        var offset = 0;

        var magic = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(offset, 2));
        offset += 2;

        if (magic != TelemetryProtocolConstants.Magic)
            throw new InvalidOperationException("Parser internal error: invalid magic in ParseValidBlock.");

        var version = span[offset];
        offset += 1;

        var sequence = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4));
        offset += 4;

        var timestamp = BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(offset, 8));
        offset += 8;

        // Count już znamy, ale przesuwamy offset.
        offset += 2;

        var records = new CanRecord[count];

        for (int i = 0; i < count; i++)
        {
            var header = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(offset, 2));
            offset += 2;

            var data = BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(offset, 8));
            offset += 8;

            records[i] = CanRecord.FromPacked(header, data);
        }

        return new TelemetryBlock
        {
            Version = version,
            Sequence = sequence,
            Timestamp = timestamp,
            Records = records,
            Crc32 = crc,
            RawBytes = rawBlock
        };
    }

    private int IndexOfMagic()
    {
        if (_buffer.Count < TelemetryProtocolConstants.MagicSize)
            return -1;

        var low = (byte)(TelemetryProtocolConstants.Magic & 0xFF);
        var high = (byte)(TelemetryProtocolConstants.Magic >> 8);

        for (int i = 0; i <= _buffer.Count - 2; i++)
        {
            if (_buffer[i] == low && _buffer[i + 1] == high)
                return i;
        }

        return -1;
    }

    private ushort ReadUInt16LittleEndian(int offset)
    {
        Span<byte> temp = stackalloc byte[2];
        temp[0] = _buffer[offset];
        temp[1] = _buffer[offset + 1];
        return BinaryPrimitives.ReadUInt16LittleEndian(temp);
    }
}

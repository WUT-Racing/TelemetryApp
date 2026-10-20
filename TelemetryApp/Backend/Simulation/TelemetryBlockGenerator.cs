using System.Buffers.Binary;
using TelemetryApp.Backend.Protocol;

namespace TelemetryApp.Backend.Simulation;

// <summary>
// Generator losowych danych telemetrycznych.
// Nieużywany w normalnej pracy z aplikacją - raczej do celów debug.
// </summary>
public sealed class TelemetryBlockGenerator
{
    private readonly SimulationOptions _options;
    private readonly ushort[] _canIds;
    private readonly Random _random = new(12345);
    private uint _sequence;
    private ulong _timestampMs;
    private ulong _sampleCounter;

    public TelemetryBlockGenerator(SimulationOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));

        _canIds = _options.CanIds.Count > 0
            ? _options.CanIds.Where(id => id <= CanRecord.CanIdMask).Distinct().ToArray()
            : new ushort[] { 0x100, 0x101, 0x102, 0x103, 0x104 };

        if (_canIds.Length == 0)
            _canIds = new ushort[] { 0x100 };

        _timestampMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    public byte[] GenerateNextBlock()
    {
        var recordsPerBlock = Math.Clamp(
            _options.RecordsPerBlock,
            1,
            TelemetryProtocolConstants.MaxRecordsPerBlock);

        if (_options.SkipSequenceEveryNBlocks > 0 && _sequence > 0 && _sequence % _options.SkipSequenceEveryNBlocks == 0)
            _sequence++;

        var blockLength = TelemetryProtocolConstants.HeaderSize +
                          recordsPerBlock * TelemetryProtocolConstants.RecordSize +
                          TelemetryProtocolConstants.Crc32Size;

        var block = new byte[blockLength];
        var span = block.AsSpan();
        var offset = 0;

        BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(offset, 2), TelemetryProtocolConstants.Magic);
        offset += 2;

        span[offset] = _options.ProtocolVersion;
        offset += 1;

        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(offset, 4), _sequence);
        offset += 4;

        BinaryPrimitives.WriteUInt64LittleEndian(span.Slice(offset, 8), _timestampMs);
        offset += 8;

        BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(offset, 2), (ushort)recordsPerBlock);
        offset += 2;

        for (int i = 0; i < recordsPerBlock; i++)
        {
            var canId = _canIds[(int)(_sampleCounter % (ulong)_canIds.Length)];
            var network = ((_sampleCounter / (ulong)_canIds.Length) & 1UL) != 0;
            var header = (ushort)((canId & CanRecord.CanIdMask) | (network ? 1 << CanRecord.NetworkBit : 0));
            var data = GenerateDataPayload(canId, network);

            BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(offset, 2), header);
            offset += 2;

            BinaryPrimitives.WriteUInt64LittleEndian(span.Slice(offset, 8), data);
            offset += 8;

            _sampleCounter++;
        }

        var crcOffset = blockLength - TelemetryProtocolConstants.Crc32Size;
        var crc = Crc32.Compute(span.Slice(0, crcOffset));

        if (_options.CorruptCrcEveryNBlocks > 0 && _sequence > 0 && _sequence % _options.CorruptCrcEveryNBlocks == 0)
            crc ^= 0x00000001u;

        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(crcOffset, 4), crc);

        _sequence++;
        _timestampMs += (ulong)_options.BlockPeriod.TotalMilliseconds;

        return block;
    }

    private ulong GenerateDataPayload(ushort canId, bool network)
    {
        // Dane są deterministyczne, ale zmienne. Dzięki temu wykresy później będą miały ruch.
        var t = _sampleCounter;
        var canIdValue = (ulong)canId;
        ulong payload = 0;

        var rpm = (ushort)(((t * 37UL) + (canIdValue * 3UL)) % 16000UL);
        var byte2 = (byte)(((t * 5UL) + canIdValue) % 200UL);
        var byte3 = (byte)(((t * 3UL) + (network ? 50UL : 0UL)) % 255UL);
        var byte4 = (byte)_random.Next(0, 255);
        var byte5 = (byte)((t / 2UL) % 255UL);
        var byte6 = (byte)((canIdValue + t) % 255UL);
        var byte7 = (byte)(network ? 1 : 0);

        payload |= rpm;
        payload |= (ulong)byte2 << 16;
        payload |= (ulong)byte3 << 24;
        payload |= (ulong)byte4 << 32;
        payload |= (ulong)byte5 << 40;
        payload |= (ulong)byte6 << 48;
        payload |= (ulong)byte7 << 56;

        return payload;
    }
}

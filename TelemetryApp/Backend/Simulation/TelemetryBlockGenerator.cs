using System.Buffers.Binary;
using TelemetryApp.Backend.Decoding;
using TelemetryApp.Backend.Protocol;

namespace TelemetryApp.Backend.Simulation;

public sealed class TelemetryBlockGenerator
{
    private const double TwoPi = Math.PI * 2.0;

    private readonly SimulationOptions _options;
    private readonly ushort[] _canIds;
    private readonly Random _random = new(12345);

    private uint _sequence;
    private ulong _timestampMs;
    private readonly ulong _startTimestampMs;
    private ulong _sampleCounter;

    public TelemetryBlockGenerator(SimulationOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));

        _canIds = ResolveCanIds(options);

        _timestampMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        _startTimestampMs = _timestampMs;
    }

    public byte[] GenerateNextBlock()
    {
        var recordsPerBlock = Math.Clamp(
            _options.RecordsPerBlock,
            1,
            TelemetryProtocolConstants.MaxRecordsPerBlock);

        if (_options.SkipSequenceEveryNBlocks > 0 &&
            _sequence > 0 &&
            _sequence % _options.SkipSequenceEveryNBlocks == 0)
        {
            _sequence++;
        }

        var blockLength =
            TelemetryProtocolConstants.HeaderSize +
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

        for (var i = 0; i < recordsPerBlock; i++)
        {
            var canId = _canIds[(int)(_sampleCounter % (ulong)_canIds.Length)];
            var network = ((_sampleCounter / (ulong)_canIds.Length) & 1UL) != 0;

            var header = (ushort)((canId & CanRecord.CanIdMask) |
                                  (network ? 1 << CanRecord.NetworkBit : 0));

            var data = GenerateDataPayload(canId, network);

            BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(offset, 2), header);
            offset += 2;

            BinaryPrimitives.WriteUInt64LittleEndian(span.Slice(offset, 8), data);
            offset += 8;

            _sampleCounter++;
        }

        var crcOffset = blockLength - TelemetryProtocolConstants.Crc32Size;
        var crc = Crc32.Compute(span.Slice(0, crcOffset));

        if (_options.CorruptCrcEveryNBlocks > 0 &&
            _sequence > 0 &&
            _sequence % _options.CorruptCrcEveryNBlocks == 0)
        {
            crc ^= 0x00000001u;
        }

        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(crcOffset, 4), crc);

        _sequence++;
        _timestampMs += (ulong)_options.BlockPeriod.TotalMilliseconds;

        return block;
    }

    private ulong GenerateDataPayload(ushort canId, bool network)
    {
        if (_options.Definitions is not null &&
            _options.Definitions.TryGetFrame(canId, out var frame))
        {
            return GenerateDefinedPayload(frame, network);
        }

        return GenerateLegacyPayload(canId, network);
    }

    private ulong GenerateDefinedPayload(CanFrameDefinition frame, bool network)
    {
        Span<byte> data = stackalloc byte[8];
        data.Clear();

        var seconds = (_timestampMs - _startTimestampMs) / 1000.0;

        foreach (var signal in frame.Signals)
        {
            if (signal.Reserved ||
                string.Equals(signal.DataType, "reserved", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var raw = GenerateRawSignalValue(signal, frame, seconds, network);
            WriteRawValue(data, signal, frame, raw);
        }

        return BinaryPrimitives.ReadUInt64LittleEndian(data);
    }

    private ulong GenerateRawSignalValue(
        CanSignalDefinition signal,
        CanFrameDefinition frame,
        double seconds,
        bool network)
    {
        var rawPhysical = GeneratePhysicalValue(signal, seconds, network);

        if (IsEnumLike(signal))
            return PhysicalToRaw(signal, rawPhysical, forceUnsigned: true);

        if (string.Equals(signal.DataType, "boolean", StringComparison.OrdinalIgnoreCase))
            return rawPhysical >= 0.5 ? 1UL : 0UL;

        return PhysicalToRaw(signal, rawPhysical, forceUnsigned: false);
    }

    private double GeneratePhysicalValue(CanSignalDefinition signal, double seconds, bool network)
    {
        var key = signal.Key.ToLowerInvariant();
        var unit = signal.Unit?.ToLowerInvariant() ?? string.Empty;

        var throttle = GetThrottlePercent(seconds);
        var speed = GetVehicleSpeedKph(seconds);
        var gear = GetGear(speed);
        var rpm = GetEngineRpm(seconds, speed, throttle, gear);
        var load = Math.Clamp(throttle / 100.0, 0.0, 1.0);

        var networkOffset = network ? 0.7 : 0.0;
        var noise = SmallNoise(key, seconds);

        // Silnik / ECU
        if (key.Contains("engine_rpm") || key == "rpm")
            return ClampToSignal(signal, rpm);

        if (key is "tps" or "dbw_position" or "dbw_target")
            return ClampToSignal(signal, throttle + noise);

        if (key.Contains("injector_pw"))
            return ClampToSignal(signal, 2.2 + load * 8.0 + rpm / 7000.0 * 2.5 + noise * 0.03);

        if (key is "map")
            return ClampToSignal(signal, 35.0 + load * 155.0 + SmoothWave(seconds, 11.0, 8.0) + noise);

        if (key.Contains("boost_target"))
            return ClampToSignal(signal, load < 0.35 ? 105.0 + load * 60.0 : 135.0 + load * 170.0);

        if (key.Contains("boost_pwm"))
            return ClampToSignal(signal, load < 0.35 ? 12.0 + load * 20.0 : 25.0 + load * 55.0);

        // Prędkość / bieg / napęd
        if (key is "vspd" || key.Contains("vehicle_speed") || key.Contains("speed"))
            return ClampToSignal(signal, speed + networkOffset);

        if (key == "gear")
            return gear;

        if (key.Contains("tc_drpm"))
            return ClampToSignal(signal, 850.0 + load * 800.0 + SmoothWave(seconds, 2.5, 80.0));

        if (key.Contains("tc_torque_reduction"))
            return ClampToSignal(signal, load > 0.82 ? 8.0 + SmoothPulse(seconds, 17.0, 14.0) : 0.0);

        if (key.Contains("pit_limiter"))
            return 0.0;

        // Temperatury
        if (key is "clt" || key.Contains("coolant"))
            return ClampToSignal(signal, Warmup(seconds, start: 76, target: 92, timeConstant: 85) + SmoothWave(seconds, 35, 1.8));

        if (key.Contains("oil_temp"))
            return ClampToSignal(signal, Warmup(seconds, start: 68, target: 103, timeConstant: 150) + SmoothWave(seconds, 41, 2.0));

        if (key.Contains("iat"))
            return ClampToSignal(signal, 28.0 + load * 13.0 + SmoothWave(seconds, 27, 2.0));

        if (key.Contains("ecu_temperature"))
            return ClampToSignal(signal, 39.0 + load * 8.0 + SmoothWave(seconds, 45, 1.0));

        if (key.Contains("board_temperature"))
            return ClampToSignal(signal, 34.0 + load * 12.0 + SmoothWave(seconds, 31, 1.5));

        if (key.Contains("egt_1"))
            return ClampToSignal(signal, 420.0 + load * 270.0 + rpm * 0.025 + SmoothWave(seconds, 7.0, 18.0));

        if (key.Contains("egt_2"))
            return ClampToSignal(signal, 415.0 + load * 260.0 + rpm * 0.024 + SmoothWave(seconds + 2.0, 8.0, 16.0));

        if (key.Contains("temp"))
            return ClampToSignal(signal, MidSafe(signal, 35.0) + SmoothWave(seconds, 19.0, 4.0));

        // Ciśnienia
        if (key.Contains("oil_pressure"))
            return ClampToSignal(signal, 1.3 + rpm / 1800.0 + load * 0.7 + SmoothWave(seconds, 5.0, 0.12));

        if (key.Contains("fuel_pressure"))
            return ClampToSignal(signal, 4.1 + load * 0.65 + SmoothWave(seconds, 9.0, 0.08));

        if (key == "baro")
            return ClampToSignal(signal, 100.0 + SmoothWave(seconds, 60.0, 0.7));

        if (key.Contains("pressure"))
            return ClampToSignal(signal, MidSafe(signal, 4.0) + SmoothWave(seconds, 13.0, 0.3));

        // Lambda / paliwo
        if (key == "lambda")
            return ClampToSignal(signal, 1.02 - load * 0.12 + SmoothWave(seconds, 6.0, 0.015));

        if (key.Contains("lambda_target"))
            return ClampToSignal(signal, load > 0.55 ? 0.88 + SmoothWave(seconds, 12.0, 0.01) : 1.0);

        if (key.Contains("lambda_correction"))
            return ClampToSignal(signal, 100.0 + SmoothWave(seconds, 8.0, 4.0) - load * 2.0);

        if (key.Contains("ethanol"))
            return ClampToSignal(signal, 85.0 + SmoothWave(seconds, 120.0, 1.0));

        if (key.Contains("fuel_used"))
            return ClampToSignal(signal, 0.04 * seconds + load * 0.02 * seconds);

        // Zapłon / dwell
        if (key.Contains("ignition"))
            return ClampToSignal(signal, 12.0 + rpm / 420.0 - load * 6.0 + SmoothWave(seconds, 10.0, 1.5));

        if (key.Contains("dwell"))
            return ClampToSignal(signal, 2.6 + load * 0.5 + SmoothWave(seconds, 15.0, 0.08));

        // Napięcia
        if (key.Contains("battery_voltage"))
            return ClampToSignal(signal, 13.75 + SmoothWave(seconds, 20.0, 0.12) - load * 0.08);

        if (key.Contains("voltage") || unit == "v")
        {
            if (key.StartsWith("ain") || key.StartsWith("a"))
                return ClampToSignal(signal, 2.45 + SmoothWave(seconds + StablePhase(key), 12.0, 1.2));

            return ClampToSignal(signal, MidSafe(signal, 2.5) + SmoothWave(seconds, 11.0, 0.4));
        }

        // Prądy / PWM / procenty
        if (key.Contains("total_current"))
            return ClampToSignal(signal, 32.0 + load * 28.0 + SmoothWave(seconds, 10.0, 2.5));

        if (key.Contains("pwm") || key.EndsWith("_dc"))
            return ClampToSignal(signal, 35.0 + load * 45.0 + SmoothWave(seconds, 7.0, 5.0));

        if (unit == "%" || key.Contains("percent") || key.Contains("position"))
            return ClampToSignal(signal, 45.0 + load * 35.0 + SmoothWave(seconds + StablePhase(key), 9.0, 6.0));

        // Enumy, boolean i bitfieldy
        if (string.Equals(signal.DataType, "boolean", StringComparison.OrdinalIgnoreCase))
        {
            if (key.Contains("error") || key.Contains("fault"))
                return 0;

            if (key.Contains("active") || key.Contains("enabled"))
                return SmoothPulse(seconds + StablePhase(key), 13.0, 1.0) > 0.4 ? 1 : 0;

            return 0;
        }

        if (string.Equals(signal.DataType, "bitfield", StringComparison.OrdinalIgnoreCase))
        {
            if (key.Contains("error"))
                return 0;

            if (key.Contains("flags") || key.Contains("outflags"))
                return 0b0000_0011;

            return 0;
        }

        if (string.Equals(signal.DataType, "enum", StringComparison.OrdinalIgnoreCase))
        {
            if (key.Contains("gear"))
                return gear;

            if (key.Contains("mode"))
                return load > 0.65 ? 2 : 1;

            if (key.Contains("status"))
                return 1;

            return 0;
        }

        // Uniwersalny fallback po safe_min/safe_max.
        return ClampToSignal(signal, MidSafe(signal, 0.0) + SmoothWave(seconds + StablePhase(key), 15.0, SafeAmplitude(signal)));
    }

    private static double GetThrottlePercent(double seconds)
    {
        var baseThrottle = 24.0 + SmoothWave(seconds, 18.0, 11.0);
        var pull1 = SmoothPulse(seconds, 22.0, 38.0);
        var pull2 = SmoothPulse(seconds + 8.0, 31.0, 24.0);

        return Math.Clamp(baseThrottle + pull1 + pull2, 6.0, 92.0);
    }

    private static double GetVehicleSpeedKph(double seconds)
    {
        var cruise = 62.0 + SmoothWave(seconds, 38.0, 18.0);
        var acceleration = SmoothPulse(seconds + 5.0, 27.0, 28.0);

        return Math.Clamp(cruise + acceleration, 0.0, 138.0);
    }

    private static int GetGear(double speedKph)
    {
        if (speedKph < 8)
            return 1;

        if (speedKph < 28)
            return 2;

        if (speedKph < 52)
            return 3;

        if (speedKph < 82)
            return 4;

        if (speedKph < 112)
            return 5;

        return 6;
    }

    private static double GetEngineRpm(double seconds, double speedKph, double throttle, int gear)
    {
        var gearRatio = gear switch
        {
            1 => 95.0,
            2 => 72.0,
            3 => 55.0,
            4 => 43.0,
            5 => 35.0,
            _ => 30.0
        };

        var rpm = 850.0 + speedKph * gearRatio + throttle * 18.0 + SmoothWave(seconds, 4.5, 140.0);

        return Math.Clamp(rpm, 850.0, 6900.0);
    }

    private static double Warmup(double seconds, double start, double target, double timeConstant)
    {
        var k = 1.0 - Math.Exp(-Math.Max(0, seconds) / Math.Max(1.0, timeConstant));
        return start + (target - start) * k;
    }

    private static double SmoothWave(double seconds, double periodSeconds, double amplitude)
    {
        return Math.Sin(seconds / periodSeconds * TwoPi) * amplitude;
    }

    private static double SmoothPulse(double seconds, double periodSeconds, double amplitude)
    {
        var s = Math.Sin(seconds / periodSeconds * TwoPi);
        var pulse = Math.Max(0.0, s);
        return pulse * pulse * amplitude;
    }

    private static double SmallNoise(string key, double seconds)
    {
        return Math.Sin(seconds * 3.7 + StablePhase(key)) * 0.35;
    }

    private static double StablePhase(string key)
    {
        unchecked
        {
            var hash = 17;

            foreach (var c in key)
                hash = hash * 31 + c;

            return Math.Abs(hash % 1000) / 1000.0 * TwoPi;
        }
    }

    private static double MidSafe(CanSignalDefinition signal, double fallback)
    {
        if (signal.SafeMin.HasValue && signal.SafeMax.HasValue)
            return (signal.SafeMin.Value + signal.SafeMax.Value) / 2.0;

        return fallback;
    }

    private static double SafeAmplitude(CanSignalDefinition signal)
    {
        if (signal.SafeMin.HasValue && signal.SafeMax.HasValue)
            return Math.Max(0.1, (signal.SafeMax.Value - signal.SafeMin.Value) * 0.25);

        return 1.0;
    }

    private static double ClampToSignal(CanSignalDefinition signal, double value)
    {
        if (signal.SafeMin.HasValue)
            value = Math.Max(signal.SafeMin.Value, value);

        if (signal.SafeMax.HasValue)
            value = Math.Min(signal.SafeMax.Value, value);

        return value;
    }

    private static ulong PhysicalToRaw(
        CanSignalDefinition signal,
        double physical,
        bool forceUnsigned)
    {
        var factor = Math.Abs(signal.Factor) < double.Epsilon ? 1.0 : signal.Factor;
        var rawDouble = (physical - signal.Offset) / factor;

        if (double.IsNaN(rawDouble) || double.IsInfinity(rawDouble))
            rawDouble = 0;

        var isSigned =
            !forceUnsigned &&
            string.Equals(signal.DataType, "signed", StringComparison.OrdinalIgnoreCase);

        var bitLength = Math.Clamp(signal.BitLength, 1, 63);

        if (isSigned)
        {
            var min = -(1L << (bitLength - 1));
            var max = (1L << (bitLength - 1)) - 1;

            var signedRaw = (long)Math.Round(rawDouble);
            signedRaw = Math.Clamp(signedRaw, min, max);

            if (signedRaw < 0)
                return (ulong)((1L << bitLength) + signedRaw);

            return (ulong)signedRaw;
        }
        else
        {
            var max = bitLength >= 63
                ? ulong.MaxValue >> 1
                : (1UL << bitLength) - 1UL;

            var unsignedRaw = rawDouble < 0
                ? 0UL
                : (ulong)Math.Round(rawDouble);

            return Math.Min(unsignedRaw, max);
        }
    }

    private static void WriteRawValue(
        Span<byte> data,
        CanSignalDefinition signal,
        CanFrameDefinition frame,
        ulong raw)
    {
        var endianness = signal.Endianness ?? frame.Endianness ?? "little";

        if (string.Equals(endianness, "big", StringComparison.OrdinalIgnoreCase))
        {
            WriteBigEndianRaw(data, signal, raw);
            return;
        }

        WriteLittleEndianRaw(data, signal, raw);
    }

    private static void WriteLittleEndianRaw(
        Span<byte> data,
        CanSignalDefinition signal,
        ulong raw)
    {
        var bitLength = Math.Clamp(signal.BitLength, 1, 64);
        var startBit = signal.ByteStart * 8 + signal.BitStart;

        if (startBit < 0 || startBit >= 64)
            return;

        if (startBit + bitLength > 64)
            bitLength = 64 - startBit;

        var valueMask = CreateMask(bitLength);
        var payload = BinaryPrimitives.ReadUInt64LittleEndian(data);

        var mask = valueMask << startBit;
        payload = (payload & ~mask) | ((raw & valueMask) << startBit);

        BinaryPrimitives.WriteUInt64LittleEndian(data, payload);
    }

    private static void WriteBigEndianRaw(
        Span<byte> data,
        CanSignalDefinition signal,
        ulong raw)
    {
        // Obsługa typowych sygnałów big-endian z JSON-a:
        // bajtowo wyrównane pola 8/16/32-bit.
        if (signal.BitStart == 0 && signal.BitLength % 8 == 0)
        {
            var byteLength = signal.BitLength / 8;

            for (var i = 0; i < byteLength; i++)
            {
                var shift = (byteLength - 1 - i) * 8;
                data[signal.ByteStart + i] = (byte)((raw >> shift) & 0xFF);
            }

            return;
        }

        // Fallback bitowy.
        var bitLength = Math.Clamp(signal.BitLength, 1, 64);

        for (var i = 0; i < bitLength; i++)
        {
            var absoluteBit = signal.ByteStart * 8 + signal.BitStart + i;

            if (absoluteBit < 0 || absoluteBit >= 64)
                continue;

            var byteIndex = absoluteBit / 8;
            var bitInByte = 7 - (absoluteBit % 8);
            var bit = (raw >> (bitLength - 1 - i)) & 1UL;

            if (bit != 0)
                data[byteIndex] |= (byte)(1 << bitInByte);
            else
                data[byteIndex] &= (byte)~(1 << bitInByte);
        }
    }

    private static bool IsEnumLike(CanSignalDefinition signal)
    {
        if (string.Equals(signal.DataType, "enum", StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.Equals(signal.DataType, "bitfield", StringComparison.OrdinalIgnoreCase))
            return true;

        return signal.Enum is { Count: > 0 };
    }

    private static ulong CreateMask(int bitLength)
    {
        if (bitLength <= 0)
            return 0;

        if (bitLength >= 64)
            return ulong.MaxValue;

        return (1UL << bitLength) - 1UL;
    }

    private static ushort[] ResolveCanIds(SimulationOptions options)
    {
        var ids = options.CanIds.Count > 0
            ? options.CanIds
            : options.Definitions?.FramesById.Keys.ToArray() ?? Array.Empty<ushort>();

        var result = ids
            .Where(id => id <= CanRecord.CanIdMask)
            .Distinct()
            .ToArray();

        if (result.Length > 0)
            return result;

        return new ushort[] { 0x600, 0x601, 0x602, 0x603, 0x604, 0x605, 0x607 };
    }

    private ulong GenerateLegacyPayload(ushort canId, bool network)
    {
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
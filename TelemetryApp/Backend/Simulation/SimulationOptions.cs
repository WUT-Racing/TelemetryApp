using TelemetryApp.Backend.Decoding;

namespace TelemetryApp.Backend.Simulation;

public sealed class SimulationOptions
{
    /// <summary>
    /// Ile bloków telemetrycznych generować na sekundę.
    /// Dla bloku co 100 ms: 10.
    /// </summary>
    public int BlocksPerSecond { get; init; } = 10;

    /// <summary>
    /// Ile rekordów CAN wkładać do jednego bloku.
    /// 2105 ramek/s / 10 bloków/s ≈ 211.
    /// </summary>
    public int RecordsPerBlock { get; init; } = 211;

    /// <summary>
    /// Wersja protokołu wpisywana do bloku.
    /// </summary>
    public byte ProtocolVersion { get; init; } = 1;

    /// <summary>
    /// Co ile bloków symulator ma uszkodzić CRC.
    /// 0 = nigdy.
    /// </summary>
    public int CorruptCrcEveryNBlocks { get; init; } = 0;

    /// <summary>
    /// Co ile bloków symulator ma pominąć jeden numer sequence.
    /// 0 = nigdy.
    /// </summary>
    public int SkipSequenceEveryNBlocks { get; init; } = 0;

    /// <summary>
    /// Opcjonalna lista CAN ID, które symulator ma generować.
    /// Jeżeli pusta, użyje ID z JSON-a.
    /// </summary>
    public IReadOnlyList<ushort> CanIds { get; init; } = Array.Empty<ushort>();

    /// <summary>
    /// Definicje z sygnaly.json.
    /// Dzięki nim symulator może generować fizycznie sensowne wartości
    /// i pakować je poprawnie do ramek CAN.
    /// </summary>
    public CanDefinitionSet? Definitions { get; init; }

    public TimeSpan BlockPeriod
    {
        get
        {
            var blocksPerSecond = Math.Max(1, BlocksPerSecond);
            return TimeSpan.FromMilliseconds(1000.0 / blocksPerSecond);
        }
    }
}
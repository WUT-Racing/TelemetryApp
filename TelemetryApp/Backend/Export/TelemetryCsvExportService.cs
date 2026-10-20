using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Maui.Storage;
using TelemetryApp.Backend.Decoding;
using TelemetryApp.Backend.Protocol;
using TelemetryApp.Backend.Sessions;
using TelemetryApp.Backend.Storage;
using TelemetryApp.Backend.UserSettings;

namespace TelemetryApp.Backend.Export;

// <summary>
// Logika zajmująca się eksportem wczytanych danych telemetrycznych do pliku CSV.
// Obecna implementacja nie jest optymalna i wymaga przemyślenia możliwych do wprowadzenia zmian.
// Skonsultować z Robertem @gruszczrob
// </summary>
public sealed class TelemetryCsvExportService
{
    private readonly SignalDisplaySettingsStore _displaySettingsStore;
    private readonly TelemetryExportSettingsStore _exportSettingsStore;

    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public TelemetryCsvExportService(
        SignalDisplaySettingsStore displaySettingsStore,
        TelemetryExportSettingsStore exportSettingsStore)
    {
        _displaySettingsStore = displaySettingsStore ?? throw new ArgumentNullException(nameof(displaySettingsStore));
        _exportSettingsStore = exportSettingsStore ?? throw new ArgumentNullException(nameof(exportSettingsStore));
    }

    public Task<string> ExportSessionAsync(
        TelemetrySession session,
        bool includeEnums = true,
        CancellationToken cancellationToken = default)
    {
        if (session is null)
            throw new ArgumentNullException(nameof(session));

        return ExportAsync(
            session,
            session.Runs.ToList(),
            "session",
            includeEnums,
            cancellationToken);
    }

    public Task<string> ExportRunAsync(
        TelemetrySession session,
        TelemetrySessionRun run,
        bool includeEnums = true,
        CancellationToken cancellationToken = default)
    {
        if (session is null)
            throw new ArgumentNullException(nameof(session));

        if (run is null)
            throw new ArgumentNullException(nameof(run));

        return ExportAsync(
            session,
            new[] { run },
            "run",
            includeEnums,
            cancellationToken);
    }

    private async Task<string> ExportAsync(
        TelemetrySession session,
        IReadOnlyList<TelemetrySessionRun> selectedRuns,
        string exportScope,
        bool includeEnums,
        CancellationToken cancellationToken)
    {
        if (selectedRuns.Count == 0)
            throw new InvalidOperationException("Nie ma żadnych runów do eksportu.");

        var exportOptions = _exportSettingsStore.GetOptions();

        if (!exportOptions.HasAnyCsv)
            throw new InvalidOperationException("W ustawieniach eksportu nie wybrano żadnego pliku CSV do utworzenia.");

        var runsWithLogs = selectedRuns
            .Where(run => !string.IsNullOrWhiteSpace(run.RawLogPath) && File.Exists(run.RawLogPath))
            .OrderBy(run => run.StartedAtLocal)
            .ToList();

        if (runsWithLogs.Count == 0)
            throw new InvalidOperationException("Nie znaleziono plików .rfdlog dla wybranych runów.");

        var needsDecodedCsv = exportOptions.ExportLongCsv || exportOptions.ExportWideCsv;

        CanSignalDecoder? decoder = null;
        Dictionary<string, SignalExportInfo> signalInfoByKey = new(StringComparer.OrdinalIgnoreCase);

        if (needsDecodedCsv)
        {
            var definitions = await LoadDefaultDefinitionsAsync(cancellationToken);
            decoder = new CanSignalDecoder(definitions);
            signalInfoByKey = BuildSignalInfoMap(definitions);
        }

        var exportDirectory = CreateExportDirectory(session, exportScope, runsWithLogs);

        var metadataPath = Path.Combine(exportDirectory, "metadata.json");
        var longPath = Path.Combine(exportDirectory, "decoded_signals_long.csv");
        var widePath = Path.Combine(exportDirectory, "decoded_signals_wide.csv");
        var rawPath = Path.Combine(exportDirectory, "raw_frames.csv");

        var warnings = new List<string>();
        var decodedSamples = new List<ExportDecodedSample>();

        ulong? firstTimestampMs = null;
        long blockCount = 0;
        long rawFrameCount = 0;

        StreamWriter? rawWriter = null;

        try
        {
            if (exportOptions.ExportRawFramesCsv)
            {
                rawWriter = await CreateExcelCsvWriterAsync(rawPath, cancellationToken);

                await rawWriter.WriteLineAsync("sep=;");
                await rawWriter.WriteLineAsync(
                    "Czas lokalny;Czas od początku eksportu [s];Timestamp ms;Run index;Run ID;Run source;Run started at;Run stopped at;Sequence;Network;CAN ID;Data hex");
            }

            foreach (var run in runsWithLogs)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var runIndex = GetRunIndex(session, run);

                try
                {
                    await using var reader = await RawTelemetryLogReader.OpenAsync(run.RawLogPath, cancellationToken);

                    await foreach (var block in reader.ReadAllBlocksAsync(cancellationToken))
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        firstTimestampMs ??= block.Timestamp;
                        blockCount++;
                        rawFrameCount += block.Records.Length;

                        if (rawWriter is not null)
                        {
                            await WriteRawFrameRowsAsync(
                                rawWriter,
                                block,
                                run,
                                runIndex,
                                firstTimestampMs.Value,
                                cancellationToken);
                        }

                        if (decoder is null)
                            continue;

                        var decodedBatch = decoder.Decode(block);

                        foreach (var signal in decodedBatch.Signals)
                        {
                            if (!includeEnums && signal.IsEnumLike)
                                continue;

                            var signalMapKey = MakeSignalKey(signal.CanId, signal.Key);
                            var hasSignalInfo = signalInfoByKey.TryGetValue(signalMapKey, out var info);

                            var type = signal.IsEnumLike ? "enum" : "sensor";
                            var savedSettings = _displaySettingsStore.GetSettings(type, signal.CanId, signal.Key);

                            var defaultName = FirstNonEmpty(
                                signal.Name,
                                hasSignalInfo ? info.FriendlyName : null,
                                HumanizeKey(signal.Key),
                                "Sygnał");

                            var displayName = string.IsNullOrWhiteSpace(savedSettings?.DisplayName)
                                ? defaultName
                                : savedSettings!.DisplayName;

                            decodedSamples.Add(new ExportDecodedSample(
                                Run: run,
                                RunIndex: runIndex,
                                TimestampMs: signal.Timestamp,
                                BlockSequence: signal.BlockSequence,
                                CanId: signal.CanId,
                                Network: signal.Network,
                                Key: signal.Key,
                                Name: displayName,
                                Unit: signal.Unit,
                                Value: signal.Value,
                                RawValue: signal.RawValue,
                                DisplayValue: signal.DisplayValue,
                                IsEnumLike: signal.IsEnumLike,
                                IsEnabled: savedSettings?.IsEnabled,
                                FrameSummary: hasSignalInfo ? info.FrameSummary : string.Empty));
                        }
                    }
                }
                catch (Exception ex) when (ex is EndOfStreamException or InvalidDataException)
                {
                    warnings.Add($"Run {runIndex} ({run.Source}) nie został odczytany do końca: {ex.Message}");
                }
            }
        }
        finally
        {
            if (rawWriter is not null)
                await rawWriter.DisposeAsync();
        }

        if (exportOptions.ExportLongCsv)
            await WriteLongSignalsCsvAsync(longPath, decodedSamples, firstTimestampMs, cancellationToken);

        if (exportOptions.ExportWideCsv)
            await WriteWideSignalsCsvAsync(widePath, decodedSamples, firstTimestampMs, cancellationToken);

        await WriteMetadataAsync(
            metadataPath,
            session,
            runsWithLogs,
            exportScope,
            includeEnums,
            exportOptions,
            blockCount,
            rawFrameCount,
            decodedSamples.Count,
            warnings,
            cancellationToken);

        return exportDirectory;
    }

    private async Task WriteRawFrameRowsAsync(
        StreamWriter writer,
        TelemetryBlock block,
        TelemetrySessionRun run,
        int runIndex,
        ulong firstTimestampMs,
        CancellationToken cancellationToken)
    {
        var culture = CultureInfo.GetCultureInfo("pl-PL");

        foreach (var record in block.Records)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var row = new[]
            {
                ToDateTime(block.Timestamp).ToString("yyyy-MM-dd HH:mm:ss.fff", culture),
                ToRelativeSeconds(block.Timestamp, firstTimestampMs).ToString("0.000", culture),
                block.Timestamp.ToString(),
                runIndex.ToString(),
                run.Id,
                run.Source,
                run.StartedAtLocal.ToString("yyyy-MM-dd HH:mm:ss.fff", culture),
                run.StoppedAtLocal?.ToString("yyyy-MM-dd HH:mm:ss.fff", culture) ?? string.Empty,
                block.Sequence.ToString(),
                record.Network ? "1" : "0",
                $"0x{record.CanId:X3}",
                record.Data.ToString("X16")
            };

            await writer.WriteLineAsync(string.Join(';', row.Select(EscapeCsvCell)));
        }
    }

    private async Task WriteLongSignalsCsvAsync(
        string filePath,
        IReadOnlyList<ExportDecodedSample> samples,
        ulong? firstTimestampMs,
        CancellationToken cancellationToken)
    {
        await using var writer = await CreateExcelCsvWriterAsync(filePath, cancellationToken);

        await writer.WriteLineAsync("sep=;");
        await writer.WriteLineAsync(
            "Czas lokalny;Czas od początku eksportu [s];Timestamp ms;Run index;Run ID;Run source;Run started at;Run stopped at;Sequence;Typ;Nazwa;CAN ID;Network;Klucz;Raw;Wartość;Wartość tekstowa;Jednostka;Włączony;Ramka");

        if (samples.Count == 0 || firstTimestampMs is null)
            return;

        var culture = CultureInfo.GetCultureInfo("pl-PL");

        foreach (var sample in samples
            .OrderBy(sample => sample.TimestampMs)
            .ThenBy(sample => sample.RunIndex)
            .ThenBy(sample => sample.BlockSequence)
            .ThenBy(sample => sample.CanId)
            .ThenBy(sample => sample.Key, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var row = new[]
            {
                ToDateTime(sample.TimestampMs).ToString("yyyy-MM-dd HH:mm:ss.fff", culture),
                ToRelativeSeconds(sample.TimestampMs, firstTimestampMs.Value).ToString("0.000", culture),
                sample.TimestampMs.ToString(),
                sample.RunIndex.ToString(),
                sample.Run.Id,
                sample.Run.Source,
                sample.Run.StartedAtLocal.ToString("yyyy-MM-dd HH:mm:ss.fff", culture),
                sample.Run.StoppedAtLocal?.ToString("yyyy-MM-dd HH:mm:ss.fff", culture) ?? string.Empty,
                sample.BlockSequence.ToString(),
                sample.IsEnumLike ? "enum" : "sensor",
                sample.Name,
                $"0x{sample.CanId:X3}",
                sample.Network ? "1" : "0",
                sample.Key,
                sample.RawValue.ToString(culture),
                sample.IsEnumLike ? string.Empty : sample.Value.ToString("0.############", culture),
                sample.DisplayValue ?? string.Empty,
                sample.Unit ?? string.Empty,
                sample.IsEnabled.HasValue ? (sample.IsEnabled.Value ? "tak" : "nie") : string.Empty,
                sample.FrameSummary
            };

            await writer.WriteLineAsync(string.Join(';', row.Select(EscapeCsvCell)));
        }
    }

    private async Task WriteWideSignalsCsvAsync(
        string filePath,
        IReadOnlyList<ExportDecodedSample> samples,
        ulong? firstTimestampMs,
        CancellationToken cancellationToken)
    {
        await using var writer = await CreateExcelCsvWriterAsync(filePath, cancellationToken);

        await writer.WriteLineAsync("sep=;");

        var columns = samples
            .GroupBy(sample => MakeSignalKey(sample.CanId, sample.Key), StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var sample = group.First();

                return new ExportColumn(
                    MakeSignalKey(sample.CanId, sample.Key),
                    BuildWideHeader(sample),
                    sample.IsEnumLike);
            })
            .OrderBy(column => column.Header, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var header = new[]
        {
            "Czas lokalny",
            "Czas od początku eksportu [s]",
            "Timestamp ms",
            "Run index",
            "Run ID",
            "Run source",
            "Run started at",
            "Run stopped at",
            "Sequence"
        }.Concat(columns.Select(column => column.Header));

        await writer.WriteLineAsync(string.Join(';', header.Select(EscapeCsvCell)));

        if (samples.Count == 0 || firstTimestampMs is null)
            return;

        var culture = CultureInfo.GetCultureInfo("pl-PL");

        var rows = samples
            .GroupBy(sample => new
            {
                sample.Run.Id,
                sample.TimestampMs,
                sample.BlockSequence
            })
            .OrderBy(group => group.Key.TimestampMs)
            .ThenBy(group => group.First().RunIndex)
            .ThenBy(group => group.Key.BlockSequence);

        foreach (var group in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var first = group.First();

            var valuesBySignal = group
                .GroupBy(sample => MakeSignalKey(sample.CanId, sample.Key), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.Last(),
                    StringComparer.OrdinalIgnoreCase);

            var row = new List<string>
            {
                ToDateTime(group.Key.TimestampMs).ToString("yyyy-MM-dd HH:mm:ss.fff", culture),
                ToRelativeSeconds(group.Key.TimestampMs, firstTimestampMs.Value).ToString("0.000", culture),
                group.Key.TimestampMs.ToString(),
                first.RunIndex.ToString(),
                first.Run.Id,
                first.Run.Source,
                first.Run.StartedAtLocal.ToString("yyyy-MM-dd HH:mm:ss.fff", culture),
                first.Run.StoppedAtLocal?.ToString("yyyy-MM-dd HH:mm:ss.fff", culture) ?? string.Empty,
                group.Key.BlockSequence.ToString()
            };

            foreach (var column in columns)
            {
                if (!valuesBySignal.TryGetValue(column.Key, out var sample))
                {
                    row.Add(string.Empty);
                    continue;
                }

                row.Add(column.IsEnumLike
                    ? sample.DisplayValue ?? sample.RawValue.ToString(culture)
                    : sample.Value.ToString("0.############", culture));
            }

            await writer.WriteLineAsync(string.Join(';', row.Select(EscapeCsvCell)));
        }
    }

    private async Task WriteMetadataAsync(
        string path,
        TelemetrySession session,
        IReadOnlyList<TelemetrySessionRun> runs,
        string exportScope,
        bool includeEnums,
        TelemetryExportOptions exportOptions,
        long blockCount,
        long rawFrameCount,
        long decodedSignalCount,
        IReadOnlyList<string> warnings,
        CancellationToken cancellationToken)
    {
        var metadata = new
        {
            ExportedAtLocal = DateTime.Now,
            ExportScope = exportScope,
            IncludeEnums = includeEnums,
            ExportFiles = new
            {
                LongCsv = exportOptions.ExportLongCsv,
                WideCsv = exportOptions.ExportWideCsv,
                RawFramesCsv = exportOptions.ExportRawFramesCsv,
                MetadataJson = true
            },
            Session = new
            {
                session.Id,
                session.Name,
                session.IsTest,
                session.StartedAtLocal,
                session.EndedAtLocal,
                session.DirectoryPath,
                session.DriverName
            },
            Runs = runs.Select(run => new
            {
                Index = GetRunIndex(session, run),
                run.Id,
                run.Source,
                run.StartedAtLocal,
                run.StoppedAtLocal,
                run.RawLogPath,
                RawLogExists = File.Exists(run.RawLogPath),
                RawLogBytes = File.Exists(run.RawLogPath) ? new FileInfo(run.RawLogPath).Length : 0
            }).ToList(),
            Counts = new
            {
                Blocks = blockCount,
                RawFrames = rawFrameCount,
                DecodedSignals = decodedSignalCount
            },
            Files = BuildExportFileList(exportOptions),
            Warnings = warnings,
            Notes = "CSV używa separatora średnika i kodowania UTF-8 BOM dla Excela. metadata.json jest tworzony zawsze. Eksport jest odtwarzany z plików .rfdlog."
        };

        var json = JsonSerializer.Serialize(metadata, _jsonOptions);

        await File.WriteAllTextAsync(
            path,
            json,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
            cancellationToken);
    }

    private string CreateExportDirectory(
        TelemetrySession session,
        string exportScope,
        IReadOnlyList<TelemetrySessionRun> runs)
    {
        var exportRoot = Path.Combine(session.DirectoryPath, "Exports");
        Directory.CreateDirectory(exportRoot);

        var scopeText = exportScope.Equals("run", StringComparison.OrdinalIgnoreCase)
            ? $"run_{GetRunIndex(session, runs[0]):00}_{MakeSafeFileName(runs[0].Source)}"
            : $"session_{MakeSafeFileName(session.Name)}";

        var directory = Path.Combine(
            exportRoot,
            $"{DateTime.Now:yyyyMMdd_HHmmss}_{scopeText}");

        Directory.CreateDirectory(directory);
        return directory;
    }

    private static async Task<CanDefinitionSet> LoadDefaultDefinitionsAsync(CancellationToken cancellationToken)
    {
        await using var stream = await FileSystem.OpenAppPackageFileAsync("sygnaly.json");
        return await CanDefinitionLoader.LoadFromStreamAsync(stream, cancellationToken);
    }

    private static Dictionary<string, SignalExportInfo> BuildSignalInfoMap(CanDefinitionSet definitions)
    {
        var map = new Dictionary<string, SignalExportInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var frame in definitions.Source.Frames)
        {
            foreach (var signal in frame.Signals)
            {
                if (signal.Reserved ||
                    string.Equals(signal.DataType, "reserved", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var key = MakeSignalKey(frame.Id, signal.Key);

                map[key] = new SignalExportInfo(
                    FriendlyName: BuildFriendlySignalName(frame, signal),
                    FrameSummary: frame.Summary ?? string.Empty,
                    IsEnumLike: IsEnumLike(signal));
            }
        }

        return map;
    }

    private static int GetRunIndex(TelemetrySession session, TelemetrySessionRun run)
    {
        var index = session.Runs.FindIndex(item =>
            string.Equals(item.Id, run.Id, StringComparison.OrdinalIgnoreCase));

        return index < 0 ? 1 : index + 1;
    }

    private static string BuildWideHeader(ExportDecodedSample sample)
    {
        var unit = string.IsNullOrWhiteSpace(sample.Unit) || sample.IsEnumLike
            ? string.Empty
            : $" [{sample.Unit}]";

        return $"{sample.Name}{unit} (0x{sample.CanId:X3}/{sample.Key})";
    }

    private static string[] BuildExportFileList(TelemetryExportOptions options)
    {
        var files = new List<string>
        {
            "metadata.json"
        };

        if (options.ExportWideCsv)
            files.Add("decoded_signals_wide.csv");

        if (options.ExportLongCsv)
            files.Add("decoded_signals_long.csv");

        if (options.ExportRawFramesCsv)
            files.Add("raw_frames.csv");

        return files.ToArray();
    }

    private static async Task<StreamWriter> CreateExcelCsvWriterAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        var stream = new FileStream(
            filePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 64 * 1024,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);

        var bom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetPreamble();
        await stream.WriteAsync(bom, cancellationToken);

        return new StreamWriter(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            bufferSize: 64 * 1024,
            leaveOpen: false);
    }

    private static double ToRelativeSeconds(ulong timestampMs, ulong firstTimestampMs)
    {
        return timestampMs >= firstTimestampMs
            ? (timestampMs - firstTimestampMs) / 1000.0
            : 0.0;
    }

    private static DateTime ToDateTime(ulong timestampMs)
    {
        try
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(unchecked((long)timestampMs))
                .ToLocalTime()
                .DateTime;
        }
        catch
        {
            return DateTime.Now;
        }
    }

    private static string EscapeCsvCell(string? value)
    {
        value ??= string.Empty;

        if (value.Contains(';') ||
            value.Contains('"') ||
            value.Contains('\n') ||
            value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }

    private static string MakeSafeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();

        var safe = new string(
            value.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());

        safe = Regex.Replace(safe, "\\s+", "_").Trim('_');

        return string.IsNullOrWhiteSpace(safe)
            ? "export"
            : safe;
    }

    private static string MakeSignalKey(ushort canId, string key)
    {
        return $"{canId:X3}|{key}";
    }

    private static bool IsEnumLike(CanSignalDefinition signal)
    {
        if (string.Equals(signal.DataType, "enum", StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.Equals(signal.DataType, "bitfield", StringComparison.OrdinalIgnoreCase))
            return true;

        return signal.Enum is { Count: > 0 };
    }

    private static string BuildFriendlySignalName(CanFrameDefinition frame, CanSignalDefinition signal)
    {
        var signalName = FirstNonEmpty(signal.Name, HumanizeKey(signal.Key), "Sygnał");
        var frameName = CleanName(frame.Summary);

        if (string.IsNullOrWhiteSpace(frameName))
            return signalName;

        if (signalName.Contains(frameName, StringComparison.OrdinalIgnoreCase))
            return signalName;

        return $"{frameName} — {signalName}";
    }

    private static string HumanizeKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return string.Empty;

        var text = key.Replace('_', ' ').Replace('-', ' ');
        text = Regex.Replace(text, "\\s+", " ").Trim();

        if (text.Length == 0)
            return string.Empty;

        return char.ToUpper(text[0]) + text[1..];
    }

    private static string CleanName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return Regex.Replace(value.Trim(), "\\s+", " ");
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return CleanName(value);
        }

        return string.Empty;
    }

    private readonly record struct ExportColumn(
        string Key,
        string Header,
        bool IsEnumLike);

    private readonly record struct SignalExportInfo(
        string FriendlyName,
        string FrameSummary,
        bool IsEnumLike);

    private readonly record struct ExportDecodedSample(
        TelemetrySessionRun Run,
        int RunIndex,
        ulong TimestampMs,
        uint BlockSequence,
        ushort CanId,
        bool Network,
        string Key,
        string Name,
        string? Unit,
        double Value,
        long RawValue,
        string? DisplayValue,
        bool IsEnumLike,
        bool? IsEnabled,
        string FrameSummary);
}
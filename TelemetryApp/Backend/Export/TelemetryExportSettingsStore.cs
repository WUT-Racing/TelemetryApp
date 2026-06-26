using Microsoft.Maui.Storage;

namespace TelemetryApp.Backend.Export;

public sealed class TelemetryExportSettingsStore
{
    private const string ExportLongCsvKey = "ExportLongCsv";
    private const string ExportWideCsvKey = "ExportWideCsv";
    private const string ExportRawFramesCsvKey = "ExportRawFramesCsv";

    public TelemetryExportOptions GetOptions()
    {
        return new TelemetryExportOptions
        {
            ExportLongCsv = Preferences.Get(ExportLongCsvKey, true),
            ExportWideCsv = Preferences.Get(ExportWideCsvKey, true),
            ExportRawFramesCsv = Preferences.Get(ExportRawFramesCsvKey, true)
        };
    }

    public void SaveOptions(TelemetryExportOptions options)
    {
        Preferences.Set(ExportLongCsvKey, options.ExportLongCsv);
        Preferences.Set(ExportWideCsvKey, options.ExportWideCsv);
        Preferences.Set(ExportRawFramesCsvKey, options.ExportRawFramesCsv);
    }
}

public sealed class TelemetryExportOptions
{
    public bool ExportLongCsv { get; set; } = true;
    public bool ExportWideCsv { get; set; } = true;
    public bool ExportRawFramesCsv { get; set; } = true;

    public bool HasAnyCsv =>
        ExportLongCsv ||
        ExportWideCsv ||
        ExportRawFramesCsv;
}
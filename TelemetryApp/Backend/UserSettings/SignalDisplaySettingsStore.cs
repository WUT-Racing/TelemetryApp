using System.Text.Json;
using Microsoft.Maui.Storage;
using TelemetryApp.Frontend.Data;

namespace TelemetryApp.Backend.UserSettings;

public sealed class SignalDisplaySettingsStore
{
    private readonly object _sync = new();
    private readonly string _filePath;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private Dictionary<string, SignalDisplaySettings> _settings = new(StringComparer.OrdinalIgnoreCase);

    public SignalDisplaySettingsStore()
    {
        var directory = Path.Combine(FileSystem.AppDataDirectory, "Config");
        Directory.CreateDirectory(directory);
        _filePath = Path.Combine(directory, "signal_display_settings.json");
        Load();
    }

    public void ApplyToSensor(SensorReading sensor)
    {
        var key = MakeKey("sensor", sensor.CanId, sensor.SignalKey);
        if (!_settings.TryGetValue(key, out var saved))
            return;

        sensor.SensorName = string.IsNullOrWhiteSpace(saved.DisplayName) ? sensor.SensorName : saved.DisplayName;
        sensor.IsEnabled = saved.IsEnabled;
        //sensor.IsPinned = saved.IsPinned;
        sensor.ColorHex = string.IsNullOrWhiteSpace(saved.ColorHex) ? sensor.ColorHex : saved.ColorHex;
        if (saved.DisplayOrder.HasValue)
            sensor.DisplayOrder = saved.DisplayOrder.Value;
        sensor.SelectedColorOption = SensorColorOption.FindByHex(sensor.ColorHex);
    }

    public void ApplyToEnum(EnumStatusReading status)
    {
        var key = MakeKey("enum", status.CanId, status.SignalKey);
        if (!_settings.TryGetValue(key, out var saved))
            return;

        status.StatusName = string.IsNullOrWhiteSpace(saved.DisplayName) ? status.StatusName : saved.DisplayName;
        status.IsEnabled = saved.IsEnabled;
        //status.IsPinned = saved.IsPinned;
        status.ColorHex = string.IsNullOrWhiteSpace(saved.ColorHex) ? status.ColorHex : saved.ColorHex;
        if (saved.DisplayOrder.HasValue)
            status.DisplayOrder = saved.DisplayOrder.Value;
        status.SelectedColorOption = SensorColorOption.FindByHex(status.ColorHex);
    }

    public void SaveSensor(SensorReading sensor)
    {
        var key = MakeKey("sensor", sensor.CanId, sensor.SignalKey);
        Save(key, new SignalDisplaySettings
        {
            DisplayName = sensor.SensorName,
            IsEnabled = sensor.IsEnabled,
            //IsPinned = sensor.IsPinned,
            ColorHex = sensor.ColorHex,
            DisplayOrder = sensor.DisplayOrder
        });
    }

    public void SaveEnum(EnumStatusReading status)
    {
        var key = MakeKey("enum", status.CanId, status.SignalKey);
        Save(key, new SignalDisplaySettings
        {
            DisplayName = status.StatusName,
            IsEnabled = status.IsEnabled,
            //IsPinned = status.IsPinned,
            ColorHex = status.ColorHex,
            DisplayOrder = status.DisplayOrder
        });
    }
    public SignalDisplaySettings? GetSettings(string type, ushort canId, string signalKey)
    {
        var key = MakeKey(type, canId, signalKey);

        lock (_sync)
        {
            return _settings.TryGetValue(key, out var saved)
                ? saved
                : null;
        }
    }
    private void Save(string key, SignalDisplaySettings value)
    {
        lock (_sync)
        {
            _settings[key] = value;
            var json = JsonSerializer.Serialize(_settings, _jsonOptions);
            File.WriteAllText(_filePath, json);
        }
    }

    private void Load()
    {
        lock (_sync)
        {
            if (!File.Exists(_filePath))
                return;

            try
            {
                var json = File.ReadAllText(_filePath);
                _settings = JsonSerializer.Deserialize<Dictionary<string, SignalDisplaySettings>>(json, _jsonOptions)
                    ?? new Dictionary<string, SignalDisplaySettings>(StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                _settings = new Dictionary<string, SignalDisplaySettings>(StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    private static string MakeKey(string type, ushort canId, string signalKey) => $"{type}|{canId:X3}|{signalKey}";
}

public sealed class SignalDisplaySettings
{
    public string DisplayName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    //public bool IsPinned { get; set; }
    public string ColorHex { get; set; } = string.Empty;
    public int? DisplayOrder { get; set; }
}

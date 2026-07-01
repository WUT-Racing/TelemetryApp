using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using TelemetryApp.Backend.Export;
using TelemetryApp.Frontend.Models;

namespace TelemetryApp.Frontend.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private const string ThemePreferenceKey = "AppTheme";
    private const string DriversPreferenceKey = "DriversList";

    private readonly TelemetryExportSettingsStore _exportSettingsStore;
    private bool _isLoadingExportOptions;

    [ObservableProperty]
    private string currentTab = "General";

    [ObservableProperty]
    private string selectedTheme = "Ciemny";

    [ObservableProperty]
    private string newDriverName = string.Empty;

    [ObservableProperty]
    private bool exportLongCsv = true;

    [ObservableProperty]
    private bool exportWideCsv = true;

    [ObservableProperty]
    private bool exportRawFramesCsv = true;

    public ObservableCollection<Driver> Drivers { get; } = new();

    public bool IsLightTheme => SelectedTheme == "Jasny";
    public bool IsDarkTheme => SelectedTheme == "Ciemny";

    public SettingsViewModel(TelemetryExportSettingsStore exportSettingsStore)
    {
        _exportSettingsStore = exportSettingsStore ?? throw new ArgumentNullException(nameof(exportSettingsStore));

        var savedTheme = Preferences.Get(ThemePreferenceKey, "Ciemny");
        ApplyTheme(savedTheme, save: false);

        LoadExportOptions();

        Drivers.CollectionChanged += OnDriversCollectionChanged;

        LoadDriversFromPreferences();
    }

    private void LoadExportOptions()
    {
        _isLoadingExportOptions = true;

        try
        {
            var options = _exportSettingsStore.GetOptions();

            ExportLongCsv = options.ExportLongCsv;
            ExportWideCsv = options.ExportWideCsv;
            ExportRawFramesCsv = options.ExportRawFramesCsv;
        }
        finally
        {
            _isLoadingExportOptions = false;
        }
    }

    partial void OnExportLongCsvChanged(bool value)
    {
        SaveExportOptions();
    }

    partial void OnExportWideCsvChanged(bool value)
    {
        SaveExportOptions();
    }

    partial void OnExportRawFramesCsvChanged(bool value)
    {
        SaveExportOptions();
    }

    private void SaveExportOptions()
    {
        if (_isLoadingExportOptions)
            return;

        _exportSettingsStore.SaveOptions(new TelemetryExportOptions
        {
            ExportLongCsv = ExportLongCsv,
            ExportWideCsv = ExportWideCsv,
            ExportRawFramesCsv = ExportRawFramesCsv
        });
    }

    private void OnDriversCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (Driver driver in e.NewItems)
                driver.PropertyChanged += OnDriverPropertyChanged;
        }

        if (e.OldItems is not null)
        {
            foreach (Driver driver in e.OldItems)
                driver.PropertyChanged -= OnDriverPropertyChanged;
        }
    }

    private void OnDriverPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Driver.Name))
            SaveDriversToPreferences();
    }

    partial void OnSelectedThemeChanged(string value)
    {
        OnPropertyChanged(nameof(IsLightTheme));
        OnPropertyChanged(nameof(IsDarkTheme));
    }

    [RelayCommand]
    private void SelectTab(string tabName)
    {
        CurrentTab = tabName;
    }

    [RelayCommand]
    private void UseLightTheme()
    {
        ApplyTheme("Jasny", save: true);
    }

    [RelayCommand]
    private void UseDarkTheme()
    {
        ApplyTheme("Ciemny", save: true);
    }

    [RelayCommand]
    private void AddDriver()
    {
        var name = NewDriverName?.Trim();

        if (string.IsNullOrWhiteSpace(name))
            return;

        Drivers.Add(new Driver { Name = name });
        NewDriverName = string.Empty;

        SaveDriversToPreferences();
    }

    [RelayCommand]
    private void DeleteDriver(Driver? driver)
    {
        if (driver is null)
            return;

        Drivers.Remove(driver);
        SaveDriversToPreferences();
    }

    private void LoadDriversFromPreferences()
    {
        try
        {
            var json = Preferences.Get(DriversPreferenceKey, string.Empty);

            if (string.IsNullOrWhiteSpace(json))
                return;

            var drivers = JsonSerializer.Deserialize<List<Driver>>(json);

            if (drivers is null)
                return;

            Drivers.Clear();

            foreach (var driver in drivers)
                Drivers.Add(driver);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Błąd wczytywania kierowców: {ex.Message}");
        }
    }

    private void SaveDriversToPreferences()
    {
        try
        {
            var json = JsonSerializer.Serialize(Drivers.ToList());
            Preferences.Set(DriversPreferenceKey, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Błąd zapisu kierowców: {ex.Message}");
        }
    }

    private void ApplyTheme(string themeName, bool save)
    {
        SelectedTheme = themeName == "Jasny" ? "Jasny" : "Ciemny";

        if (Application.Current is not null)
        {
            Application.Current.UserAppTheme = SelectedTheme == "Jasny"
                ? AppTheme.Light
                : AppTheme.Dark;
        }

        if (save)
            Preferences.Set(ThemePreferenceKey, SelectedTheme);
    }

    [ObservableProperty]
    private CanDefinition? _loadedCanConfig;

    [ObservableProperty]
    private string _canConfigFilePath = string.Empty;

    [RelayCommand]
    private async Task LoadCanConfigAsync()
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Wybierz plik sygnaly.json",
            });

            if (result != null)
            {
                CanConfigFilePath = result.FullPath;
                var backendSet = await Backend.Decoding.CanDefinitionLoader.LoadFromFileAsync(result.FullPath);
                
                LoadedCanConfig = MapToFrontendDefinition(backendSet.Source);
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Błąd", $"Nie udało się wczytać pliku:\n{ex.Message}", "OK");
        }
    }

    [RelayCommand]
    private async Task SaveCanConfigAsync()
    {
        if (LoadedCanConfig == null || string.IsNullOrWhiteSpace(CanConfigFilePath))
        {
            await Shell.Current.DisplayAlertAsync("Błąd", "Brak wczytanej konfiguracji lub ścieżki do zapisu.", "OK");
            return;
        }

        try
        {
            var backendFile = MapToBackendDefinition(LoadedCanConfig);
            
            await Backend.Decoding.CanDefinitionSaver.SaveToFileAsync(backendFile, CanConfigFilePath);
            await Shell.Current.DisplayAlertAsync("Sukces", "Pomyślnie zapisano plik CAN.", "OK");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Błąd", $"Nie udało się zapisać pliku:\n{ex.Message}", "OK");
        }
    }

    [RelayCommand]
    private void AddFrame()
    {
        if (LoadedCanConfig == null) return;

        var newFrame = new CanFrame
        {
            Id = 0,
            Summary = "Nowa Ramka",
            FrequencyHz = 10,
            Dlc = 8,
            Endianness = "little"
        };
        LoadedCanConfig.Frames.Add(newFrame);
    }

    [RelayCommand]
    private void DeleteFrame(CanFrame frame)
    {
        if (LoadedCanConfig == null || frame == null) return;
        LoadedCanConfig.Frames.Remove(frame);
    }

    [RelayCommand]
    private void AddSignal(CanFrame frame)
    {
        if (frame == null) return;

        var newSignal = new CanSignal
        {
            Name = "Nowy Sygnał",
            Key = "nowy_sygnal_" + DateTime.Now.Ticks.ToString().Substring(10),
            ByteStart = 0,
            BitStart = 0,
            BitLength = 8,
            DataType = "unsigned",
            Factor = 1,
            Offset = 0,
            Unit = ""
        };
        frame.Signals.Add(newSignal);
    }

    [RelayCommand]
    private void DeleteSignal(CanSignal signal)
    {
        if (LoadedCanConfig == null || signal == null) return;

        foreach (var frame in LoadedCanConfig.Frames)
        {
            if (frame.Signals.Contains(signal))
            {
                frame.Signals.Remove(signal);
                break;
            }
        }
    }

    [RelayCommand]
    private void AddEnumItem(CanSignal signal)
    {
        if (signal == null) return;

        signal.Enum ??= new Dictionary<string, SignalEnumItem>();

        int keyIndex = 0;
        while (signal.Enum.ContainsKey(keyIndex.ToString()))
        {
            keyIndex++;
        }

        signal.Enum[keyIndex.ToString()] = new SignalEnumItem
        {
            IsSimple = true,
            StringValue = "Wartość",
            Label = "Etykieta",
            Description = ""
        };

        var currentEnum = signal.Enum;
        signal.Enum = null;
        signal.Enum = new Dictionary<string, SignalEnumItem>(currentEnum);
    }

    [RelayCommand]
    private void DeleteEnumItem(KeyValuePair<string, SignalEnumItem> pair)
    {
        if (LoadedCanConfig == null) return;

        foreach (var frame in LoadedCanConfig.Frames)
        {
            foreach (var signal in frame.Signals)
            {
                if (signal.Enum != null && signal.Enum.ContainsKey(pair.Key) && signal.Enum[pair.Key] == pair.Value)
                {
                    signal.Enum.Remove(pair.Key);
                    
                    var currentEnum = signal.Enum;
                    signal.Enum = null;
                    signal.Enum = new Dictionary<string, SignalEnumItem>(currentEnum);
                    return;
                }
            }
        }
    }

    private CanDefinition MapToFrontendDefinition(Backend.Decoding.CanDefinitionFile backendDef)
    {
        var frontendDef = new CanDefinition();
        
        if (backendDef.Metadata != null)
        {
            frontendDef.Metadata.Name = backendDef.Metadata.Name;
            frontendDef.Metadata.Version = backendDef.Metadata.Version;
            frontendDef.Metadata.ByteIndexing = backendDef.Metadata.ByteIndexing;
            frontendDef.Metadata.BitIndexing = backendDef.Metadata.BitIndexing;
            frontendDef.Metadata.DefaultDlc = backendDef.Metadata.DefaultDlc;
            frontendDef.Metadata.DefaultOffset = backendDef.Metadata.DefaultOffset;
            frontendDef.Metadata.ValueFormula = backendDef.Metadata.ValueFormula;
        }

        foreach (var bf in backendDef.Frames)
        {
            var ff = new CanFrame
            {
                Id = bf.Id,
                Summary = bf.Summary,
                FrequencyHz = bf.FrequencyHz,
                Dlc = bf.Dlc,
                Endianness = bf.Endianness
            };

            foreach (var bs in bf.Signals)
            {
                var fs = new CanSignal
                {
                    Name = bs.Name,
                    Key = bs.Key,
                    ByteStart = bs.ByteStart,
                    ByteLength = bs.ByteLength,
                    BitStart = bs.BitStart,
                    BitLength = bs.BitLength,
                    Endianness = bs.Endianness,
                    DataType = bs.DataType,
                    RangeMin = bs.RangeMin,
                    RangeMax = bs.RangeMax,
                    RawMin = bs.RawMin,
                    RawMax = bs.RawMax,
                    Factor = bs.Factor,
                    Offset = bs.Offset,
                    Unit = bs.Unit,
                    Reserved = bs.Reserved,
                    Description = bs.Description,
                    SafeMin = bs.SafeMin,
                    SafeMax = bs.SafeMax
                };

                if (bs.Enum != null)
                {
                    fs.Enum = new Dictionary<string, SignalEnumItem>();
                    foreach (var kvp in bs.Enum)
                    {
                        fs.Enum[kvp.Key] = new SignalEnumItem
                        {
                            IsSimple = kvp.Value.IsSimple,
                            StringValue = kvp.Value.StringValue,
                            Key = kvp.Value.Key,
                            Label = kvp.Value.Label,
                            Description = kvp.Value.Description
                        };
                    }
                }
                ff.Signals.Add(fs);
            }
            frontendDef.Frames.Add(ff);
        }
        return frontendDef;
    }

    private Backend.Decoding.CanDefinitionFile MapToBackendDefinition(CanDefinition frontendDef)
    {
        var backendDef = new Backend.Decoding.CanDefinitionFile
        {
            Metadata = new Backend.Decoding.CanDefinitionMetadata
            {
                Name = frontendDef.Metadata.Name,
                Version = frontendDef.Metadata.Version,
                ByteIndexing = frontendDef.Metadata.ByteIndexing,
                BitIndexing = frontendDef.Metadata.BitIndexing,
                DefaultDlc = frontendDef.Metadata.DefaultDlc,
                DefaultOffset = frontendDef.Metadata.DefaultOffset,
                ValueFormula = frontendDef.Metadata.ValueFormula
            }
        };

        foreach (var ff in frontendDef.Frames)
        {
            var bf = new Backend.Decoding.CanFrameDefinition
            {
                Id = ff.Id,
                Summary = ff.Summary,
                FrequencyHz = ff.FrequencyHz,
                Dlc = ff.Dlc,
                Endianness = ff.Endianness
            };

            foreach (var fs in ff.Signals)
            {
                var bs = new Backend.Decoding.CanSignalDefinition
                {
                    Name = fs.Name,
                    Key = fs.Key,
                    ByteStart = fs.ByteStart,
                    ByteLength = fs.ByteLength,
                    BitStart = fs.BitStart,
                    BitLength = fs.BitLength,
                    Endianness = fs.Endianness,
                    DataType = fs.DataType,
                    RangeMin = fs.RangeMin,
                    RangeMax = fs.RangeMax,
                    RawMin = fs.RawMin,
                    RawMax = fs.RawMax,
                    Factor = fs.Factor,
                    Offset = fs.Offset,
                    Unit = fs.Unit,
                    Reserved = fs.Reserved,
                    Description = fs.Description,
                    SafeMin = fs.SafeMin,
                    SafeMax = fs.SafeMax
                };

                if (fs.Enum != null)
                {
                    bs.Enum = new Dictionary<string, Backend.Decoding.CanEnumItemDefinition>();
                    foreach (var kvp in fs.Enum)
                    {
                        bs.Enum[kvp.Key] = new Backend.Decoding.CanEnumItemDefinition
                        {
                            IsSimple = kvp.Value.IsSimple,
                            StringValue = kvp.Value.StringValue,
                            Key = kvp.Value.Key,
                            Label = kvp.Value.Label,
                            Description = kvp.Value.Description
                        };
                    }
                }
                bf.Signals.Add(bs);
            }
            backendDef.Frames.Add(bf);
        }
        return backendDef;
    }
}

public partial class Driver : ObservableObject
{
    [ObservableProperty]
    private string name = string.Empty;
}
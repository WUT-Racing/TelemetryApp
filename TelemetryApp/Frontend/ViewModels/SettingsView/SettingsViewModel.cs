using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using TelemetryApp.Backend.Export;

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
}

public partial class Driver : ObservableObject
{
    [ObservableProperty]
    private string name = string.Empty;
}
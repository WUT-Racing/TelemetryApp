using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;

namespace TelemetryApp.Frontend.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private const string ThemePreferenceKey = "AppTheme";
    private const string DriversPreferenceKey = "DriversList";
    private readonly SessionDetailViewModel _sessionDetailViewModel;

    [ObservableProperty]
    private string currentTab = "General";

    [ObservableProperty]
    private string selectedTheme = "Ciemny";

    [ObservableProperty]
    private string exportStatus = "Nie eksportowano jeszcze danych.";

    [ObservableProperty]
    private bool isExporting;

    [ObservableProperty]
    private bool includeEnumsInExport = true;

    [ObservableProperty]
    private string newDriverName = string.Empty;

    public ObservableCollection<Driver> Drivers { get; } = new();

    public bool IsLightTheme => SelectedTheme == "Jasny";
    public bool IsDarkTheme => SelectedTheme == "Ciemny";

    public SettingsViewModel(SessionDetailViewModel sessionDetailViewModel)
    {
        _sessionDetailViewModel = sessionDetailViewModel ?? throw new ArgumentNullException(nameof(sessionDetailViewModel));

        var savedTheme = Preferences.Get(ThemePreferenceKey, "Ciemny");
        ApplyTheme(savedTheme, save: false);

        Drivers.CollectionChanged += OnDriversCollectionChanged;

        LoadDriversFromPreferences();
    }
    
    private void OnDriversCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (Driver driver in e.NewItems)
                driver.PropertyChanged += OnDriverPropertyChanged;
        }
        if (e.OldItems != null)
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
    private async Task ExportCurrentSessionAsync()
    {
        if (IsExporting)
            return;

        try
        {
            IsExporting = true;
            ExportStatus = "Eksportuję aktualną sesję do CSV...";

            var path = await _sessionDetailViewModel.ExportCurrentSessionToCsvAsync(IncludeEnumsInExport);
            ExportStatus = $"Wyeksportowano folder CSV: {path}";

            await Clipboard.SetTextAsync(path);
            await Application.Current!.MainPage!.DisplayAlert(
                "Eksport zakończony",
                $"Folder eksportu został zapisany tutaj:\n{path}\n\nŚcieżka została skopiowana do schowka.",
                "OK");
        }
        catch (Exception ex)
        {
            ExportStatus = $"Błąd eksportu: {ex.Message}";
            await Application.Current!.MainPage!.DisplayAlert("Błąd eksportu", ex.Message, "OK");
        }
        finally
        {
            IsExporting = false;
        }
    }

    [RelayCommand]
    private void AddDriver()
    {
        string name = NewDriverName?.Trim();
        if (!string.IsNullOrWhiteSpace(name))
        {
            Drivers.Add(new Driver { Name = name });
            NewDriverName = string.Empty;
            SaveDriversToPreferences();
        }
    }

    [RelayCommand]
    private void DeleteDriver(Driver? driver)
    {
        if (driver != null)
        {
            Drivers.Remove(driver);
            SaveDriversToPreferences();
        }
    }

    private void LoadDriversFromPreferences()
    {
        try
        {
            string json = Preferences.Get(DriversPreferenceKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(json))
            {
                var drivers = JsonSerializer.Deserialize<List<Driver>>(json);
                if (drivers != null)
                {
                    Drivers.Clear();
                    foreach (var d in drivers)
                        Drivers.Add(d);
                }
            }
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
            string json = JsonSerializer.Serialize(Drivers.ToList());
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

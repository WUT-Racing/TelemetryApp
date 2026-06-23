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

    public bool IsLightTheme => SelectedTheme == "Jasny";
    public bool IsDarkTheme => SelectedTheme == "Ciemny";

    public SettingsViewModel(SessionDetailViewModel sessionDetailViewModel)
    {
        _sessionDetailViewModel = sessionDetailViewModel ?? throw new ArgumentNullException(nameof(sessionDetailViewModel));

        var savedTheme = Preferences.Get(ThemePreferenceKey, "Ciemny");
        ApplyTheme(savedTheme, save: false);
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

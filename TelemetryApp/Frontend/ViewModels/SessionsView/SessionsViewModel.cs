using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using TelemetryApp.Backend.Sessions;

public partial class SessionViewModel : ObservableObject
{
    private readonly TelemetrySessionManager _sessionManager;

    public ObservableCollection<TelemetrySession> Sessions => _sessionManager.Sessions;

    [ObservableProperty]
    private string statusText = "Lista sesji z AppData/Sessions.";

    public SessionViewModel(TelemetrySessionManager sessionManager)
    {
        _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        _sessionManager.SessionsChanged += () => StatusText = $"Sesje: {Sessions.Count}";
        StatusText = $"Sesje: {Sessions.Count}";
    }

    [RelayCommand]
    private void RefreshSessions()
    {
        _sessionManager.RefreshFromDisk();
        StatusText = $"Odświeżono. Sesje: {Sessions.Count}";
    }

    [RelayCommand]
    private async Task OpenSessionFolder(TelemetrySession session)
    {
        if (session is null || string.IsNullOrWhiteSpace(session.DirectoryPath) || !Directory.Exists(session.DirectoryPath))
        {
            await Application.Current!.MainPage!.DisplayAlert("Sesja", "Folder tej sesji nie istnieje.", "OK");
            return;
        }

        try
        {
#if WINDOWS
            Process.Start(new ProcessStartInfo
            {
                FileName = session.DirectoryPath,
                UseShellExecute = true
            });
#elif MACCATALYST
            var directoryPath = session.DirectoryPath.EndsWith(Path.DirectorySeparatorChar)
                ? session.DirectoryPath
                : session.DirectoryPath + Path.DirectorySeparatorChar;

            await Launcher.Default.OpenAsync(new Uri(directoryPath));
#else
            await Clipboard.Default.SetTextAsync(session.DirectoryPath);
            await Application.Current!.MainPage!.DisplayAlert(
                "Sesja",
                $"Nie umiem automatycznie otworzyć folderu na tej platformie. Skopiowałem ścieżkę do schowka:\n{session.DirectoryPath}",
                "OK");
#endif
        }
        catch (Exception ex)
        {
            await Clipboard.Default.SetTextAsync(session.DirectoryPath);
            await Application.Current!.MainPage!.DisplayAlert(
                "Nie udało się otworzyć folderu",
                $"Skopiowałem ścieżkę do schowka:\n{session.DirectoryPath}\n\nBłąd: {ex.Message}",
                "OK");
        }
    }

    [RelayCommand]
    private async Task GoToConnection()
    {
        await Shell.Current.GoToAsync("//ConnectionPage");
    }
}

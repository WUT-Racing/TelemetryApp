using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using System.Collections.ObjectModel;
using System.Diagnostics;
using TelemetryApp.Backend.BoardConnection;
using TelemetryApp.Backend.Export;
using TelemetryApp.Backend.Sessions;

public partial class SessionViewModel : ObservableObject
{
    private readonly TelemetrySessionManager _sessionManager;
    private readonly TelemetryCsvExportService _exportService;
    private readonly ClientBoard _clientBoard;

    public ObservableCollection<SessionItemViewModel> Sessions { get; } = new();

    [ObservableProperty]
    private string statusText = "Lista sesji z AppData/Sessions.";

    public SessionViewModel(
        TelemetrySessionManager sessionManager,
        TelemetryCsvExportService exportService,
        ClientBoard clientBoard)
    {
        _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        _exportService = exportService ?? throw new ArgumentNullException(nameof(exportService));
        _clientBoard = clientBoard ?? throw new ArgumentNullException(nameof(clientBoard));

        _sessionManager.SessionsChanged += OnSessionsChanged;

        RefreshSessionItems($"Sesje: {_sessionManager.Sessions.Count}");
    }

    private void OnSessionsChanged()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            RefreshSessionItems($"Sesje: {_sessionManager.Sessions.Count}");
        });
    }

    [RelayCommand]
    private void RefreshSessions()
    {
        _sessionManager.RefreshFromDisk();
        RefreshSessionItems($"Odświeżono. Sesje: {_sessionManager.Sessions.Count}");
    }

    [RelayCommand]
    private void ToggleRuns(SessionItemViewModel? item)
    {
        if (item is null || !item.HasRuns)
            return;

        item.IsExpanded = !item.IsExpanded;
    }

    [RelayCommand]
    private async Task OpenSessionFolder(SessionItemViewModel? item)
    {
        if (item is null)
            return;

        var session = item.Session;

        if (string.IsNullOrWhiteSpace(session.DirectoryPath) || !Directory.Exists(session.DirectoryPath))
        {
            await MainPage.DisplayAlert("Sesja", "Folder tej sesji nie istnieje.", "OK");
            return;
        }

        await OpenDirectoryAsync(session.DirectoryPath);
    }

    [RelayCommand]
    private async Task ExportSession(SessionItemViewModel? item)
    {
        if (item is null)
            return;

        var session = item.Session;

        if (session.Runs.Count == 0)
        {
            await MainPage.DisplayAlert("Eksport", "Ta sesja nie ma żadnych runów do eksportu.", "OK");
            return;
        }

        var shouldContinue = await HandleActiveRunBeforeExportAsync(session, session.Runs);
        if (!shouldContinue)
            return;

        try
        {
            StatusText = $"Eksportuję sesję: {session.DisplayName}...";

            var exportDirectory = await _exportService.ExportSessionAsync(
                session,
                includeEnums: true);

            StatusText = $"Wyeksportowano sesję: {session.DisplayName}";

            await OfferOpenExportDirectoryAsync(exportDirectory);
        }
        catch (Exception ex)
        {
            StatusText = "Eksport sesji nie powiódł się.";

            await MainPage.DisplayAlert(
                "Błąd eksportu",
                ex.Message,
                "OK");
        }
    }

    [RelayCommand]
    private async Task ExportRun(SessionRunItemViewModel? item)
    {
        if (item is null)
            return;

        var session = item.Session;
        var run = item.Run;

        var shouldContinue = await HandleActiveRunBeforeExportAsync(session, new[] { run });
        if (!shouldContinue)
            return;

        try
        {
            StatusText = $"Eksportuję run {item.Index}: {run.Source}...";

            var exportDirectory = await _exportService.ExportRunAsync(
                session,
                run,
                includeEnums: true);

            StatusText = $"Wyeksportowano run {item.Index}: {run.Source}";

            await OfferOpenExportDirectoryAsync(exportDirectory);
        }
        catch (Exception ex)
        {
            StatusText = "Eksport runu nie powiódł się.";

            await MainPage.DisplayAlert(
                "Błąd eksportu",
                ex.Message,
                "OK");
        }
    }

    [RelayCommand]
    private async Task GoToConnection()
    {
        await Shell.Current.GoToAsync("//ConnectionPage");
    }

    private async Task<bool> HandleActiveRunBeforeExportAsync(
        TelemetrySession session,
        IReadOnlyList<TelemetrySessionRun> selectedRuns)
    {
        var activeRun = _sessionManager.ActiveRun;

        if (activeRun is null)
            return true;

        var includesActiveRun = selectedRuns.Any(run =>
            string.Equals(run.Id, activeRun.Id, StringComparison.OrdinalIgnoreCase));

        if (!includesActiveRun)
            return true;

        if (!_clientBoard.IsConnected)
            return true;

        var action = await MainPage.DisplayActionSheet(
            "Ten run nadal trwa. Co zrobić?",
            "Anuluj",
            null,
            "Zakończ run i eksportuj",
            "Eksportuj częściowo i kontynuuj");

        if (action == "Zakończ run i eksportuj")
        {
            try
            {
                StatusText = "Zatrzymuję aktualny run...";
                await _clientBoard.DisconnectAsync();

                RefreshSessionItems($"Run zatrzymany. Sesje: {_sessionManager.Sessions.Count}");
                return true;
            }
            catch (Exception ex)
            {
                await MainPage.DisplayAlert(
                    "Nie udało się zatrzymać runu",
                    ex.Message,
                    "OK");

                return false;
            }
        }

        if (action == "Eksportuj częściowo i kontynuuj")
        {
            return true;
        }

        return false;
    }

    private void RefreshSessionItems(string statusText)
    {
        var expandedSessionIds = Sessions
            .Where(item => item.IsExpanded)
            .Select(item => item.Session.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Sessions.Clear();

        foreach (var session in _sessionManager.Sessions)
        {
            var isExpanded = expandedSessionIds.Contains(session.Id);
            Sessions.Add(new SessionItemViewModel(session, isExpanded));
        }

        StatusText = statusText;
    }

    private async Task OfferOpenExportDirectoryAsync(string exportDirectory)
    {
        var open = await MainPage.DisplayAlert(
            "Eksport gotowy",
            $"Pliki zostały zapisane tutaj:\n{exportDirectory}",
            "Otwórz folder",
            "OK");

        if (open)
            await OpenDirectoryAsync(exportDirectory);
    }

    private static async Task OpenDirectoryAsync(string directoryPath)
    {
        try
        {
#if WINDOWS || MACCATALYST
            Process.Start(new ProcessStartInfo
            {
                FileName = directoryPath,
                UseShellExecute = true
            });
#else
            await Clipboard.Default.SetTextAsync(directoryPath);
            await MainPage.DisplayAlert(
                "Folder eksportu",
                $"Nie umiem automatycznie otworzyć folderu na tej platformie. Skopiowałem ścieżkę do schowka:\n{directoryPath}",
                "OK");
#endif
        }
        catch (Exception ex)
        {
            await Clipboard.Default.SetTextAsync(directoryPath);

            await MainPage.DisplayAlert(
                "Nie udało się otworzyć folderu",
                $"Skopiowałem ścieżkę do schowka:\n{directoryPath}\n\nBłąd: {ex.Message}",
                "OK");
        }
    }

    private static Page MainPage =>
        Application.Current?.MainPage
        ?? throw new InvalidOperationException("Brak aktywnej strony aplikacji.");
}

public partial class SessionItemViewModel : ObservableObject
{
    public TelemetrySession Session { get; }

    public ObservableCollection<SessionRunItemViewModel> Runs { get; } = new();

    [ObservableProperty]
    private bool isExpanded;

    public SessionItemViewModel(TelemetrySession session, bool isExpanded = false)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
        this.isExpanded = isExpanded;

        for (var i = Session.Runs.Count - 1; i >= 0; i--)
        {
            Runs.Add(new SessionRunItemViewModel(Session, Session.Runs[i], i + 1));
        }
    }

    public string DisplayName => Session.DisplayName;
    public string TimeText => Session.TimeText;
    public string SourceText => Session.SourceText;
    public string StatusText => Session.StatusText;

    public bool HasRuns => Runs.Count > 0;

    public string RunCountText => Runs.Count switch
    {
        0 => "Brak runów",
        1 => "1 run",
        _ => $"{Runs.Count} runy/runów"
    };

    public string ExpandButtonText
    {
        get
        {
            if (!HasRuns)
                return " ";

            return IsExpanded ? "▾" : "▸";
        }
    }

    public bool IsRunsVisible => HasRuns && IsExpanded;

    partial void OnIsExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(ExpandButtonText));
        OnPropertyChanged(nameof(IsRunsVisible));
    }
}

public sealed class SessionRunItemViewModel
{
    public TelemetrySession Session { get; }
    public TelemetrySessionRun Run { get; }
    public int Index { get; }

    public SessionRunItemViewModel(
        TelemetrySession session,
        TelemetrySessionRun run,
        int index)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
        Run = run ?? throw new ArgumentNullException(nameof(run));
        Index = index;
    }

    public string DisplayName => $"Run {Index}: {Run.Source}";

    public string TimeText
    {
        get
        {
            var start = Run.StartedAtLocal.ToString("yyyy-MM-dd HH:mm:ss");
            var end = Run.StoppedAtLocal is null
                ? "trwa / niedomknięty"
                : Run.StoppedAtLocal.Value.ToString("HH:mm:ss");

            return $"{start} – {end}";
        }
    }

    public string StatusText => Run.StoppedAtLocal.HasValue
        ? "Zakończony"
        : "Trwa / niedomknięty";

    public string RawLogText
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Run.RawLogPath))
                return "Brak ścieżki .rfdlog";

            var fileName = Path.GetFileName(Run.RawLogPath);

            if (!File.Exists(Run.RawLogPath))
                return $"{fileName} — brak pliku";

            var bytes = new FileInfo(Run.RawLogPath).Length;
            return $"{fileName} — {FormatBytes(bytes)}";
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";

        if (bytes < 1024 * 1024)
            return $"{bytes / 1024.0:0.0} KB";

        if (bytes < 1024L * 1024L * 1024L)
            return $"{bytes / 1024.0 / 1024.0:0.0} MB";

        return $"{bytes / 1024.0 / 1024.0 / 1024.0:0.0} GB";
    }
}
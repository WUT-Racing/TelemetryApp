using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TelemetryApp.Backend.BoardConnection;
using TelemetryApp.Backend.Decoding;
using TelemetryApp.Backend.Pipeline;
using TelemetryApp.Backend.Sessions;
using TelemetryApp.Frontend.ViewModels;

public partial class ConnectionPageViewModel : ObservableObject
{
    private readonly ClientBoard _clientBoard;
    private readonly TelemetrySessionManager _sessionManager;
    private readonly SettingsViewModel _settingsViewModel;

    [ObservableProperty]
    private string clientText = string.Empty;

    [ObservableProperty]
    private string serverConsoleText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<string> availablePorts = new();

    [ObservableProperty]
    private string? selectedPort;

    [ObservableProperty]
    private string baudRate = "460800";

    [ObservableProperty]
    private bool isConnected;

    [ObservableProperty]
    private string connectionButtonText = "Connect";

    [ObservableProperty]
    private string simulatorButtonText = "Start simulator";

    [ObservableProperty]
    private string sessionName = TelemetrySessionManager.CreateDefaultSessionName();

    [ObservableProperty]
    private string activeSessionText = "Brak aktywnej sesji.";

    [ObservableProperty]
    private bool hasActiveSession;

    [ObservableProperty]
    private Driver? selectedDriver;

    public ObservableCollection<Driver> Drivers => _settingsViewModel.Drivers;

    public ConnectionPageViewModel(ClientBoard clientBoard, TelemetrySessionManager sessionManager, SettingsViewModel settingsViewModel)
    {
        _clientBoard = clientBoard ?? throw new ArgumentNullException(nameof(clientBoard));
        _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        _settingsViewModel = settingsViewModel ?? throw new ArgumentNullException(nameof(settingsViewModel));

        _clientBoard.OnStatusChanged += HandleStatusChanged;
        _clientBoard.OnStatsChanged += HandleStatsChanged;
        _clientBoard.OnSignalsDecoded += HandleSignalsDecoded;
        _clientBoard.OnError += HandleError;
        _sessionManager.ActiveSessionChanged += _ => RefreshSessionStatus();
        _sessionManager.SessionsChanged += RefreshSessionStatus;

        ScanPorts();
        RefreshSessionStatus();
    }

    [RelayCommand]
    public void UseDefaultSessionName()
    {
        SessionName = TelemetrySessionManager.CreateDefaultSessionName();
    }

    [RelayCommand]
    public async Task StartSession()
    {
        if (IsConnected)
        {
            await ShowAlertAsync("Sesja", "Najpierw zatrzymaj aktualne połączenie.");
            return;
        }

        if (_sessionManager.ActiveSession is { IsEnded: false })
        {
            var action = await Application.Current!.MainPage!.DisplayActionSheet(
                "Aktywna sesja już istnieje",
                "Anuluj",
                null,
                "Kontynuuj aktualną",
                "Zakończ i rozpocznij nową");

            if (action == "Kontynuuj aktualną")
                return;

            if (action != "Zakończ i rozpocznij nową")
                return;

            _sessionManager.EndActiveSession();
        }

        StartSessionInternal(isTest: false);
    }

    [RelayCommand]
    public async Task EndSession()
    {
        if (IsConnected)
        {
            await ShowAlertAsync("Sesja", "Najpierw zatrzymaj połączenie/symulator, potem zakończ sesję.");
            return;
        }

        if (_sessionManager.ActiveSession is null)
        {
            await ShowAlertAsync("Sesja", "Nie ma aktywnej sesji do zakończenia.");
            return;
        }

        var confirm = await Application.Current!.MainPage!.DisplayAlert(
            "Zakończyć sesję?",
            $"Sesja: {_sessionManager.ActiveSession.DisplayName}",
            "Zakończ",
            "Anuluj");

        if (!confirm)
            return;

        _sessionManager.EndActiveSession();
        AppendText("Sesja zakończona.");
    }

    [RelayCommand]
    public async Task Connect()
    {
        try
        {
            if (IsConnected)
            {
                await DisconnectInternalAsync();
                return;
            }

            if (!await EnsureSessionBeforeConnectionAsync(isSimulator: false))
                return;

            if (string.IsNullOrWhiteSpace(SelectedPort))
            {
                AppendText("Wybierz port przed połączeniem albo użyj symulatora.");
                return;
            }

            if (!int.TryParse(BaudRate, out var baudRateValue))
            {
                AppendText($"Niepoprawny baud rate: {BaudRate}");
                return;
            }

            await _clientBoard.ConnectAsync(SelectedPort, baudRateValue);
            SetConnectedUiState(true);
        }
        catch (Exception ex)
        {
            AppendText($"Błąd połączenia: {ex.Message}");
            SetConnectedUiState(false);
        }
    }

    [RelayCommand]
    public async Task StartSimulator()
    {
        try
        {
            if (IsConnected)
            {
                await DisconnectInternalAsync();
                return;
            }

            if (!await EnsureSessionBeforeConnectionAsync(isSimulator: true))
                return;

            await _clientBoard.ConnectSimulatorAsync();
            SetConnectedUiState(true);
        }
        catch (Exception ex)
        {
            AppendText($"Błąd symulatora: {ex.Message}");
            SetConnectedUiState(false);
        }
    }

    [RelayCommand]
    public void ScanPorts()
    {
        var ports = ClientBoard.GetAvailablePorts();
        AvailablePorts = new ObservableCollection<string>(ports);

        if (SelectedPort is null && AvailablePorts.Count > 0)
            SelectedPort = AvailablePorts[0];

        AppendText(ports.Length == 0
            ? "Nie znaleziono portów serial. Możesz użyć trybu symulatora."
            : $"Znaleziono porty: {string.Join(", ", ports)}");
    }

    [RelayCommand]
    public async Task GoToDashboard()
    {
        await Shell.Current.GoToAsync("//Dashboard/SessionDetailPage");
    }

    private async Task<bool> EnsureSessionBeforeConnectionAsync(bool isSimulator)
    {
        var active = _sessionManager.ActiveSession;

        if (active is null || active.IsEnded)
        {
            var create = await Application.Current!.MainPage!.DisplayAlert(
                "Brak aktywnej sesji",
                "Przed połączeniem trzeba rozpocząć sesję. Utworzyć teraz?",
                "Utwórz",
                "Anuluj");

            if (!create)
                return false;

            StartSessionInternal(isTest: isSimulator);
            return true;
        }

        if (active.HasCompletedRun)
        {
            var action = await Application.Current!.MainPage!.DisplayActionSheet(
                "Nowe połączenie",
                "Anuluj",
                null,
                "Kontynuuj aktualną sesję",
                "Zakończ aktualną i rozpocznij nową");

            if (action == "Kontynuuj aktualną sesję")
                return true;

            if (action != "Zakończ aktualną i rozpocznij nową")
                return false;

            _sessionManager.EndActiveSession();
            StartSessionInternal(isTest: isSimulator);
            return true;
        }

        return true;
    }

    private void StartSessionInternal(bool isTest)
    {
        var session = _sessionManager.StartNewSession(SessionName, isTest, SelectedDriver?.Name);
        SessionName = TelemetrySessionManager.CreateDefaultSessionName();
        AppendText($"Rozpoczęto sesję: {session.DisplayName}");
        RefreshSessionStatus();
    }

    private async Task DisconnectInternalAsync()
    {
        await _clientBoard.DisconnectAsync();
        SetConnectedUiState(false);
        RefreshSessionStatus();
    }

    private void SetConnectedUiState(bool connected)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            IsConnected = connected;
            ConnectionButtonText = connected ? "Disconnect" : "Connect";
            SimulatorButtonText = connected ? "Stop" : "Start simulator";
        });
    }

    private void RefreshSessionStatus()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var session = _sessionManager.ActiveSession;
            HasActiveSession = session is { IsEnded: false };
            ActiveSessionText = session is null
                ? "Brak aktywnej sesji. Kliknij Start session i podaj nazwę."
                : $"Aktywna sesja: {session.DisplayName} | start: {session.StartedAtLocal:yyyy-MM-dd HH:mm} | połączenia: {session.Runs.Count}";
        });
    }

    private void HandleStatusChanged(string status)
    {
        AppendText(status);
        RefreshSessionStatus();
    }

    private void HandleStatsChanged(TelemetryPipelineStats stats)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            ClientText =
                $"{ActiveSessionText}\n" +
                $"Status: {(IsConnected ? "Connected" : "Disconnected")}\n" +
                $"Bytes received: {stats.BytesReceived}\n" +
                $"Blocks received: {stats.BlocksReceived}\n" +
                $"Records received: {stats.RecordsReceived}\n" +
                $"Signals decoded: {stats.SignalsDecoded}\n" +
                $"CRC errors: {stats.CrcErrors}\n" +
                $"Parse errors: {stats.ParseErrors}\n" +
                $"Lost blocks: {stats.LostBlocks}\n" +
                $"Last sequence: {stats.LastSequence}\n" +
                $"Last timestamp: {stats.LastTimestamp}\n" +
                $"Raw log: {stats.LogFilePath}\n";
        });
    }

    private void HandleSignalsDecoded(DecodedSignalBatch batch)
    {
        // Dashboard dostaje dane przez singleton ClientBoard/SessionDetailViewModel.
    }

    private void HandleError(Exception ex)
    {
        AppendText($"Pipeline error: {ex.Message}");
        SetConnectedUiState(false);
    }

    private void AppendText(string text)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (string.IsNullOrWhiteSpace(ClientText))
                ClientText = text;
            else
                ClientText += Environment.NewLine + text;
        });
    }

    private static Task ShowAlertAsync(string title, string message)
    {
        return Application.Current!.MainPage!.DisplayAlert(title, message, "OK");
    }
}

using System.ComponentModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

public partial class SessionViewModel : ObservableObject
{
    private readonly TelemetryService _telemetryService;

    public ObservableCollection<TestSession> Sessions { get; } = new();

    public SessionViewModel(TelemetryService telemetryService)
    {
        _telemetryService = telemetryService ?? throw new System.ArgumentNullException(nameof(telemetryService));
    }

    [RelayCommand]
    private void LoadSession()
    {
    }

    [RelayCommand]
    private void OpenSession(TestSession session)
    {
        // TODO: nawigacja / otwarcie pliku DB powiązanego z sesją
        // przykładowo: Shell.Current.GoToAsync($"sessiondetails?sessionId={session.Id}");
    }
}
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

    public void LoadSession(string fileToChange)
    {
        _telemetryService.ChangeDatabase(fileToChange);
    }
    private void OpenSession(TestSession session)
    {
        // TODO: nawigacja / otwarcie pliku DB powiązanego z sesją
        // przykładowo: Shell.Current.GoToAsync($"sessiondetails?sessionId={session.Id}");
    }

    [RelayCommand]
    private async Task LoadSessionFromPicker()
    {
        FileResult result = null;

        var sessionFileType = new FilePickerFileType(
            new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                {DevicePlatform.WinUI, new[] {".db"}}
            }
        );

        var options = new PickOptions
        {
            PickerTitle = "Wybierz plik DB",
            FileTypes = sessionFileType,
        };

        try
        {
            
            if (DeviceInfo.Current.Platform == DevicePlatform.WinUI)
            {
                result = await FilePicker.Default.PickAsync(options);
            }
            else
            {
                result = await FilePicker.Default.PickAsync();
            }

            if (result == null)
                return;
            var newDatabasePath = result.FullPath;

            LoadSession(newDatabasePath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Błąd FilePicker: {ex.Message}");
        }
    }
}
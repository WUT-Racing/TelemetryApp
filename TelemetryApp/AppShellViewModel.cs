using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public partial class AppShellViewModel : ObservableObject
{
    public AppShellViewModel()
    {
    }

    [RelayCommand]
    private async Task NavigateToSettings()
    {
        await Shell.Current.GoToAsync("//settingsRoute");
        Shell.Current.FlyoutIsPresented = false;
    }
}
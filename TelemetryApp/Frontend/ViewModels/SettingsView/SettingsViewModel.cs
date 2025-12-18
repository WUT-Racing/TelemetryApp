using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public partial class SettingsViewModel : ObservableObject
{
    public SettingsViewModel(){}

    [ObservableProperty]
    private string _currentTab = "General";

    [RelayCommand]
    private void SelectTab(string tabName)
    {
        CurrentTab = tabName;
    }

}
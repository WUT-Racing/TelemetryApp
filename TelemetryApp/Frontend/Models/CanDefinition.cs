using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TelemetryApp.Frontend.Models;

public partial class CanDefinition : ObservableObject
{
    [ObservableProperty]
    private Metadata _metadata = new();

    [ObservableProperty]
    private ObservableCollection<CanFrame> _frames = new();
}

public partial class Metadata : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _version = string.Empty;
}
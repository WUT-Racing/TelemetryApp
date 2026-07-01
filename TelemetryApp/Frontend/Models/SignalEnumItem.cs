using CommunityToolkit.Mvvm.ComponentModel;

namespace TelemetryApp.Frontend.Models;

public partial class SignalEnumItem : ObservableObject
{
    [ObservableProperty]
    private bool _isSimple;

    [ObservableProperty]
    private string? _stringValue;

    [ObservableProperty]
    private string? _key;

    [ObservableProperty]
    private string? _label;

    [ObservableProperty]
    private string? _description;
}
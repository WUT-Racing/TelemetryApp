using CommunityToolkit.Mvvm.ComponentModel;

namespace TelemetryApp.Frontend.Models;

public partial class SignalEnumItem : ObservableObject
{
    [ObservableProperty]
    private string? _id;

    [ObservableProperty]
    private bool _isComplex;

    [ObservableProperty]
    private string? _value;

    [ObservableProperty]
    private string? _key;

    [ObservableProperty]
    private string? _label;

    [ObservableProperty]
    private string? _description;
}
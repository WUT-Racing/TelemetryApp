using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TelemetryApp.Frontend.Models;

public partial class CanSignal : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _key = string.Empty;

    [ObservableProperty]
    private int _byteStart;

    [ObservableProperty]
    private int _bitStart;

    [ObservableProperty]
    private int _bitLength;

    [ObservableProperty]
    private double _factor = 1.0;

    [ObservableProperty]
    private double _offset = 0.0;

    [ObservableProperty]
    private string _unit = string.Empty;

    [ObservableProperty]
    private Dictionary<string, SignalEnumItem>? _enum;
}
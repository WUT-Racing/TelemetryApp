using System.Collections.ObjectModel;
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
    private int _byteLength;

    [ObservableProperty]
    private int _bitStart;

    [ObservableProperty]
    private int _bitLength;

    [ObservableProperty]
    private string? _endianness;

    [ObservableProperty]
    private string _dataType = "unsigned";

    [ObservableProperty]
    private double? _rangeMin;

    [ObservableProperty]
    private double? _rangeMax;

    [ObservableProperty]
    private double? _rawMin;

    [ObservableProperty]
    private double? _rawMax;

    [ObservableProperty]
    private double _factor = 1.0;

    [ObservableProperty]
    private double _offset = 0.0;

    [ObservableProperty]
    private string? _unit;

    [ObservableProperty]
    private ObservableCollection<SignalEnumItem>? _enum;

    [ObservableProperty]
    private bool _reserved;

    [ObservableProperty]
    private string? _description;

    [ObservableProperty]
    private double? _safeMin;

    [ObservableProperty]
    private double? _safeMax;
}
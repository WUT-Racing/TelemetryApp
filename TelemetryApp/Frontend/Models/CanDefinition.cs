using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TelemetryApp.Frontend.Models;

public partial class CanDefinition : ObservableObject
{
    [ObservableProperty]
    private CanDefinitionMetadata _metadata = new();

    [ObservableProperty]
    private ObservableCollection<CanFrame> _frames = new();
}

public partial class CanDefinitionMetadata : ObservableObject
{
    [ObservableProperty]
    private string? _name;

    [ObservableProperty]
    private string? _version;

    [ObservableProperty]
    private int _byteIndexing;

    [ObservableProperty]
    private int _bitIndexing;

    [ObservableProperty]
    private int _defaultDlc = 8;

    [ObservableProperty]
    private double _defaultOffset;

    [ObservableProperty]
    private string? _valueFormula;
}
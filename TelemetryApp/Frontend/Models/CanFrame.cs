using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TelemetryApp.Frontend.Models;

public partial class CanFrame : ObservableObject
{
    [ObservableProperty]
    private int _id;

    [ObservableProperty]
    private string _summary = string.Empty;

    [ObservableProperty]
    private int _frequencyHz;

    [ObservableProperty]
    private int _dlc;

    [ObservableProperty]
    private string _endianness = "little";

    [ObservableProperty]
    private ObservableCollection<CanSignal> _signals = new();
}
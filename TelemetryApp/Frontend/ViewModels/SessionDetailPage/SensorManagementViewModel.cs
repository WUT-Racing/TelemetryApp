using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TelemetryApp.Frontend.Data;

namespace TelemetryApp.Frontend.ViewModels;

public partial class SensorManagementViewModel : ObservableObject
{
    private readonly IReadOnlyList<SensorReading> _allSensors;

    public ObservableCollection<SensorReading> VisibleSensors { get; } = new();
    public ObservableCollection<SensorColorOption> ColorOptions { get; } = new(SensorColorOption.Defaults);

    [ObservableProperty]
    private string searchText = string.Empty;

    public SensorManagementViewModel(IReadOnlyList<SensorReading> sensors)
    {
        _allSensors = sensors;
        Refresh();
    }

    partial void OnSearchTextChanged(string value) => Refresh();

    [RelayCommand]
    public void ClearSearch()
    {
        SearchText = string.Empty;
    }

    [RelayCommand]
    public void TogglePin(SensorReading sensor)
    {
        if (sensor is null)
            return;

        sensor.IsPinned = !sensor.IsPinned;
        Refresh();
    }

    [RelayCommand]
    public void EnableAll()
    {
        foreach (var sensor in _allSensors)
            sensor.IsEnabled = true;

        Refresh();
    }

    [RelayCommand]
    public void DisableAll()
    {
        foreach (var sensor in _allSensors)
            sensor.IsEnabled = false;

        Refresh();
    }

    [RelayCommand]
    public void Refresh()
    {
        var query = SearchText?.Trim();

        var result = _allSensors
            .Where(sensor => string.IsNullOrWhiteSpace(query) || sensor.SearchText.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(sensor => sensor.IsPinned)
            .ThenBy(sensor => sensor.SensorName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(sensor => sensor.CanId)
            .ThenBy(sensor => sensor.SignalKey, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        VisibleSensors.Clear();
        for (var i = 0; i < result.Count; i++)
        {
            result[i].RowIndex = i;
            VisibleSensors.Add(result[i]);
        }
    }
}

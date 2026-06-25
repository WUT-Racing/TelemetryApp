using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TelemetryApp.Frontend.Data;

namespace TelemetryApp.Frontend.ViewModels;

public partial class SensorManagementViewModel : ObservableObject
{
    private readonly IReadOnlyList<SensorReading> _allSensors;

    public ObservableCollection<SensorReading> VisibleSensors { get; } = new();

    public ObservableCollection<SensorColorOption> ColorOptions { get; } =
        new(SensorColorOption.Defaults);

    [ObservableProperty]
    private string searchText = string.Empty;

    public SensorManagementViewModel(IReadOnlyList<SensorReading> sensors)
    {
        _allSensors = sensors;

        EnsureInitialOrder();
        Refresh();
    }

    partial void OnSearchTextChanged(string value)
    {
        Refresh();
    }

    [RelayCommand]
    public void ClearSearch()
    {
        SearchText = string.Empty;
    }

    [RelayCommand]
    public void EnableAll()
    {
        foreach (var sensor in _allSensors)
        {
            sensor.IsEnabled = true;
        }

        Refresh();
    }

    [RelayCommand]
    public void DisableAll()
    {
        foreach (var sensor in _allSensors)
        {
            sensor.IsEnabled = false;
        }

        Refresh();
    }

    [RelayCommand]
    public void ResetDefaults()
    {
        var defaultOrderedSensors = _allSensors
            .OrderBy(sensor => sensor.SensorId)
            .ThenBy(sensor => sensor.CanId)
            .ThenBy(sensor => sensor.SignalKey, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        for (var i = 0; i < defaultOrderedSensors.Count; i++)
        {
            var sensor = defaultOrderedSensors[i];

            sensor.DisplayOrder = i;

            if (!string.IsNullOrWhiteSpace(sensor.OriginalSignalName))
            {
                sensor.SensorName = sensor.OriginalSignalName;
            }
        }

        Refresh();
    }

    [RelayCommand]
    public void Refresh()
    {
        var query = SearchText?.Trim();

        var result = _allSensors
            .Where(sensor =>
                string.IsNullOrWhiteSpace(query) ||
                sensor.SearchText.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderBy(sensor => sensor.DisplayOrder)
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

    public void MoveSensorToVisibleIndex(SensorReading draggedSensor, int targetInsertIndex)
    {
        var oldVisibleIndex = VisibleSensors.IndexOf(draggedSensor);

        if (oldVisibleIndex < 0)
            return;

        var count = VisibleSensors.Count;

        if (count <= 1)
            return;

        targetInsertIndex = Math.Clamp(targetInsertIndex, 0, count);

        var newVisibleIndex = targetInsertIndex;

        if (oldVisibleIndex < targetInsertIndex)
            newVisibleIndex--;

        newVisibleIndex = Math.Clamp(newVisibleIndex, 0, count - 1);

        if (oldVisibleIndex == newVisibleIndex)
            return;

        VisibleSensors.Move(oldVisibleIndex, newVisibleIndex);

        ApplyVisibleSensorOrderWithoutRefresh();
    }

    private void ApplyVisibleSensorOrderWithoutRefresh()
    {
        var reorderedVisibleSensors = VisibleSensors.ToList();
        var visibleSet = new HashSet<SensorReading>(reorderedVisibleSensors);

        var orderedAll = _allSensors
            .OrderBy(sensor => sensor.DisplayOrder)
            .ThenBy(sensor => sensor.SensorName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(sensor => sensor.CanId)
            .ThenBy(sensor => sensor.SignalKey, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var visiblePositions = orderedAll
            .Select((sensor, index) => new { sensor, index })
            .Where(x => visibleSet.Contains(x.sensor))
            .Select(x => x.index)
            .ToList();

        for (var i = 0; i < visiblePositions.Count && i < reorderedVisibleSensors.Count; i++)
        {
            orderedAll[visiblePositions[i]] = reorderedVisibleSensors[i];
        }

        for (var i = 0; i < orderedAll.Count; i++)
        {
            orderedAll[i].DisplayOrder = i;
        }

        for (var i = 0; i < VisibleSensors.Count; i++)
        {
            VisibleSensors[i].RowIndex = i;
        }
    }

    private void EnsureInitialOrder()
    {
        if (_allSensors.Count == 0)
            return;

        var distinctOrders = _allSensors
            .Select(sensor => sensor.DisplayOrder)
            .Distinct()
            .Count();

        var hasValidOrder = distinctOrders == _allSensors.Count;

        if (hasValidOrder)
            return;

        var defaultOrderedSensors = _allSensors
            .OrderBy(sensor => sensor.SensorId)
            .ThenBy(sensor => sensor.CanId)
            .ThenBy(sensor => sensor.SignalKey, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        for (var i = 0; i < defaultOrderedSensors.Count; i++)
        {
            defaultOrderedSensors[i].DisplayOrder = i;
        }
    }
}
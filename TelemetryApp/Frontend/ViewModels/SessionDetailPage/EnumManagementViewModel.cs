using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TelemetryApp.Frontend.Data;

namespace TelemetryApp.Frontend.ViewModels;

public partial class EnumManagementViewModel : ObservableObject
{
    private readonly IReadOnlyList<EnumStatusReading> _allStatuses;

    public ObservableCollection<EnumStatusReading> VisibleStatuses { get; } = new();

    public ObservableCollection<SensorColorOption> ColorOptions { get; } =
        new(SensorColorOption.Defaults);

    [ObservableProperty]
    private string searchText = string.Empty;

    public EnumManagementViewModel(IReadOnlyList<EnumStatusReading> statuses)
    {
        _allStatuses = statuses;

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
        foreach (var status in _allStatuses)
        {
            status.IsEnabled = true;
        }

        Refresh();
    }

    [RelayCommand]
    public void DisableAll()
    {
        foreach (var status in _allStatuses)
        {
            status.IsEnabled = false;
        }

        Refresh();
    }

    [RelayCommand]
    public void ResetDefaults()
    {
        var defaultOrderedStatuses = _allStatuses
            .OrderBy(status => status.OriginalSignalName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(status => status.CanId)
            .ThenBy(status => status.SignalKey, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        for (var i = 0; i < defaultOrderedStatuses.Count; i++)
        {
            var status = defaultOrderedStatuses[i];

            status.DisplayOrder = i;

            if (!string.IsNullOrWhiteSpace(status.OriginalSignalName))
            {
                status.StatusName = status.OriginalSignalName;
            }
        }

        Refresh();
    }

    [RelayCommand]
    public void Refresh()
    {
        var query = SearchText?.Trim();

        var result = _allStatuses
            .Where(status =>
                string.IsNullOrWhiteSpace(query) ||
                status.SearchText.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderBy(status => status.DisplayOrder)
            .ThenBy(status => status.StatusName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(status => status.CanId)
            .ThenBy(status => status.SignalKey, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        VisibleStatuses.Clear();

        for (var i = 0; i < result.Count; i++)
        {
            result[i].RowIndex = i;
            VisibleStatuses.Add(result[i]);
        }
    }

    public void MoveStatusToVisibleIndex(EnumStatusReading draggedStatus, int targetInsertIndex)
    {
        var oldVisibleIndex = VisibleStatuses.IndexOf(draggedStatus);

        if (oldVisibleIndex < 0)
            return;

        var count = VisibleStatuses.Count;

        if (count <= 1)
            return;

        targetInsertIndex = Math.Clamp(targetInsertIndex, 0, count);

        var newVisibleIndex = targetInsertIndex;

        if (oldVisibleIndex < targetInsertIndex)
            newVisibleIndex--;

        newVisibleIndex = Math.Clamp(newVisibleIndex, 0, count - 1);

        if (oldVisibleIndex == newVisibleIndex)
            return;

        VisibleStatuses.Move(oldVisibleIndex, newVisibleIndex);

        ApplyVisibleStatusOrderWithoutRefresh();
    }

    private void ApplyVisibleStatusOrderWithoutRefresh()
    {
        var reorderedVisibleStatuses = VisibleStatuses.ToList();
        var visibleSet = new HashSet<EnumStatusReading>(reorderedVisibleStatuses);

        var orderedAll = _allStatuses
            .OrderBy(status => status.DisplayOrder)
            .ThenBy(status => status.StatusName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(status => status.CanId)
            .ThenBy(status => status.SignalKey, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var visiblePositions = orderedAll
            .Select((status, index) => new { status, index })
            .Where(x => visibleSet.Contains(x.status))
            .Select(x => x.index)
            .ToList();

        for (var i = 0; i < visiblePositions.Count && i < reorderedVisibleStatuses.Count; i++)
        {
            orderedAll[visiblePositions[i]] = reorderedVisibleStatuses[i];
        }

        for (var i = 0; i < orderedAll.Count; i++)
        {
            orderedAll[i].DisplayOrder = i;
        }

        for (var i = 0; i < VisibleStatuses.Count; i++)
        {
            VisibleStatuses[i].RowIndex = i;
        }
    }

    private void EnsureInitialOrder()
    {
        if (_allStatuses.Count == 0)
            return;

        var distinctOrders = _allStatuses
            .Select(status => status.DisplayOrder)
            .Distinct()
            .Count();

        var hasValidOrder = distinctOrders == _allStatuses.Count;

        if (hasValidOrder)
            return;

        var defaultOrderedStatuses = _allStatuses
            .OrderBy(status => status.OriginalSignalName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(status => status.CanId)
            .ThenBy(status => status.SignalKey, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        for (var i = 0; i < defaultOrderedStatuses.Count; i++)
        {
            defaultOrderedStatuses[i].DisplayOrder = i;
        }
    }
}
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TelemetryApp.Frontend.Data;

namespace TelemetryApp.Frontend.ViewModels;

public partial class EnumManagementViewModel : ObservableObject
{
    private readonly IReadOnlyList<EnumStatusReading> _allStatuses;

    public ObservableCollection<EnumStatusReading> VisibleStatuses { get; } = new();
    public ObservableCollection<SensorColorOption> ColorOptions { get; } = new(SensorColorOption.Defaults);

    [ObservableProperty]
    private string searchText = string.Empty;

    public EnumManagementViewModel(IReadOnlyList<EnumStatusReading> statuses)
    {
        _allStatuses = statuses;
        Refresh();
    }

    partial void OnSearchTextChanged(string value) => Refresh();

    [RelayCommand]
    public void ClearSearch()
    {
        SearchText = string.Empty;
    }

    [RelayCommand]
    public void TogglePin(EnumStatusReading status)
    {
        if (status is null)
            return;

        status.IsPinned = !status.IsPinned;
        Refresh();
    }

    [RelayCommand]
    public void EnableAll()
    {
        foreach (var status in _allStatuses)
            status.IsEnabled = true;

        Refresh();
    }

    [RelayCommand]
    public void DisableAll()
    {
        foreach (var status in _allStatuses)
            status.IsEnabled = false;

        Refresh();
    }

    [RelayCommand]
    public void Refresh()
    {
        var query = SearchText?.Trim();

        var result = _allStatuses
            .Where(status => string.IsNullOrWhiteSpace(query) || status.SearchText.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(status => status.IsPinned)
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
}

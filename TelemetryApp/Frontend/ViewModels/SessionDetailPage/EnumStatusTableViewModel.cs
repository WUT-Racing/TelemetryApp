using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TelemetryApp.Frontend.Data;

namespace TelemetryApp.Frontend.ViewModels;

public partial class EnumStatusTableViewModel : ObservableObject
{
    private readonly IReadOnlyList<EnumStatusReading> _allStatuses;

    public ObservableCollection<EnumStatusReading> VisibleStatuses { get; } = new();

    [ObservableProperty]
    private string searchText = string.Empty;

    public EnumStatusTableViewModel(IReadOnlyList<EnumStatusReading> statuses)
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

    public void Refresh()
    {
        var query = SearchText?.Trim();

        var result = _allStatuses
            .Where(status => status.IsEnabled)
            .Where(status => string.IsNullOrWhiteSpace(query) || status.SearchText.Contains(query, StringComparison.OrdinalIgnoreCase))
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
}

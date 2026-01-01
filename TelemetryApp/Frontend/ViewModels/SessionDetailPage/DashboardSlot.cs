using CommunityToolkit.Mvvm.ComponentModel;

public partial class DashboardSlot: ObservableObject
{
    [ObservableProperty]
    private IView _currentWidget;

    public string SlotId { get; set; } = Guid.NewGuid().ToString();
}
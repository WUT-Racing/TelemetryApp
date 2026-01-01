using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public partial class SessionDetailViewModel : ObservableObject
{
    public SessionDetailViewModel()
    {
        AddSlot();
    }

    [ObservableProperty]
    private string _draggedType;

    [ObservableProperty]
    private int _gridSpan = 2;

    public ObservableCollection<DashboardSlot> DashboardSlots { get; } = new();

    [RelayCommand]
    private void AddSlot() => DashboardSlots.Add(new DashboardSlot());

    [RelayCommand]
    private void RemoveSlot(DashboardSlot slot)
    {
        if (slot != null) DashboardSlots.Remove(slot);
    }

    [RelayCommand]
    private void DragStart(string type)
    {
        DraggedType = type;
    }

    [RelayCommand]
    private async Task ItemDropped(DashboardSlot targetSlot)
    {
        if (string.IsNullOrEmpty(DraggedType) || targetSlot == null)
        {
            return;
        }

        string selection = await Shell.Current.DisplayActionSheetAsync("Wybierz dane", "Anuluj", null, "Prędkość", "Obroty", "Temperatura Oleju");
        if (selection == "Anuluj" || selection == null) return;

        switch (DraggedType)
        {
            case "Single":

                targetSlot.CurrentWidget = CreateSingleValueWidget(selection);
                break;
            case "Chart":
                targetSlot.CurrentWidget = CreateChartWidget(selection);
                break;

        }
        DraggedType = null;
    }

    private IView CreateSingleValueWidget(string dataName) => new VerticalStackLayout
    {
        VerticalOptions = LayoutOptions.Center,
        Children = {
        new Label { Text = dataName, HorizontalOptions = LayoutOptions.Center, FontSize = 12, TextColor = Color.FromArgb("#ACACAC") },
        new Label { Text = "0.0", FontAttributes = FontAttributes.Bold, HorizontalOptions = LayoutOptions.Center, FontSize = 24, TextColor = Colors.White }
    }
    };

    private IView CreateChartWidget(string dataName) => new VerticalStackLayout
    {
        Spacing = 5,
        Children = {
        new Label { Text = "WYKRES: " + dataName, HorizontalOptions = LayoutOptions.Center, FontSize = 10, TextColor = Color.FromArgb("#ACACAC") },
        new BoxView { HeightRequest = 60, WidthRequest = 100, Color = Color.FromArgb("#FFC00000"), HorizontalOptions = LayoutOptions.Center }
    }
    };


}
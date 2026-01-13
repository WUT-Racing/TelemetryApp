using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public partial class SessionDetailViewModel : ObservableObject
{

    public ObservableCollection<SinglePlotViewModel> VisibleCharts { get; set; }
    public SessionDetailViewModel()
    {
        VisibleCharts = new ObservableCollection<SinglePlotViewModel>();
    }

    [RelayCommand]
    public void AddPlot()
    {

        VisibleCharts.Add(new SinglePlotViewModel
        {
            Title = $"Wykres {VisibleCharts.Count + 1}"
        });
    }

    [RelayCommand]
    public void RemovePlot(SinglePlotViewModel plot)
    {
        if (plot != null && VisibleCharts.Contains(plot))
        {
            VisibleCharts.Remove(plot);
        }
    }


}
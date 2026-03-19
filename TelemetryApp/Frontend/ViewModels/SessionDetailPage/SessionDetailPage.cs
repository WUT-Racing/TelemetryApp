using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;

public partial class SessionDetailViewModel : ObservableObject
{

    [ObservableProperty]
    private double globalTimeMin = 0;

    [ObservableProperty]
    private double globalTimeMax = 100;

    private double _rangeStart = 0;
    public double RangeStart
    {
        get => _rangeStart;
        set
        {
            if (SetProperty(ref _rangeStart, value)) UpdateAxisLimits();
        }
    }

    private double _rangeEnd = 20;
    public double RangeEnd
    {
        get => _rangeEnd;
        set
        {
            if (SetProperty(ref _rangeEnd, value))
                UpdateAxisLimits();
        }
    }

    public Axis[] SharedXAxis { get; set; } = 
    {
        new Axis
        {
            //start i end nie są ustawione na twardo
            Labeler = value => value.ToString("F1") + "s" 
        }
    };
    public ObservableCollection<SinglePlotViewModel> VisibleCharts { get; set; }
    public SessionDetailViewModel()
    {
        VisibleCharts = new ObservableCollection<SinglePlotViewModel>();
        UpdateAxisLimits();
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

    private void UpdateAxisLimits()
    {
        if (RangeEnd <= RangeStart) return;
        SharedXAxis[0].MinLimit = RangeStart;
        SharedXAxis[0].MaxLimit = RangeEnd;
    }

}
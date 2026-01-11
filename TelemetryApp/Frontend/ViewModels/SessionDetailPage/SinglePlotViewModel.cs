using System.Collections.ObjectModel;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

public class SinglePlotViewModel
{
    public ObservableCollection<ISeries> Series { get; set; }
    public ObservableCollection<RectangularSection> Sections { get; set; }

    public SinglePlotViewModel()
    {
        Series = new ObservableCollection<ISeries>
        {
            new LineSeries<double>
            {
                Values = new ObservableCollection<double> { 0 }, // Zaczynamy od zera
                Name = "Sensor 1",
                Fill = null
            }
        };

        Sections = new ObservableCollection<RectangularSection>
        {
            new RectangularSection
            {
                Yi = 5, Yj = 5,
                Stroke = new SolidColorPaint(SKColors.Red, 2)
            }
        };
    }
}
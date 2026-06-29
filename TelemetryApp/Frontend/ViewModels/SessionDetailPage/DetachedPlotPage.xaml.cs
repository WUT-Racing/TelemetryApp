namespace TelemetryApp.Frontend.Views;

using TelemetryApp.Frontend.ViewModels;

public partial class DetachedPlotPage : ContentPage
{
    private bool _isDragging;

    public SessionDetailViewModel OwnerViewModel { get; }

    public DetachedPlotPage(
        SinglePlotViewModel plotViewModel,
        SessionDetailViewModel ownerViewModel)
    {
        InitializeComponent();

        OwnerViewModel = ownerViewModel ?? throw new ArgumentNullException(nameof(ownerViewModel));
        BindingContext = plotViewModel ?? throw new ArgumentNullException(nameof(plotViewModel));
    }

    private void OnPointerPressed(object sender, PointerEventArgs e)
    {
        _isDragging = true;
    }

    private void OnPointerReleased(object sender, PointerEventArgs e)
    {
        _isDragging = false;
    }

    private void OnPointerMoved(object sender, PointerEventArgs e)
    {
        if (!_isDragging)
            return;

        var chart = (LiveChartsCore.SkiaSharpView.Maui.CartesianChart)sender;
        var point = e.GetPosition(chart);

        if (chart.BindingContext is SinglePlotViewModel plotVm &&
            point.HasValue &&
            plotVm.Sections.Count > 0)
        {
            var lvcPoint = new LiveChartsCore.Drawing.LvcPointD(
                (float)point.Value.X,
                (float)point.Value.Y);

            var dataValue = chart.ScalePixelsToData(lvcPoint);

            plotVm.Sections[0].Yi = dataValue.Y;
            plotVm.Sections[0].Yj = dataValue.Y;
        }
    }

    private void OnSignalSearchFocused(object sender, FocusEventArgs e)
    {
        if (sender is Entry entry &&
            entry.BindingContext is SinglePlotViewModel plot)
        {
            plot.OpenSignalSearch();
        }
    }

    private void OnSignalSearchCompleted(object sender, EventArgs e)
    {
        if (sender is Entry entry &&
            entry.BindingContext is SinglePlotViewModel plot)
        {
            if (plot.FilteredSignals.Count > 0)
                plot.SelectSignal(plot.FilteredSignals[0]);
        }
    }

    private void OnSignalSearchSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not CollectionView collectionView)
            return;

        if (collectionView.BindingContext is not SinglePlotViewModel plot)
            return;

        if (e.CurrentSelection.Count == 0)
            return;

        if (e.CurrentSelection[0] is not CanSignalOption option)
            return;

        plot.SelectSignal(option);
        collectionView.SelectedItem = null;
    }
}
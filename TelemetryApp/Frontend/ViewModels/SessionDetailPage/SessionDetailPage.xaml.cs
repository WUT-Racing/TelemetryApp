namespace TelemetryApp.Frontend.Views;

public partial class SessionDetailPage : ContentPage
{
    public SessionDetailViewModel viewModel;
    private bool _isDragging = false;


    public SessionDetailPage(SessionDetailViewModel newViewModel)
    {
        InitializeComponent();
        viewModel = newViewModel;
        BindingContext = viewModel;
    }

    private void OnPointerPressed(object sender, PointerEventArgs e) => _isDragging = true;
    private void OnPointerReleased(object sender, PointerEventArgs e) => _isDragging = false;

    private void OnPointerMoved(object sender, PointerEventArgs e)
    {
        if (!_isDragging) return;

        var chart = (LiveChartsCore.SkiaSharpView.Maui.CartesianChart)sender;
        var point = e.GetPosition(chart);

        if (chart.BindingContext is SinglePlotViewModel plotVm && point.HasValue)
        {
            var lvcPoint = new LiveChartsCore.Drawing.LvcPointD((float)point.Value.X, (float)point.Value.Y);
            var dataValue = chart.ScalePixelsToData(lvcPoint);

            plotVm.Sections[0].Yi = dataValue.Y;
            plotVm.Sections[0].Yj = dataValue.Y;
        }
    }
}
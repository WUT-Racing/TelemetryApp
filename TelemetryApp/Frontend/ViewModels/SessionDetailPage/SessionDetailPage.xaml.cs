namespace TelemetryApp.Frontend.Views;

public partial class SessionDetailPage : ContentPage
{
    public SessionDetailViewModel viewModel;

    public SessionDetailPage(SessionDetailViewModel newViewModel)
    {
        InitializeComponent();
        viewModel = newViewModel;
        BindingContext = viewModel;
    }
}
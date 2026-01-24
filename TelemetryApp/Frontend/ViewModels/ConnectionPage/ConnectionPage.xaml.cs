namespace TelemetryApp.Frontend.Views;

public partial class ConnectionPage : ContentPage
{
    private ConnectionPageViewModel viewModel;

    public ConnectionPage(ConnectionPageViewModel newViewModel)
    {
        InitializeComponent();
        viewModel = newViewModel ?? throw new ArgumentNullException(nameof(newViewModel));
        BindingContext = viewModel;
    }
}
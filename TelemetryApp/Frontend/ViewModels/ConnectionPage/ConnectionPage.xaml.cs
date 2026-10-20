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

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (DriverPicker.ItemsSource != null)
        {
            var currentSelection = DriverPicker.SelectedItem;
            var currentSource = DriverPicker.ItemsSource;

            DriverPicker.ItemsSource = null;
            DriverPicker.ItemsSource = currentSource;
            DriverPicker.SelectedItem = currentSelection;
        }
    }
}

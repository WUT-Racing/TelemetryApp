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

        // Jeśli Picker ma już jakieś źródło danych, zmuszamy go do przeładowania
        if (DriverPicker.ItemsSource != null)
        {
            // 1. Zapamiętujemy aktualnie wybrany element
            var currentSelection = DriverPicker.SelectedItem;
            var currentSource = DriverPicker.ItemsSource;

            // 2. Trik wymuszający na MAUI przerysowanie tekstów w liście
            DriverPicker.ItemsSource = null;
            DriverPicker.ItemsSource = currentSource;

            // 3. Przywracamy wybór, żeby użytkownik nie musiał klikać drugi raz
            DriverPicker.SelectedItem = currentSelection;
        }
    }
}
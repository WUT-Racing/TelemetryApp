using Microsoft.Maui.Controls;

namespace TelemetryApp.Frontend.Views;

public partial class SessionsPage : ContentPage
{
    public SessionsPage(SessionViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
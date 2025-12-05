using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using System;
namespace TelemetryApp.Frontend.Views;

public partial class SessionsPage : ContentPage
{
    private SessionViewModel viewModel;

    public SessionsPage()
    {
        InitializeComponent();
        viewModel = new SessionViewModel();
        BindingContext = viewModel;
    }
    public SessionsPage(SessionViewModel newViewModel)
    {
        InitializeComponent();
        viewModel = newViewModel ?? throw new ArgumentNullException(nameof(newViewModel));
        BindingContext = viewModel;
    }

    private async void OpenSessionDialog(object sender, EventArgs e)
    {
        try
        {
            var result = await FilePicker.Default.PickAsync();
            if (result == null) return;

            var newDatabasePath = result.FullPath;
            viewModel.LoadSession(newDatabasePath);        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Błąd", ex.Message, "OK");
        }
    }
}
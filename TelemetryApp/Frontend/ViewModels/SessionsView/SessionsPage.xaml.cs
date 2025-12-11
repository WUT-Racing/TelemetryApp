using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using System;
namespace TelemetryApp.Frontend.Views;

public partial class SessionsPage : ContentPage
{
    private SessionViewModel viewModel;

    public SessionsPage(SessionViewModel newViewModel)
    {
        InitializeComponent();
        viewModel = newViewModel ?? throw new ArgumentNullException(nameof(newViewModel));
        BindingContext = viewModel;
    }

    
}
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using System;
using TelemetryApp.Frontend.ViewModels;
namespace TelemetryApp.Frontend.Views;

public partial class SettingsPage : ContentPage
{
    private SettingsViewModel viewModel;

    public SettingsPage(SettingsViewModel newViewModel)
    {
        InitializeComponent();
        viewModel = newViewModel ?? throw new ArgumentNullException(nameof(newViewModel));
        BindingContext = viewModel;
    }
    
}
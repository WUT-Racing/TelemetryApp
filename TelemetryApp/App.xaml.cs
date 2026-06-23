using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using TelemetryApp.Frontend.Views;

namespace TelemetryApp;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();

		var savedTheme = Preferences.Get("AppTheme", "Ciemny");
		UserAppTheme = savedTheme == "Jasny" ? AppTheme.Light : AppTheme.Dark;
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var shellPage = MauiProgram.Services.GetRequiredService<AppShell>();
		return new Window(shellPage);
	}
}
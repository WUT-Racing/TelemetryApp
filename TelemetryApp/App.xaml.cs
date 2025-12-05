using Microsoft.Extensions.DependencyInjection;
using TelemetryApp.Frontend.Views;

namespace TelemetryApp;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new SessionsPage());
	}
}
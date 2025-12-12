using Microsoft.Maui.Controls.Handlers.Compatibility;
using TelemetryApp.Frontend.Views;

namespace TelemetryApp;

public partial class AppShell : Shell
{
	public AppShell(AppShellViewModel appShellViewModel)
	{
		InitializeComponent();
		BindingContext = appShellViewModel;
	}
}

using Microsoft.Extensions.Logging;
using TelemetryApp.Frontend.Views;

namespace TelemetryApp;

public static class MauiProgram
{
    public static IServiceProvider Services { get; private set; }
    public static MauiApp CreateMauiApp()
    {

        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        builder.Services.AddSingleton<SQLiteConnectionFactory>();
        builder.Services.AddTransient<TelemetryService>();
        builder.Services.AddTransient<SessionsPage>();
        builder.Services.AddTransient<SessionViewModel>();
        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddSingleton<AppShellViewModel>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddTransient<SessionDetailPage>();
        builder.Services.AddTransient<SessionDetailViewModel>();


        var mauiApp = builder.Build();
        Services = mauiApp.Services;

        return mauiApp;
    }
}
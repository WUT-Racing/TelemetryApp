using LiveChartsCore.SkiaSharpView.Maui;
using Microsoft.Extensions.Logging;
using SkiaSharp.Views.Maui.Controls.Hosting;
using TelemetryApp.Frontend.Views;
using TelemetryApp.Frontend.ViewModels;
using TelemetryApp.Backend.BoardConnection;
using TelemetryApp.Backend.Sessions;
using TelemetryApp.Backend.UserSettings;
using CommunityToolkit.Maui;
using TelemetryApp.Backend.Export;
using Microsoft.Maui.LifecycleEvents;
using TelemetryApp.Backend.AppLifecycle;

namespace TelemetryApp;

public static class MauiProgram
{
    public static IServiceProvider Services { get; private set; }
    public static MauiApp CreateMauiApp()
    {
        

        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseSkiaSharp()
            .UseLiveCharts()
            .UseMauiCommunityToolkit()
            .ConfigureLifecycleEvents(events =>
            {
        #if WINDOWS
                events.AddWindows(windows =>
                {
                    windows.OnWindowCreated(window =>
                    {
                        WindowsRunCloseGuard.Attach(window);
                    });
                });
        #endif
            })
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        builder.Services.AddSingleton<SQLiteConnectionFactory>();
        builder.Services.AddSingleton<TelemetrySessionManager>();
        builder.Services.AddSingleton<SignalDisplaySettingsStore>();
        builder.Services.AddSingleton<TelemetryExportSettingsStore>();
        builder.Services.AddSingleton<TelemetryCsvExportService>();
        builder.Services.AddSingleton<ClientBoard>();
        builder.Services.AddTransient<TelemetryService>();
        builder.Services.AddTransient<SessionsPage>();
        builder.Services.AddTransient<SessionViewModel>();
        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddSingleton<AppShellViewModel>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddSingleton<SettingsViewModel>();
        builder.Services.AddSingleton<SessionDetailPage>();
        builder.Services.AddSingleton<SessionDetailViewModel>();
        builder.Services.AddSingleton<ConnectionPage>();
        builder.Services.AddSingleton<ConnectionPageViewModel>();


        var mauiApp = builder.Build();
        Services = mauiApp.Services;

        return mauiApp;
    }
}
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using TelemetryApp.Backend.BoardConnection;
using TelemetryApp.Backend.Sessions;

#if WINDOWS
using Microsoft.UI.Windowing;
using WinRT.Interop;
#endif

namespace TelemetryApp.Backend.AppLifecycle;

public static class WindowsRunCloseGuard
{
#if WINDOWS
    private static bool _allowClose;
    private static bool _isPromptOpen;

    public static void Attach(Microsoft.UI.Xaml.Window nativeWindow)
    {
        var hwnd = WindowNative.GetWindowHandle(nativeWindow);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);

        appWindow.Closing += OnAppWindowClosing;
    }

    private static async void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_allowClose)
            return;

        var clientBoard = MauiProgram.Services.GetService<ClientBoard>();
        var sessionManager = MauiProgram.Services.GetService<TelemetrySessionManager>();

        var hasRunningRun =
            clientBoard?.IsConnected == true ||
            sessionManager?.HasActiveRun == true;

        if (!hasRunningRun)
            return;

        args.Cancel = true;

        if (_isPromptOpen)
            return;

        _isPromptOpen = true;

        try
        {
            var mainPage = Application.Current?.MainPage;

            if (mainPage is null)
            {
                _allowClose = true;
                Application.Current?.Quit();
                return;
            }

            var sessionName = sessionManager?.ActiveSession?.DisplayName ?? "aktywna sesja";

            var shouldExit = await MainThread.InvokeOnMainThreadAsync(() =>
                mainPage.DisplayAlert(
                    "Aktywny run",
                    $"Aktualnie trwa run w sesji:\n{sessionName}\n\nCzy zakończyć run i zamknąć aplikację?",
                    "Zakończ run i wyjdź",
                    "Anuluj"));

            if (!shouldExit)
                return;

            try
            {
                if (clientBoard?.IsConnected == true)
                {
                    await clientBoard.DisconnectAsync();
                }
                else if (sessionManager?.HasActiveRun == true)
                {
                    sessionManager.StopActiveRun();
                }

                _allowClose = true;

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    Application.Current?.Quit();
                });
            }
            catch (Exception ex)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                    mainPage.DisplayAlert(
                        "Nie udało się zakończyć runu",
                        ex.Message,
                        "OK"));
            }
        }
        finally
        {
            _isPromptOpen = false;
        }
    }
#endif
}
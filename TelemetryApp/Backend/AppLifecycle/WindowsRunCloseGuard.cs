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
    private static bool _allowMainWindowClose;
    private static bool _isPromptOpen;
    private static bool _isMainWindowAttached;

    public static void Attach(Microsoft.UI.Xaml.Window nativeWindow)
    {
        if (_isMainWindowAttached)
            return;

        var hwnd = WindowNative.GetWindowHandle(nativeWindow);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);

        appWindow.Closing += OnAppWindowClosing;

        _isMainWindowAttached = true;
    }

    private static async void OnMainAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_allowMainWindowClose)
            return;

        args.Cancel = true;

        if (_isPromptOpen)
            return;

        var clientBoard = MauiProgram.Services.GetService<ClientBoard>();
        var sessionManager = MauiProgram.Services.GetService<TelemetrySessionManager>();

        var hasRunningRun =
            clientBoard?.IsConnected == true ||
            sessionManager?.HasActiveRun == true;

        if (!hasRunningRun)
        {
            CloseAllOtherWindows();
            _allowMainWindowClose = true;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                Application.Current?.Quit();
            });

            return;
        }

        _isPromptOpen = true;

        try
        {
            var mainPage = Application.Current?.MainPage;

            if (mainPage is null)
            {
                CloseAllOtherWindows();
                _allowMainWindowClose = true;

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    Application.Current?.Quit();
                });

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

                CloseAllOtherWindows();

                _allowMainWindowClose = true;

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

    private static void CloseAllOtherWindows()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var app = Application.Current;

            if (app is null)
                return;

            var windows = app.Windows.ToList();

            foreach (var window in windows)
            {
                if (IsMainMauiWindow(window))
                    continue;

                try
                {
                    app.CloseWindow(window);
                }
                catch
                {
                    // Nie blokujemy zamykania aplikacji, jeśli poboczne okno
                    // było już w trakcie zamykania.
                }
            }
        });
    }

    private static bool IsMainMauiWindow(Window window)
    {
        if (_mainNativeWindow is null)
            return false;

        return ReferenceEquals(window.Handler?.PlatformView, _mainNativeWindow);
    }
#endif
}
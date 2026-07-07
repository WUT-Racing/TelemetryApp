using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System.Threading.Tasks;
using TelemetryApp.Services;

namespace TelemetryApp.Frontend.ViewModels.PythonPages.TestPage
{
    public partial class PythonTestPage : ContentPage
    {
        private readonly TelemetryAnalysisService _telemetryAnalysisService;

        public PythonTestPage()
        {
            InitializeComponent();
            _telemetryAnalysisService = new TelemetryAnalysisService();
        }

        private async void OnAnalyzeButtonClicked(object sender, EventArgs e)
        {
            // Zmiana stanu UI podczas ładowania
            AnalyzeButton.IsEnabled = false;
            LoadingIndicator.IsRunning = true;
            LoadingIndicator.IsVisible = true;

            // Czyścimy okno przed nową analizą
            ResultEditor.Text = "Czekam na uruchomienie procesu...\n";
            ResultEditor.TextColor = Colors.LightGreen;

            try
            {
                // Twoja ścieżka do pliku
                string testCsvPath = "F:\\WUT\\Programing\\Pliki_z_danymi_do_telemetrii\\converted_output_Endu.csv";
                string competition = "Auto";
                string configJson = "{}";

                // TWORZYMY FUNKCJĘ LOGUJĄCĄ (CALLBACK)
                Action<string> progressCallback = (message) =>
                {
                    // Przekazanie zadania aktualizacji tekstu do wątku rysującego interfejs
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        ResultEditor.Text += message + "\n";
                    });
                };

                // Wywołanie z przekazaniem callbacka
                string result = await Task.Run(() =>
                    _telemetryAnalysisService.AnalyzeSessionAsync(testCsvPath, competition, configJson, progressCallback));

                // Doklejenie wyniku końcowego na samym dole
                ResultEditor.Text += "\n--- WYNIK KOŃCOWY (JSON) ---\n" + result;
            }
            catch (Exception ex)
            {
                ResultEditor.Text += $"\nWystąpił błąd:\n{ex.Message}";
                ResultEditor.TextColor = Colors.Red;
            }
            finally
            {
                AnalyzeButton.IsEnabled = true;
                LoadingIndicator.IsRunning = false;
                LoadingIndicator.IsVisible = false;
            }
        }
    }
}
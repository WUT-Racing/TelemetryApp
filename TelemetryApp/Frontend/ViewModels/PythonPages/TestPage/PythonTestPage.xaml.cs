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
            ResultEditor.Text = "Inicjalizacja środowiska Python (może potrwać chwilę przy pierwszym uruchomieniu)...\nTrwa przetwarzanie...";
            ResultEditor.TextColor = Colors.LightGreen;

            try
            {
                // PAMIĘTAJ O ZMIANIE ŚCIEŻKI! (Podwójne ukośniki są wymagane)
                string testCsvPath = "C:\\ścieżka\\do\\twojego\\pliku\\decoded_signals_wide.csv";
                string competition = "Skidpad";
                string configJson = "{}";

                string result = await Task.Run(() =>
                    _telemetryAnalysisService.AnalyzeSessionAsync(testCsvPath, competition, configJson));

                ResultEditor.Text = result;
            }
            catch (Exception ex)
            {
                ResultEditor.Text = $"Wystąpił błąd:\n{ex.Message}";
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
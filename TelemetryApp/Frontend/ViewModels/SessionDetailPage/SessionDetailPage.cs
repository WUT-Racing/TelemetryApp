using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
using TelemetryApp.Frontend.Data;
using TelemetryApp.Frontend.Views;

namespace TelemetryApp.Frontend.ViewModels
{
    public partial class SessionDetailViewModel : ObservableObject
    {
        public ObservableCollection<SinglePlotViewModel> VisibleCharts { get; set; }

        // Lista sensorow (wspoldzielona miedzy oknami)
        public ObservableCollection<SensorReading> Sensors { get; set; }

        public SessionDetailViewModel()
        {
            VisibleCharts = new ObservableCollection<SinglePlotViewModel>();
            Sensors = new ObservableCollection<SensorReading>(GetSampleSensors());
        }

        // Generuje przykładowe dane dla sensorów (pełne: Values + RowIndex)
        private List<SensorReading> GetSampleSensors()
        {
            var sensors = new List<SensorReading>
            {
                new SensorReading
                {
                    SensorId = 1,
                    SensorName = "Test1",
                    Values = new List<int> { 0, 30, 60, 90, 120, 100, 80, 50 }
                },
                new SensorReading
                {
                    SensorId = 2,
                    SensorName = "Test2",
                    Values = new List<int> { 800, 2000, 3500, 5000, 6500, 7000, 6000, 4000 }
                },
                new SensorReading
                {
                    SensorId = 3,
                    SensorName = "Test3",
                    Values = new List<int> { 80, 85, 92, 98, 105, 110, 108, 102 }
                }
            };

            // Ustawienie indeksów wierszy dla naprzemiennych kolorów w tabeli
            for (int i = 0; i < sensors.Count; i++)
                sensors[i].RowIndex = i;

            return sensors;
        }

        // Dodawanie wykresu
        [RelayCommand]
        public void AddPlot()
        {
            VisibleCharts.Add(new SinglePlotViewModel
            {
                Title = $"Wykres {VisibleCharts.Count + 1}"
            });
        }

        // Usuwanie wykresu
        [RelayCommand]
        public void RemovePlot(SinglePlotViewModel plot)
        {
            if (plot != null && VisibleCharts.Contains(plot))
            {
                VisibleCharts.Remove(plot);
            }
        }

        // Otwieranie okna statystyk (przekazuje aktualną listę sensorów)
        [RelayCommand]
        private void ShowTables()
        {
            var window = new Window(new ShowTables(Sensors.ToList()))
            {
                MinimumWidth = 500,
                MinimumHeight = 300,
                Title = "Statystyki sensorów"
            };
            Application.Current?.OpenWindow(window);
        }

        // Otwieranie okna zarządzania sensorami (edycja nazw)
        [RelayCommand]
        private void ManageSensors()
        {
            var window = new Window(new SensorManagementPage(Sensors.ToList()))
            {
                MinimumWidth = 400,
                MinimumHeight = 300,
                Title = "Edytuj nazwy sensorów"
            };
            Application.Current?.OpenWindow(window);
        }
    }
}
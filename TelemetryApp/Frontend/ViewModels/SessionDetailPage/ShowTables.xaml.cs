using System.Collections.Generic;
using Microsoft.Maui.Controls;
using TelemetryApp.Frontend.Data;
using TelemetryApp.Frontend.ViewModels;

namespace TelemetryApp.Frontend.Views
{
    public partial class ShowTables : ContentPage
    {
        public ShowTables(IReadOnlyList<SensorReading> readings)
        {
            InitializeComponent();
            BindingContext = new SensorTableViewModel(readings);
        }
    }
}

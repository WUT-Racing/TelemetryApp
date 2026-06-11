using System.Collections.Generic;
using Microsoft.Maui.Controls;
using TelemetryApp.Frontend.Data;

namespace TelemetryApp.Frontend.Views
{
    public partial class SensorManagementPage : ContentPage
    {
        public SensorManagementPage(List<SensorReading> sensors)
        {
            InitializeComponent();
            BindingContext = new { Sensors = sensors };
        }
    }
}
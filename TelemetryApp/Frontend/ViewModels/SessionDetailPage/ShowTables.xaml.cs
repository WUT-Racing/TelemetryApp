using System.Collections.Generic;
using Microsoft.Maui.Controls;
using TelemetryApp.Frontend.Data;

namespace TelemetryApp.Frontend.Views
{
    public partial class ShowTables : ContentPage
    {
        public ShowTables(List<SensorReading> readings)
        {
            InitializeComponent();
            BindingContext = readings;
        }
    }
}
using System.Collections.Generic;
using Microsoft.Maui.Controls;
using TelemetryApp.Frontend.Data;
using TelemetryApp.Frontend.ViewModels;

namespace TelemetryApp.Frontend.Views
{
    public partial class SensorManagementPage : ContentPage
    {
        public SensorManagementPage(IReadOnlyList<SensorReading> sensors)
        {
            InitializeComponent();
            BindingContext = new SensorManagementViewModel(sensors);
        }
    }
}

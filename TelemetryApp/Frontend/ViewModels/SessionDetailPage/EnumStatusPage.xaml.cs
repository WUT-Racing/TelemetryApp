using System.Collections.Generic;
using Microsoft.Maui.Controls;
using TelemetryApp.Frontend.Data;
using TelemetryApp.Frontend.ViewModels;

namespace TelemetryApp.Frontend.Views
{
    public partial class EnumStatusPage : ContentPage
    {
        public EnumStatusPage(IReadOnlyList<EnumStatusReading> statuses)
        {
            InitializeComponent();
            BindingContext = new EnumStatusTableViewModel(statuses);
        }
    }
}

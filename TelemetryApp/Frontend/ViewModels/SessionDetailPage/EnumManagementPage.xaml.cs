using System.Collections.Generic;
using Microsoft.Maui.Controls;
using TelemetryApp.Frontend.Data;
using TelemetryApp.Frontend.ViewModels;

namespace TelemetryApp.Frontend.Views
{
    public partial class EnumManagementPage : ContentPage
    {
        public EnumManagementPage(IReadOnlyList<EnumStatusReading> statuses)
        {
            InitializeComponent();
            BindingContext = new EnumManagementViewModel(statuses);
        }
    }
}

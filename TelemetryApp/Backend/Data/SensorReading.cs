using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TelemetryApp.Frontend.Data
{
    public partial class SensorReading : ObservableObject
    {
        [ObservableProperty]
        private int _sensorId;

        [ObservableProperty]
        private string _sensorName = string.Empty;

        public List<DateTime> Timestamps { get; set; } = new();
        public List<int> Values { get; set; } = new();

        public int Min => Values.Count > 0 ? Values.Min() : 0;
        public int Max => Values.Count > 0 ? Values.Max() : 0;
        public double Average => Values.Count > 0 ? Values.Average() : 0.0;
        public int Current => Values.Count > 0 ? Values.Last() : 0;
    }
}

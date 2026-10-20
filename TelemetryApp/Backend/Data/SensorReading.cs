using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Maui.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TelemetryApp.Frontend.Data
{
    public enum ValueLimitStatus
    {
        Normal,
        Low,
        High
    }

    // <summary>
    // Klasa zajmująca się odczytem i formatowaniem sensorów do frontendu.
    // Posiada niedokończony kod związany z przypiananiem i odpinaniem sensorów.
    // TODO: Dokończyć logikę przypinania i odpinania sensorów. Więcej detali od Roberta @gruszczrob
    // TODO: Rozważyć przeniesienie logiki od UI do namespace-u Frontendu.
    // </summary>
    public partial class SensorReading : ObservableObject
    {
        [ObservableProperty]
        private int _sensorId;

        [ObservableProperty]
        private int _rowIndex;

        [ObservableProperty]
        private int _displayOrder;

        [ObservableProperty]
        private string _sensorName = string.Empty;

        /*[ObservableProperty]
        private bool _isPinned;*/

        [ObservableProperty]
        private bool _isEnabled = false;

        [ObservableProperty]
        private string _colorHex = "#42A5F5";

        [ObservableProperty]
        private SensorColorOption? _selectedColorOption = SensorColorOption.FindByHex("#42A5F5");

        [ObservableProperty]
        private double? _safeMin;

        [ObservableProperty]
        private double? _safeMax;

        public string SignalKey { get; set; } = string.Empty;
        public ushort CanId { get; set; }
        public string? Unit { get; set; }
        public string? FrameSummary { get; set; }
        public string OriginalSignalName { get; set; } = string.Empty;

        public List<DateTime> Timestamps { get; set; } = new();
        public List<double> Values { get; set; } = new();

        public double Min => Values.Count > 0 ? Values.Min() : 0.0;
        public double Max => Values.Count > 0 ? Values.Max() : 0.0;
        public double Average => Values.Count > 0 ? Values.Average() : 0.0;
        public double Current => Values.Count > 0 ? Values.Last() : 0.0;

        public string CurrentText => HasData ? FormatValue(Current) : "—";
        public string MinText => HasData ? FormatValue(Min) : "—";
        public string MaxText => HasData ? FormatValue(Max) : "—";
        public string AverageText => HasData ? FormatValue(Average) : "—";
        public bool HasData => Values.Count > 0;

        /*public string PinText => IsPinned ? "★" : "☆";
        public string PinButtonText => IsPinned ? "Odepnij" : "Przypnij";*/
        public string EnabledText => IsEnabled ? "Włączony" : "Wyłączony";
        public Color SensorColor => ParseColor(ColorHex);

        public ValueLimitStatus MinStatus => GetLimitStatus(Min);
        public ValueLimitStatus MaxStatus => GetLimitStatus(Max);
        public ValueLimitStatus AvgStatus => GetLimitStatus(Average);
        public ValueLimitStatus CurrentStatus => GetLimitStatus(Current);

        private ValueLimitStatus GetLimitStatus(double value)
        {
            if (!HasData) 
                return ValueLimitStatus.Normal;
                
            if (SafeMin.HasValue && value < SafeMin.Value) 
                return ValueLimitStatus.Low;
                
            if (SafeMax.HasValue && value > SafeMax.Value) 
                return ValueLimitStatus.High;
                
            return ValueLimitStatus.Normal;
        }

        public string DetailsText
        {
            get
            {
                var unitText = string.IsNullOrWhiteSpace(Unit) ? string.Empty : $" [{Unit}]";
                var frameText = string.IsNullOrWhiteSpace(FrameSummary) ? string.Empty : $" — {FrameSummary}";
                return $"0x{CanId:X3} / {SignalKey}{unitText}{frameText}";
            }
        }

        public string SearchText => $"{SensorName} {OriginalSignalName} {SignalKey} {FrameSummary} 0x{CanId:X3} {Unit}";

        /*partial void OnIsPinnedChanged(bool value)
        {
            OnPropertyChanged(nameof(PinText));
            OnPropertyChanged(nameof(PinButtonText));
        }*/

        partial void OnIsEnabledChanged(bool value)
        {
            OnPropertyChanged(nameof(EnabledText));
            OnPropertyChanged(nameof(SearchText));
        }

        partial void OnColorHexChanged(string value)
        {
            OnPropertyChanged(nameof(SensorColor));
        }

        partial void OnSelectedColorOptionChanged(SensorColorOption? value)
        {
            if (value is not null)
                ColorHex = value.Hex;
        }

        partial void OnSensorNameChanged(string value)
        {
            OnPropertyChanged(nameof(SearchText));
        }

        public void SetColor(SensorColorOption? option)
        {
            if (option is null)
                return;

            ColorHex = option.Hex;
        }

        public void ClearValues()
        {
            Values.Clear();
            Timestamps.Clear();

            OnPropertyChanged(nameof(HasData));
            OnPropertyChanged(nameof(Min));
            OnPropertyChanged(nameof(Max));
            OnPropertyChanged(nameof(Average));
            OnPropertyChanged(nameof(Current));
            OnPropertyChanged(nameof(MinText));
            OnPropertyChanged(nameof(MaxText));
            OnPropertyChanged(nameof(AverageText));
            OnPropertyChanged(nameof(CurrentText));
            OnPropertyChanged(nameof(MinStatus));
            OnPropertyChanged(nameof(MaxStatus));
            OnPropertyChanged(nameof(AvgStatus));
            OnPropertyChanged(nameof(CurrentStatus));
        }

        public void AddValue(double value, DateTime timestamp)
        {
            Values.Add(value);
            Timestamps.Add(timestamp);

            OnPropertyChanged(nameof(HasData));
            OnPropertyChanged(nameof(Min));
            OnPropertyChanged(nameof(Max));
            OnPropertyChanged(nameof(Average));
            OnPropertyChanged(nameof(Current));
            OnPropertyChanged(nameof(MinText));
            OnPropertyChanged(nameof(MaxText));
            OnPropertyChanged(nameof(AverageText));
            OnPropertyChanged(nameof(CurrentText));
            OnPropertyChanged(nameof(MinStatus));
            OnPropertyChanged(nameof(MaxStatus));
            OnPropertyChanged(nameof(AvgStatus));
            OnPropertyChanged(nameof(CurrentStatus));
        }

        private string FormatValue(double value)
        {
            var text = value.ToString("0.###");
            return string.IsNullOrWhiteSpace(Unit) ? text : $"{text} {Unit}";
        }

        private static Color ParseColor(string? hex)
        {
            try
            {
                return Color.FromArgb(string.IsNullOrWhiteSpace(hex) ? "#42A5F5" : hex);
            }
            catch
            {
                return Color.FromArgb("#42A5F5");
            }
        }
    }
}

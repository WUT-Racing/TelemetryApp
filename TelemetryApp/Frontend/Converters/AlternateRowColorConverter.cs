using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace TelemetryApp.Frontend.Converters
{
    public class AlternateRowColorConverter : IValueConverter
    {
        public Color EvenColor { get; set; }
        public Color OddColor { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int rowIndex && EvenColor != null && OddColor != null)
                return rowIndex % 2 == 0 ? EvenColor : OddColor;
            return EvenColor ?? Colors.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
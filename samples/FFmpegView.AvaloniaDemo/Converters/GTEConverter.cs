using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace FFmpegView.AvaloniaDemo.Converters
{
    public class GTEConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (double?) value >= (double?) parameter;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (double?) value < (double?) parameter;
        }
    }
}
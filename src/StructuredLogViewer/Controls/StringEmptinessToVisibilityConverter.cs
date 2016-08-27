using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace StructuredLogViewer
{
    public class StringEmptinessToVisibilityConverter : IValueConverter
    {
        public static readonly StringEmptinessToVisibilityConverter Instance = new StringEmptinessToVisibilityConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var text = value as string;
            return string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

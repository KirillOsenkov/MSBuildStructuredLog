using System.Globalization;
using Avalonia.Data.Converters;

namespace StructuredLogViewer.Avalonia.Controls
{
    public class StringEmptinessToVisibilityConverter : IValueConverter
    {
        public static StringEmptinessToVisibilityConverter Instance { get; } = new StringEmptinessToVisibilityConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var text = value as string;
            bool result = string.IsNullOrEmpty(text);
            if (parameter is string s && s == "Invert")
            {
                result = !result;
            }

            return result;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}

using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace StructuredLogViewer.Avalonia.Controls
{
    public class NodeIsSelectedToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isSelected)
                return isSelected ? Brushes.Black : parameter;

            return Brushes.Black;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}

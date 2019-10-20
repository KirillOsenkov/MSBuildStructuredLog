using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace StructuredLogViewer.Avalonia.Controls
{
    public class NodeLowRelevanceToOpacityConverter : IValueConverter
    {
        private static readonly object boxedLowRelevance = 0.25;
        private static readonly object boxedHighRelevance = 1.0;
        
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isLowRelevance)
                return isLowRelevance ? boxedLowRelevance : boxedHighRelevance;

            return boxedHighRelevance;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) 
            => throw new NotSupportedException();
    }
}

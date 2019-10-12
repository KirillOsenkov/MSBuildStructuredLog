using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace StructuredLogViewer.Avalonia.Controls
{
    public class ProjectIconConverter : IValueConverter
    {
        private readonly Dictionary<string, object> icons = new Dictionary<string, object>();
            
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            switch (value as string)
            {
                case ".sln":
                    return GetIcon("SlnIcon");
                
                case ".csproj":
                    return GetIcon("CSProjIcon");
                    
                case ".vbproj":
                    return GetIcon("VBProjIcon");
                    
                case ".fsproj":
                    return GetIcon("FSProjIcon");
                    
                default:
                    return GetIcon("GenericProjectIcon");
            }
        }

        private object GetIcon(string resourceName)
        {
            if (!icons.TryGetValue(resourceName, out var icon))
            {
                if (!Application.Current.Resources.TryGetResource(resourceName, out icon))
                    icon = null;

                icons[resourceName] = icon;
            }

            return icon;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) 
            => throw new NotSupportedException();
    }
}

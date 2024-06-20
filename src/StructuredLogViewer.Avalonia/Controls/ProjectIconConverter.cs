using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace StructuredLogViewer.Avalonia.Controls
{
    public class ProjectIconConverter : IValueConverter
    {
        private readonly Dictionary<string, DrawingGroup> icons = new Dictionary<string, DrawingGroup>();

        public DrawingGroup ProjectExtensionToIcon(string projectExtension)
        {
            switch (projectExtension)
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

        private DrawingGroup GetIcon(string resourceName)
        {
            if (!icons.TryGetValue(resourceName, out var icon))
            {
                if (!Application.Current.Resources.TryGetResource(resourceName, null, out var resource))
                    resource = null;

                icon = resource as DrawingGroup;
                icons[resourceName] = icon;
            }

            return icon;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) 
            => ProjectExtensionToIcon(value as string);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) 
            => throw new NotSupportedException();
    }
}

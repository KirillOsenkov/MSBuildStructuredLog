using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls.Shapes;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;

namespace StructuredLogViewer.Avalonia.Controls;

// WPF expresses these per-node visual variations with DataTriggers;
// Avalonia has no triggers, so each one is a small converter instead.

/// <summary>
/// Colors special diagnostic folders (DoubleWrites etc.) red, other folders
/// DarkGoldenrod when unselected, like the WPF Folder template triggers.
/// Takes [IsSelected, Name].
/// </summary>
public class FolderForegroundConverter : IMultiValueConverter
{
    public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture)
    {
        var name = values.Count > 1 ? values[1] as string : null;
        if (IsErrorFolder(name))
        {
            return Brushes.Red;
        }

        if (values.Count > 0 && values[0] is bool isSelected && !isSelected)
        {
            return Brushes.DarkGoldenrod;
        }

        // inherit the tree view item's (selection) foreground
        return AvaloniaProperty.UnsetValue;
    }

    internal static bool IsErrorFolder(string name) =>
        name is "DoubleWrites" or "Circular Project References" or "DoubleBuild";
}

/// <summary>
/// Gives special diagnostic folders the error icon fill, others the folder fill.
/// </summary>
public class FolderIconFillConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var resourceName = FolderForegroundConverter.IsErrorFolder(value as string)
            ? "ErrorBrush"
            : "ClosedFolderBrush";
        return GetResource(resourceName);
    }

    internal static object GetResource(string resourceName)
    {
        if (!Application.Current.Resources.TryGetResource(resourceName, Application.Current.ActualThemeVariant, out var resource))
        {
            resource = null;
        }

        return resource;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Search result highlight background: $time spans are Lavender,
/// write/read spans Turquoise/PaleTurquoise, plain matches Yellow.
/// </summary>
public class HighlightedTextBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return (value as string) switch
        {
            "time" => Brushes.Lavender,
            "write" => Brushes.Turquoise,
            "read" => Brushes.PaleTurquoise,
            _ => Brushes.Yellow
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Directional copy icon for FileCopy nodes: box position and color depend
/// on whether the node is the Source, the Destination or the copy itself.
/// </summary>
public class FileCopyKindToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string data;
        IBrush brush;
        switch (value as string)
        {
            case "Source":
                data = "M0,5 L0,11 L6,11 L6,5 Z M 6,8 L16,8";
                brush = Brushes.LightSkyBlue;
                break;
            case "Destination":
                data = "M10,5 L10,11 L16,11 L16,5 Z M 0,8 L10,8";
                brush = Brushes.DarkSalmon;
                break;
            default:
                data = "M5,5 L5,11 L11,11 L11,5 Z M 0,8 L16,8";
                brush = Brushes.Thistle;
                break;
        }

        return new Path
        {
            Data = StreamGeometry.Parse(data),
            Stroke = brush,
            Fill = brush,
            StrokeThickness = 1,
            Width = 16,
            Height = 16,
            Margin = new Thickness(1, 2, 6, 2),
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// NoImport reason text gets the NoImportFill background when unselected.
/// </summary>
public class NoImportReasonBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isSelected && isSelected)
        {
            return Brushes.Transparent;
        }

        return FolderIconFillConverter.GetResource("NoImportFill");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

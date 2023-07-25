using System.Windows.Controls;

namespace StructuredLogViewer;

internal static class AvaloniaExtensions
{
    public static void AddItem(this ItemsControl itemsControl, object o)
    {
        itemsControl.Items.Add(o);
    }
}

using System.Windows.Controls;
using System.Windows;

namespace StructuredLogViewer.Controls
{
    public static class TreeViewItemExtensions
    {
        public static FrameworkElement GetHeaderControl(this TreeViewItem item)
        {
            if (item == null || item.Template == null)
            {
                return item;
            }

            var result = item.Template.FindName("PART_Header", item) as FrameworkElement;
            if (result != null)
            {
                return result;
            }

            return item;
        }
    }
}
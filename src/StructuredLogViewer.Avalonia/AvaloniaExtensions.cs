using Avalonia.Controls;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;

namespace StructuredLogViewer.Avalonia
{
    public static class AvaloniaExtensions
    {
        public static void AddItem(this ItemsControl itemsControl, object o)
        {
            (itemsControl.Items as IList)?.Add(o);
        }

        public static void RemoveItem(this ItemsControl itemsControl, object o)
        {
            (itemsControl.Items as IList)?.Remove(o);
        }

        public static void ClearItems(this ItemsControl itemsControl)
        {
            (itemsControl.Items as IList)?.Clear();
        }

        public static void RegisterControl<TControl>(this IControl parent, out TControl control, string name)
            where TControl : class, IControl
        {
            control = parent.FindControl<TControl>(name);
        }
    }
}

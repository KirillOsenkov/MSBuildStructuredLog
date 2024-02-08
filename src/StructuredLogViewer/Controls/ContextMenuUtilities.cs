using System.Windows;

namespace StructuredLogViewer.Controls
{
    public class ContextMenuUtilities : UIElement
    {
        public static readonly DependencyProperty IsContextMenuOpenProperty = DependencyProperty.RegisterAttached(
            "IsContextMenuOpen",
            typeof(bool),
            typeof(UIElement),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));

        public static bool GetIsContextMenuOpen(UIElement element)
         => (bool)element.GetValue(IsContextMenuOpenProperty);

        public static void SetIsContextMenuOpen(UIElement element, bool value)
         => element.SetValue(IsContextMenuOpenProperty, value);
    }
}
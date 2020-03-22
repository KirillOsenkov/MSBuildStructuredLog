using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Build.Logging.StructuredLogger;

namespace StructuredLogViewer.Controls
{
    public class NodeHyperlinkControl : TextBlock
    {
        public NodeHyperlinkControl()
        {
            this.MouseEnter += Control_MouseEnter;
            this.MouseLeave += Control_MouseLeave;
            this.MouseUp += Control_MouseUp;

            DestinationNodeGetter = GetParentTarget;
        }

        private Brush defaultForeground;

        public Func<BaseNode> DestinationNodeGetter { get; set; }

        public Target GetParentTarget()
        {
            if (DataContext is Target target)
            {
                var parentTargetName = target.ParentTarget;
                if (parentTargetName != null)
                {
                    var project = target.Project;
                    if (project != null)
                    {
                        var parentTarget = project.FindFirstDescendant<Target>(t => t.Name == parentTargetName);
                        return parentTarget;
                    }
                }
            }

            return null;
        }

        private void Control_MouseUp(object sender, MouseButtonEventArgs e)
        {
            var destinationNode = DestinationNodeGetter?.Invoke();
            if (destinationNode != null)
            {
                NavigateToNode(destinationNode);
            }
        }

        private void NavigateToNode(BaseNode node)
        {
            var buildControl = GetBuildControl();
            if (buildControl != null)
            {
                buildControl.SelectItem(node);
            }
        }

        public BuildControl GetBuildControl()
        {
            return FindParentElement(this, e => e is BuildControl) as BuildControl;
        }

        public DependencyObject FindParentElement(FrameworkElement element, Func<DependencyObject, bool> predicate)
        {
            DependencyObject current = element;
            while (current != null)
            {
                if (predicate(current))
                {
                    return current;
                }

                current = VisualTreeHelper.GetParent(current) ?? LogicalTreeHelper.GetParent(current);
            }

            return null;
        }

        private void Control_MouseEnter(object sender, MouseEventArgs e)
        {
            var destinationNode = DestinationNodeGetter?.Invoke();
            if (destinationNode == null)
            {
                return;
            }

            this.TextDecorations = System.Windows.TextDecorations.Underline;
            if (defaultForeground == null)
            {
                defaultForeground = this.Foreground;
            }

            Foreground = Brushes.Blue;
        }

        private void Control_MouseLeave(object sender, MouseEventArgs e)
        {
            this.TextDecorations = null;
            Foreground = defaultForeground ?? Brushes.LightBlue;
        }
    }
}
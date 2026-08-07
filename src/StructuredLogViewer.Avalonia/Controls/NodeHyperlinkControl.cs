using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging.StructuredLogger;

namespace StructuredLogViewer.Avalonia.Controls
{
    public class NodeHyperlinkControl : TextBlock
    {
        public NodeHyperlinkControl()
        {
            PointerEntered += Control_PointerEntered;
            PointerExited += Control_PointerExited;
            PointerReleased += Control_PointerReleased;

            DestinationNodeGetter = GetDestination;
        }

        private IBrush defaultForeground;

        public Func<BaseNode> DestinationNodeGetter { get; set; }

        public string HyperlinkKind { get; set; }

        public BaseNode GetDestination()
        {
            if (DataContext is Target target)
            {
                if (target.OriginalNode != null)
                {
                    return target.OriginalNode;
                }

                var parentTargetName = target.ParentTarget;
                if (parentTargetName != null)
                {
                    var project = target.Project;
                    if (project != null)
                    {
                        var parentTarget = project.FindFirstDescendant<Target>(t => t.Name == parentTargetName && t.Project == project);
                        if (parentTarget != null)
                        {
                            return parentTarget;
                        }

                        var buildControl = GetBuildControl();
                        if (buildControl != null)
                        {
                            string text = buildControl.TryFindDanglingTarget(project, parentTargetName);
                            if (text != null)
                            {
                                var reason = target.TargetBuiltReason switch
                                {
                                    TargetBuiltReason.BeforeTargets => "[Before] ",
                                    TargetBuiltReason.DependsOn => "[DependsOn] ",
                                    TargetBuiltReason.AfterTargets => "[After] ",
                                    _ => ""
                                };
                                text = $" {text} → {reason}{target.Name}";
                                DestinationNodeGetter = null;
                                Text = text;
                            }
                        }
                    }
                }
            }
            else if (DataContext is Project project)
            {
                if (HyperlinkKind == "Evaluation")
                {
                    var evaluation = project.GetNearestParent<Build>()?.FindEvaluation(project.EvaluationId);
                    if (evaluation != null)
                    {
                        return evaluation;
                    }

                    return null;
                }

                var targetName = project.EntryTargets.FirstOrDefault();
                if (targetName != null)
                {
                    var firstTarget = project.FindFirstDescendant<Target>(t => t.Name == targetName && t.Project == project);
                    return firstTarget;
                }
            }

            if (DataContext is EntryTarget entryTarget)
            {
                return entryTarget.Target;
            }

            return null;
        }

        private void Control_PointerReleased(object sender, PointerReleasedEventArgs e)
        {
            var destinationNode = DestinationNodeGetter?.Invoke();
            if (destinationNode != null)
            {
                NavigateToNode(destinationNode);
                e.Handled = true;
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
            return this.FindAncestorOfType<BuildControl>();
        }

        private void Control_PointerEntered(object sender, PointerEventArgs e)
        {
            var destinationNode = DestinationNodeGetter?.Invoke();
            if (destinationNode == null)
            {
                return;
            }

            TextDecorations = global::Avalonia.Media.TextDecorations.Underline;
            if (defaultForeground == null)
            {
                defaultForeground = Foreground;
            }

            Foreground = Brushes.RoyalBlue;
            Cursor = new Cursor(StandardCursorType.Hand);
        }

        private void Control_PointerExited(object sender, PointerEventArgs e)
        {
            TextDecorations = null;

            if (DestinationNodeGetter == null)
            {
                return;
            }

            Foreground = defaultForeground ?? Brushes.LightBlue;
        }
    }
}

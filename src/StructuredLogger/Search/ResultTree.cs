using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Logging.StructuredLogger;

namespace StructuredLogViewer
{
    public class ResultTree
    {
        public static Folder BuildResultTree(
            object resultsObject,
            bool moreAvailable = false,
            TimeSpan elapsed = default,
            TimeSpan precalculationDuration = default)
        {
            var root = new Folder();

            var results = resultsObject as ICollection<SearchResult>;
            if (results == null)
            {
                return root;
            }

            string durationString = TextUtilities.DisplayDuration(elapsed);
            if (!string.IsNullOrEmpty(durationString))
            {
                durationString = $" Search took: {durationString}";
            }

            string status = $"{results.Count} result{(results.Count == 1 ? "" : "s")}.{durationString}";
            string precalculationString = TextUtilities.DisplayDuration(precalculationDuration);
            if (!string.IsNullOrWhiteSpace(precalculationString))
            {
                status += $" (precalculation: {precalculationString})";
            }

            root.Children.Add(new Note
            {
                Text = status
            });

            bool includeDuration = false;
            bool includeStart = false;
            bool includeEnd = false;

            foreach (var r in results)
            {
                if (r.Duration != default)
                {
                    includeDuration = true;
                }

                if (r.StartTime != default)
                {
                    includeStart = true;
                }

                if (r.EndTime != default)
                {
                    includeEnd = true;
                }
            }

            if (includeDuration)
            {
                results = results.OrderByDescending(r => r.Duration).ToArray();

                TimeSpan totalDuration = TimeSpan.Zero;
                foreach (var result in results)
                {
                    totalDuration += result.Duration;
                }

                root.Children.Add(new Message
                {
                    Text = $"Total duration: {TextUtilities.DisplayDuration(totalDuration)}"
                });
            }
            else if (includeStart)
            {
                results = results.OrderBy(r => r.StartTime).ToArray();
            }
            else if (includeEnd)
            {
                results = results.OrderBy(r => r.EndTime).ToArray();
            }

            foreach (var result in results)
            {
                TreeNode parent = root;
                var resultNode = result.Node;

                bool nest = !includeDuration && !includeStart && !includeEnd;

                if (nest && resultNode != null && resultNode is not Project && resultNode.Parent != null)
                {
                    if (result.RootFolder is string rootFolderName)
                    {
                        parent = InsertParent(
                            parent,
                            actualParent: null,
                            name: rootFolderName);
                    }

                    var project = resultNode.GetNearestParent<Project>();
                    if (project != null)
                    {
                        parent = InsertParent(parent, project);
                    }
                    else
                    {
                        var evaluation = resultNode.GetNearestParent<ProjectEvaluation>();
                        if (evaluation != null)
                        {
                            parent = InsertParent(parent, evaluation.Parent as TimedNode);
                            parent = InsertParent(parent, evaluation);
                        }
                    }

                    bool isTarget = resultNode is Target;

                    var target = resultNode.GetNearestParent<Target>();
                    if (!isTarget && project != null && target != null && target.Project == project)
                    {
                        parent = InsertParent(parent, target);
                    }

                    // nest under a Task, unless it's an MSBuild task higher up the parent chain
                    var task = resultNode.GetNearestParent<Task>(t => !string.Equals(t.Name, "MSBuild", StringComparison.OrdinalIgnoreCase));
                    if (task != null && !isTarget && project != null && task.GetNearestParent<Project>() == project)
                    {
                        parent = InsertParent(parent, task);
                    }

                    if (resultNode is Item item &&
                        item.Parent is NamedNode itemParent &&
                        (itemParent is Folder || itemParent is AddItem || itemParent is RemoveItem))
                    {
                        parent = InsertParent(parent, itemParent);
                    }

                    if (resultNode is Metadata metadata &&
                        metadata.Parent is Item parentItem &&
                        parentItem.Parent is NamedNode grandparent &&
                        (grandparent is Folder || grandparent is AddItem || grandparent is RemoveItem))
                    {
                        parent = InsertParent(parent, grandparent);
                        parent = InsertParent(parent, parentItem);
                    }
                }

                if (resultNode == null || resultNode.Parent != null)
                {
                    var proxy = new ProxyNode();
                    proxy.Original = resultNode;
                    proxy.SearchResult = result;
                    proxy.Text = resultNode?.Title;

                    resultNode = proxy;
                }

                parent.Children.Add(resultNode);
            }

            if (!root.HasChildren)
            {
                root.Children.Add(new Message { Text = "No results found." });
            }

            return root;
        }

        private static TreeNode InsertParent(
            TreeNode parent,
            NamedNode actualParent,
            string name = null,
            Func<ProxyNode, bool> existingNodeFinder = null)
        {
            name ??= ProxyNode.GetNodeText(actualParent);

            ProxyNode folderProxy = null;

            if (existingNodeFinder != null)
            {
                foreach (var existingChild in parent.Children.OfType<ProxyNode>())
                {
                    if (existingNodeFinder(existingChild))
                    {
                        folderProxy = existingChild;
                        break;
                    }
                }

                if (folderProxy == null)
                {
                    folderProxy = new ProxyNode { Text = name };
                    parent.AddChild(folderProxy);
                }
            }

            if (folderProxy == null)
            {
                folderProxy = parent.GetOrCreateNodeWithText<ProxyNode>(name);
            }

            folderProxy.Original = actualParent;

            if (folderProxy.Highlights.Count == 0)
            {
                if (actualParent is Target or Task or AddItem or RemoveItem)
                {
                    folderProxy.Highlights.Add(folderProxy.OriginalType + " ");
                }

                folderProxy.Highlights.Add(name);
            }

            folderProxy.IsExpanded = true;
            return folderProxy;
        }
    }
}
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
            TimeSpan elapsed = default,
            TimeSpan precalculationDuration = default,
            bool addDuration = true,
            Func<BaseNode> addWhenNoResults = null)
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

            if (addDuration)
            {
                root.Children.Add(new Note
                {
                    Text = status
                });
            }

            bool includeDuration = false;
            bool includeStart = false;
            bool includeEnd = false;
            bool nest = !includeDuration && !includeStart && !includeEnd;

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

            var nodeToProxyMap = new Dictionary<BaseNode, BaseNode>();

            foreach (var result in results)
            {
                var resultNode = result.Node;
                if (resultNode != null && resultNode.Parent != null)
                {
                    var proxy = new ProxyNode();
                    proxy.Original = resultNode;
                    proxy.SearchResult = result;
                    if (resultNode is IHasRelevance relevance)
                    {
                        proxy.IsLowRelevance = relevance.IsLowRelevance;
                    }

                    proxy.Text = ProxyNode.GetNodeText(resultNode);
                    nodeToProxyMap[resultNode] = proxy;
                }
            }

            // We don't want to reuse nodes created for a result under a particular rootFolderName,
            // such as Incoming and Outgoing for file copies. We do want to reuse the proxies for
            // actual results as well as reuse nodes under each rootFolderName.
            Dictionary<string, Dictionary<BaseNode, BaseNode>> perRootFolderProxyCache = new();

            foreach (var result in results)
            {
                TreeNode parent = root;
                var resultNode = result.Node;

                var map = nodeToProxyMap;

                if (nest && resultNode != null && resultNode is not Project && resultNode.Parent != null)
                {
                    if (result.RootFolder is string rootFolderName)
                    {
                        parent = InsertParent(
                            map: null,
                            parent,
                            actualParent: null,
                            name: rootFolderName);

                        // create a dictionary specific for this rootFolderName based on the base dictionary
                        if (!perRootFolderProxyCache.TryGetValue(rootFolderName, out map))
                        {
                            map = new Dictionary<BaseNode, BaseNode>(nodeToProxyMap);
                            perRootFolderProxyCache[rootFolderName] = map;
                        }
                    }

                    var project = resultNode.GetNearestParent<Project>();
                    if (project != null)
                    {
                        parent = InsertParent(map, parent, project);
                    }
                    else
                    {
                        var evaluation = resultNode.GetNearestParent<ProjectEvaluation>();
                        if (evaluation != null)
                        {
                            parent = InsertParent(map, parent, evaluation.Parent as TimedNode);
                            parent = InsertParent(map, parent, evaluation);
                        }
                    }

                    bool isTarget = resultNode is Target;

                    var target = resultNode.GetNearestParent<Target>();
                    if (!isTarget && project != null && target != null && target.Project == project)
                    {
                        parent = InsertParent(map, parent, target);
                    }

                    // nest under a Task, unless it's an MSBuild task higher up the parent chain
                    var task = resultNode.GetNearestParent<Task>(t => t is not MSBuildTask);
                    if (task != null && !isTarget && project != null && task.GetNearestParent<Project>() == project)
                    {
                        parent = InsertParent(map, parent, task);
                    }

                    if (resultNode is Item item &&
                        item.Parent is NamedNode itemParent &&
                        (itemParent is Folder || itemParent is AddItem || itemParent is RemoveItem))
                    {
                        parent = InsertParent(map, parent, itemParent);
                    }

                    if (resultNode is Metadata metadata &&
                        metadata.Parent is Item parentItem &&
                        parentItem.Parent is NamedNode grandparent &&
                        (grandparent is Folder || grandparent is AddItem || grandparent is RemoveItem))
                    {
                        parent = InsertParent(map, parent, grandparent);
                        parent = InsertParent(map, parent, parentItem);
                    }
                }

                if (resultNode == null || resultNode.Parent != null)
                {
                    if (map.TryGetValue(resultNode, out var existing))
                    {
                        resultNode = existing;
                    }
                    else
                    {
                        var proxy = new ProxyNode();
                        proxy.Original = resultNode;
                        proxy.SearchResult = result;
                        proxy.Text = ProxyNode.GetNodeText(resultNode);

                        resultNode = proxy;
                    }
                }

                parent.Children.Add(resultNode);
            }

            if (!root.HasChildren && addWhenNoResults != null)
            {
                var node = addWhenNoResults();
                root.Children.Add(node);
            }

            return root;
        }

        private static TreeNode InsertParent(
            Dictionary<BaseNode, BaseNode> map,
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

            if (map != null)
            {
                if (map.TryGetValue(actualParent, out var result) && result is ProxyNode found)
                {
                    folderProxy = found;
                }
            }

            if (folderProxy == null)
            {
                folderProxy = parent.GetOrCreateNodeWithText<ProxyNode>(name);
                if (map != null)
                {
                    map[actualParent] = folderProxy;
                }
            }

            folderProxy.Original = actualParent;

            folderProxy.IsExpanded = true;
            return folderProxy;
        }
    }
}
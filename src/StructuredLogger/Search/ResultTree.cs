using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Logging.StructuredLogger;

namespace StructuredLogViewer
{
    public class ResultTree
    {
        public static Folder BuildResultTree(object resultsObject, bool moreAvailable = false, TimeSpan elapsed = default)
        {
            var root = new Folder();

            var results = resultsObject as ICollection<SearchResult>;
            if (results == null)
            {
                return root;
            }

            root.Children.Add(new Message
            {
                Text = $"{results.Count} result{(results.Count == 1 ? "" : "s")}. Search took: {elapsed.ToString()}"
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

                bool isProject = resultNode is Project;
                bool isTarget = resultNode is Target;

                if (!includeDuration && !includeStart && !includeEnd && !isProject)
                {
                    var project = resultNode.GetNearestParent<Project>();
                    if (project != null)
                    {
                        var projectName = ProxyNode.GetNodeText(project);
                        parent = InsertParent(
                            parent,
                            project,
                            projectName,
                            existingProxy => existingProxy.Original is Project existing &&
                                string.Equals(existing.SourceFilePath, project.SourceFilePath, StringComparison.OrdinalIgnoreCase));
                    }

                    var target = resultNode.GetNearestParent<Target>();
                    if (!isTarget && project != null && target != null && target.Project == project)
                    {
                        parent = InsertParent(parent, target, target.TypeName + " " + target.Name);
                    }

                    // nest under a Task, unless it's an MSBuild task higher up the parent chain
                    var task = resultNode.GetNearestParent<Task>(t => !string.Equals(t.Name, "MSBuild", StringComparison.OrdinalIgnoreCase));
                    if (task != null && !isTarget && project != null && task.GetNearestParent<Project>() == project)
                    {
                        parent = InsertParent(parent, task, "Task " + task.Name);
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
                        parent = InsertParent(parent, parentItem, parentItem.Text);
                    }

                    if (parent == root)
                    {
                        var evaluation = resultNode.GetNearestParent<ProjectEvaluation>();
                        if (evaluation != null)
                        {
                            var evaluationName = ProxyNode.GetNodeText(evaluation);
                            parent = InsertParent(parent, evaluation, evaluationName);
                        }
                    }
                }

                var proxy = new ProxyNode();
                proxy.Original = resultNode;
                proxy.SearchResult = result;
                parent.Children.Add(proxy);
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
            name ??= actualParent.Name;

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
                    folderProxy = new ProxyNode { Name = name };
                    parent.AddChild(folderProxy);
                }
            }

            if (folderProxy == null)
            {
                folderProxy = parent.GetOrCreateNodeWithName<ProxyNode>(name);
            }

            folderProxy.Original = actualParent;
            if (folderProxy.Highlights.Count == 0)
            {
                folderProxy.Highlights.Add(name);
            }

            folderProxy.IsExpanded = true;
            return folderProxy;
        }
    }
}
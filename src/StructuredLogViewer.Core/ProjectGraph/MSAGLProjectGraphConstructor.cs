using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.Logging.StructuredLogger;
using Microsoft.Msagl.Drawing;

namespace StructuredLogViewer.Core.ProjectGraph
{
    public class MsaglProjectGraphConstructor
    {
        private class GlobalPropertyComparer : IComparer<string>
        {
            public static readonly GlobalPropertyComparer Instance = new GlobalPropertyComparer();

            // larger number means higher priority
            private static readonly IReadOnlyDictionary<string, int> propertiesWithPriority =
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    {"TargetFramework", 3},
                    {"Configuration", 2},
                    {"Platform", 1}
                };

            private GlobalPropertyComparer()
            {
            }

            public int Compare(string x, string y)
            {
                if (x == null || y == null)
                {
                    return string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
                }

                var xIsHighPriority = propertiesWithPriority.TryGetValue(x, out var xPriority);
                var yIsHighPriority = propertiesWithPriority.TryGetValue(y, out var yPriority);

                if (xIsHighPriority && yIsHighPriority)
                {
                    return xPriority - yPriority;
                }
                if (xIsHighPriority)
                {
                    return 1;
                }
                if (yIsHighPriority)
                {
                    return -1;
                }
                return string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
            }
        }

        private readonly StringCache stringCache = new StringCache();

        public Graph FromBuild(Build build)
        {
            var runtimeGraph = RuntimeGraph.FromBuild(build);

            var commonGlobalProperties = ComputeCommonGlobalProperties(runtimeGraph);

            var graph = new Graph { Attr = { LayerSeparation = 100 } };

            foreach (var root in runtimeGraph.SortedRoots)
            {
                RecursiveAddNode(root, graph, commonGlobalProperties);
            }

            AddVirtualBuildRequestNodes(graph);

            if (commonGlobalProperties.Any())
            {
                AddNodeForCommonGlobalProperties(commonGlobalProperties, graph);
            }


            return graph;
        }

        private void AddVirtualBuildRequestNodes(Graph graph)
        {
            var rootNodes = graph.Nodes.Where(n => !n.InEdges.Any()).ToArray();

            foreach (var rootNode in rootNodes)
            {
                if (rootNode.UserData != null && rootNode.UserData is Project project)
                {
                    var node = graph.AddNode("Build Request");
                    node.Attr.Color = Color.Gray;
                    node.Label.FontColor = Color.Gray;

                    var edge = new Edge(node, rootNode, ConnectionToGraph.Connected);
                    node.AddOutEdge(edge);

                    edge.LabelText = GetTargetString(project);
                    edge.Label.FontColor = Color.Gray;
                }
            }
        }

        private void RecursiveAddNode(RuntimeGraph.RuntimeGraphNode node, Graph graph, IDictionary<string, string> commonGlobalProperties)
        {
            AddNodeAndDirectChildrenToGraph(node, graph, commonGlobalProperties);

            foreach (var child in node.SortedChildren)
            {
                RecursiveAddNode(child, graph, commonGlobalProperties);
            }
        }

        private void AddNodeAndDirectChildrenToGraph(RuntimeGraph.RuntimeGraphNode node, Graph graph, IDictionary<string, string> commonGlobalProperties)
        {
            var msaglNode = GetMsaglNode(node.Project, graph, commonGlobalProperties);

            Node previousMsaglNode = null;

            foreach (var child in node.SortedChildren)
            {
                var currentMsaglNode = GetMsaglNode(child.Project, graph, commonGlobalProperties);

                // ensure left to right ordering
                if (previousMsaglNode != null)
                {
                    graph.LayerConstraints.AddLeftRightConstraint(previousMsaglNode, currentMsaglNode);
                }

                // add edge
                var edge = new Edge(msaglNode, currentMsaglNode, ConnectionToGraph.Connected);
                msaglNode.AddOutEdge(edge);
                edge.LabelText = GetTargetString(child.Project);

                previousMsaglNode = currentMsaglNode;
            }
        }

        private static IDictionary<string, string> ComputeCommonGlobalProperties(RuntimeGraph runtimeGraph)
        {
            if (runtimeGraph.Nodes.Count == 0)
            {
                return ImmutableDictionary<string, string>.Empty;
            }

            // The common global properties are included in all projects. So take the global properties from the first project and prune the uncommon ones.
            var commonGlobalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // solution nodes are not a good template for common global properties because they set many verbose global properties on their references
            var meaningfulNodes = runtimeGraph.Nodes.Where(n => !n.Project.ProjectFile.EndsWith(".sln")).ToArray();

            var templateNode = meaningfulNodes.FirstOrDefault();

            if (templateNode?.Project.GlobalProperties == null || templateNode.Project.GlobalProperties.Count == 0)
            {
                return ImmutableDictionary<string, string>.Empty;
            }

            foreach (var globalProperty in templateNode.Project.GlobalProperties)
            {
                commonGlobalProperties[globalProperty.Key] = globalProperty.Value;
            }

            foreach (var node in meaningfulNodes.Skip(1))
            {
                var nodeProperties = node.Project.GlobalProperties;
                var keysToRemove = new HashSet<string>();

                foreach (var commonGlobalProperty in commonGlobalProperties)
                {
                    if (
                        !(nodeProperties.TryGetValue(commonGlobalProperty.Key, out var commonValue)
                          && commonValue.Equals(commonGlobalProperty.Value, StringComparison.Ordinal)))
                    {
                        keysToRemove.Add(commonGlobalProperty.Key);
                    }
                }

                foreach (var key in keysToRemove)
                {
                    commonGlobalProperties.Remove(key);
                }
            }

            return commonGlobalProperties.ToImmutableDictionary();
        }

        private static void AddNodeForCommonGlobalProperties(IDictionary<string, string> commonGlobalProperties, Graph graph)
        {
            var graphRoots = graph.Nodes.Where(n => !n.InEdges.Any());

            var commonGlobalPropertiesNode = graph.AddNode("CommonGlobalProperties");

            // Place the common prop node (which can get very large with solutions) on top of root nodes, to avoid it being placed in the middle of the graph.
            foreach (var root in graphRoots)
            {
                graph.LayerConstraints.AddUpDownConstraint(commonGlobalPropertiesNode, root);
            }

            var sb = new StringBuilder();

            var title = "Common Global Properties for non solution nodes";
            sb.AppendLine(title);
            sb.AppendLine(new string('-', title.Length));

            WriteGlobalPropertyDictionaryToStringBuilder(commonGlobalProperties, sb);

            StyleProjectNode(commonGlobalPropertiesNode, sb.ToString());
        }

        private static string GetTargetString(Project project)
        {
            return project.EntryTargets.Count == 0
                ? "<default targets>"
                : string.Join(";", project.EntryTargets);
        }

        private Node GetMsaglNode(Project project, Graph graph, IDictionary<string, string> commonGlobalProperties)
        {
            if (project == null)
            {
                var node = graph.FindNode("EntryNode");

                if (node == null)
                {
                    var entryNode = graph.AddNode("EntryNode");
                    entryNode.Attr.Shape = Shape.Point;
                    return entryNode;
                }

                return node;
            }
            else
            {
                var nodeId = GetProjectInvocationIdAsDurableString(project);

                var node = graph.FindNode(nodeId);

                if (node == null)
                {
                    node = graph.AddNode(nodeId);

                    node.UserData = project;

                    var sb = new StringBuilder();

                    var nodeTitle = Path.GetFileName(project.ProjectFile);

                    sb.AppendLine(nodeTitle);
                    sb.AppendLine(new string('-', nodeTitle.Length));

                    WriteGlobalPropertyDictionaryToStringBuilder(
                        project.GlobalProperties
                            // exclude common global properties, they just get repeated on each node and needlessly bloat the graph
                            .Where(
                                kvp =>
                                    !(commonGlobalProperties.TryGetValue(
                                        kvp.Key,
                                        out var commonValue)
                                      && commonValue.Equals(kvp.Value, StringComparison.Ordinal)))
                            .OrderByDescending(kvp => kvp.Key, GlobalPropertyComparer.Instance),
                        sb);

                    StyleProjectNode(node, sb.ToString());
                }

                return node;
            }
        }

        private static void StyleProjectNode(Node node, string labelText)
        {
            node.Attr.LabelMargin = 10;
            node.Label.FontName = "Consolas";
            node.LabelText = labelText;
        }

        private static void WriteGlobalPropertyDictionaryToStringBuilder(
            IEnumerable<KeyValuePair<string, string>> globalProperties,
            StringBuilder sb)
        {
            foreach (var globalProperty in globalProperties)
            {
                sb.AppendLine($"{globalProperty.Key} = {globalProperty.Value}");
            }
        }


        private string GetProjectInvocationIdAsDurableString(Project invocation)
        {
            return stringCache.Intern(invocation.Id.ToString());
        }
    }
}

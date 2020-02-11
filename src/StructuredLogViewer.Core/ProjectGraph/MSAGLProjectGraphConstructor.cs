// --------------------------------------------------------------------
// 
// Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// --------------------------------------------------------------------

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
    public class MSAGLProjectGraphConstructor
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
            var projectInvocations = build.FindChildrenRecursive<Project>();

            var commonGlobalProperties = ComputeCommonGlobalProperties(projectInvocations);

            var graph = new Graph {Attr = {LayerSeparation = 100}};

            if (commonGlobalProperties.Any())
            {
                AddNodeForCommonGlobalProperties(commonGlobalProperties, graph);
            }

            foreach (var projectInvocation in projectInvocations)
            {
                AddNodeForProjectInvocation(projectInvocation, graph, commonGlobalProperties);
            }

            return graph;
        }

        private void AddNodeForProjectInvocation(Project projectInvocation, Graph graph, IDictionary<string, string> commonGlobalProperties)
        {
            var sourceNode = GetNodeForProjectInvocation(projectInvocation.GetNearestParent<Project>(), graph, commonGlobalProperties);
            var targetNode = GetNodeForProjectInvocation(projectInvocation, graph, commonGlobalProperties);

            graph.AddEdge(sourceNode.Id, GetTargetString(projectInvocation), targetNode.Id);
        }

        private static IDictionary<string, string> ComputeCommonGlobalProperties(IReadOnlyList<Project> projectInvocations)
        {
            if (projectInvocations.Count == 0)
            {
                return ImmutableDictionary<string, string>.Empty;
            }

            // The common global properties are included in all projects. So take the global properties from the first project and prune the uncommon ones.
            var commonGlobalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var globalProperty in projectInvocations.First(project => !project.ProjectFile.EndsWith(".sln")).GlobalProperties)
            {
                commonGlobalProperties[globalProperty.Key] = globalProperty.Value;
            }

            foreach (var projectInvocation in projectInvocations.Skip(1))
            {
                foreach (var globalProperty in projectInvocation.GlobalProperties)
                {
                    if (commonGlobalProperties.TryGetValue(globalProperty.Key, out var value)
                        && !value.Equals(globalProperty.Value, StringComparison.Ordinal))
                    {
                        commonGlobalProperties.Remove(globalProperty.Key);
                    }
                }
            }

            return commonGlobalProperties.ToImmutableDictionary();
        }

        private static void AddNodeForCommonGlobalProperties(IDictionary<string, string> commonGlobalProperties, Graph graph)
        {
            var node = graph.AddNode("CommonGlobalProperties");

            var sb = new StringBuilder();

            sb.AppendLine("Common Global Properties");
            sb.AppendLine("------------------------");
            WriteGlobalPropertyDictionaryToStringBuilder(commonGlobalProperties, sb);

            StyleProjectNode(node, sb.ToString());
        }

        private static string GetTargetString(Project project)
        {
            return project.EntryTargets.Count == 0
                ? "<default targets>"
                : string.Join(";", project.EntryTargets);
        }

        private Node GetNodeForProjectInvocation(Project project, Graph graph, IDictionary<string, string> commonGlobalProperties)
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
                            .Where(kvp => !commonGlobalProperties.ContainsKey(kvp.Key))
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

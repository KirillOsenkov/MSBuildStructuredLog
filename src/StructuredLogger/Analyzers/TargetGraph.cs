using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using StructuredLogViewer;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class TargetGraphManager : ISearchExtension
    {
        public Build Build { get; }
        public Func<ProjectEvaluation, string> TextProvider { get; set; }

        public bool AugmentOtherResults => true;

        public TargetGraphManager(Build build)
        {
            Build = build;
        }

        private Dictionary<ProjectEvaluation, TargetGraph> graphs = new();

        private void AddTargetGraph(ProjectEvaluation evaluation, TargetGraph graph)
        {
            graphs[evaluation] = graph;
        }

        private TargetGraph TryGetTargetGraph(ProjectEvaluation evaluation)
        {
            graphs.TryGetValue(evaluation, out var graph);
            return graph;
        }

        public TargetGraph GetTargetGraph(ProjectEvaluation evaluation)
        {
            if (TryGetTargetGraph(evaluation) is TargetGraph graph)
            {
                return graph;
            }

            var preprocessedText = this.TextProvider?.Invoke(evaluation);
            if (preprocessedText == null)
            {
                return null;
            }

            var properties = evaluation.GetProperties();

            graph = TargetGraph.ParseXml(preprocessedText, properties);
            AddTargetGraph(evaluation, graph);
            return graph;
        }

        bool ISearchExtension.TryGetResults(NodeQueryMatcher matcher, IList<SearchResult> resultCollector, int maxResults)
        {
            if (!string.Equals(matcher.TypeKeyword, "target", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (resultCollector.Count != 1 || resultCollector[0].Node is not Target target)
            {
                return false;
            }

            if (target.Project == null || target.Project.GetEvaluation(this.Build) is not ProjectEvaluation evaluation)
            {
                return false;
            }

            var graph = GetTargetGraph(evaluation);
            if (graph == null)
            {
                return false;
            }

            var node = graph.GetTarget(target.Name);
            if (node == null)
            {
                return false;
            }

            var before = graph.AllTargets.Where(t => t.BeforeTargets.Contains(node)).ToArray();
            var depends = graph.AllTargets.Where(t => t.DependsOnTargets.Contains(node)).ToArray();
            var after = graph.AllTargets.Where(t => t.AfterTargets.Contains(node)).ToArray();

            if (before.Length == 0 && depends.Length == 0 && after.Length == 0)
            {
                return false;
            }

            var graphFolder = new Folder { Name = "Target Graph" };

            if (before.Length > 0)
            {
                var beforeFolder = new Folder { Name = "BeforeTargets" };
                Add(beforeFolder, before);
                graphFolder.AddChild(beforeFolder);
            }

            if (depends.Length > 0)
            {
                var dependsFolder = new Folder { Name = $"Targets that depend on {target.Name}" };
                Add(dependsFolder, depends);
                graphFolder.AddChild(dependsFolder);
            }

            if (after.Length > 0)
            {
                var afterFolder = new Folder { Name = "AfterTargets" };
                Add(afterFolder, after);
                graphFolder.AddChild(afterFolder);
            }

            resultCollector.Add(new SearchResult(graphFolder));

            void Add(Folder folder, IEnumerable<TargetNode> targets)
            {
                foreach (var target in targets)
                {
                    var child = new Target { Name = target.Name };
                    folder.AddChild(child);
                }
            }

            return true;
        }
    }

    public class TargetNode
    {
        public string Name;
        public List<TargetNode> BeforeTargets = new();
        public List<TargetNode> AfterTargets = new();
        public List<TargetNode> DependsOnTargets = new();

        public IEnumerable<TargetNode> EnumerateDependentTargets => BeforeTargets.Concat(DependsOnTargets).Concat(AfterTargets);
    }

    public class TargetGraph
    {
        private Dictionary<string, TargetNode> targetNodes = new(StringComparer.OrdinalIgnoreCase);

        private List<string> rootTargets = new List<string>();

        public IReadOnlyList<string> RootTargets => rootTargets;

        public IEnumerable<TargetNode> AllTargets => targetNodes.Values;

        public static TargetGraph ParseXml(string text, Dictionary<string, string> props)
        {
            var result = new TargetGraph();

            var xdoc = XDocument.Parse(text);
            var targets = xdoc
                .DescendantNodes()
                .OfType<XElement>()
                .Where(element => element.Name.LocalName == "Target")
                .ToArray();

            var project = xdoc.Root;
            if (project != null)
            {
                var defaultTargetsText = GetAttribute(project, "DefaultTargets");
                var initialTargetsText = GetAttribute(project, "InitialTargets");

                var defaultTargets = Expand(defaultTargetsText, props);
                var initialTargets = Expand(initialTargetsText, props);

                if (initialTargets.Any())
                {
                    result.rootTargets.AddRange(initialTargets);
                }
                else if (defaultTargets.Any())
                {
                    result.rootTargets.AddRange(defaultTargets);
                }
                else if (targets.FirstOrDefault() is { } firstTarget)
                {
                    var name = GetAttribute(firstTarget, "Name");
                    result.rootTargets.Add(name);
                }
            }

            var targetNodes = result.targetNodes;

            foreach (var target in targets)
            {
                var name = GetAttribute(target, "Name");
                var beforeTargetsText = GetAttribute(target, "BeforeTargets");
                var afterTargetsText = GetAttribute(target, "AfterTargets");
                var dependsOnTargetsText = GetAttribute(target, "DependsOnTargets");

                var beforeTargets = Expand(beforeTargetsText, props);
                var afterTargets = Expand(afterTargetsText, props);
                var dependsOnTargets = Expand(dependsOnTargetsText, props);

                var node = result.GetTarget(name);

                foreach (var before in beforeTargets)
                {
                    var dest = result.GetTarget(before);
                    dest.BeforeTargets.Insert(0, node);
                }

                foreach (var after in afterTargets)
                {
                    var dest = result.GetTarget(after);
                    dest.AfterTargets.Insert(0, node);
                }

                foreach (var dep in dependsOnTargets)
                {
                    var dest = result.GetTarget(dep);
                    node.DependsOnTargets.Add(dest);
                }
            }

            return result;
        }

        public TargetNode GetTarget(string text)
        {
            if (!targetNodes.TryGetValue(text, out var node))
            {
                node = new TargetNode { Name = text };
                targetNodes.Add(text, node);
            }

            return node;
        }

        private static string[] Expand(string expression, Dictionary<string, string> props)
        {
            if (expression == null)
            {
                return Array.Empty<string>();
            }

            if (expression.Contains("$("))
            {
                foreach (var kvp in props)
                {
                    expression = expression.Replace("$(" + kvp.Key + ")", kvp.Value);
                }
            }

            return expression.Split([';'], StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray();
        }

        private static string GetAttribute(XElement element, string attributeName)
        {
            return element.Attribute(XName.Get(attributeName))?.Value;
        }

        public IEnumerable<(string targetName, string relationship)> FindPathFromEntryTargets(
            string targetToFind,
            IEnumerable<string> entryTargets)
        {
            var visited = new HashSet<TargetNode>();
            var stack = new Stack<(string targetName, string relationship)>();

            VisitTargets(entryTargets.Select(GetTarget), stack, visited, target =>
            {
                if (string.Equals(target.Name, targetToFind, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                return true;
            }, "Entry");

            return stack;
        }

        private bool VisitTargets(
            IEnumerable<TargetNode> targets,
            Stack<(string targetName, string relationship)> stack,
            HashSet<TargetNode> visited,
            Func<TargetNode, bool> shouldContinue,
            string relationship)
        {
            foreach (var target in targets)
            {
                stack.Push((target.Name, relationship));
                var result = VisitTarget(target, stack, visited, shouldContinue);
                if (!result)
                {
                    return false;
                }

                stack.Pop();
            }

            return true;
        }

        private bool VisitTarget(
            TargetNode target,
            Stack<(string targetName, string relationship)> stack,
            HashSet<TargetNode> visited,
            Func<TargetNode, bool> shouldContinue)
        {
            if (!visited.Add(target))
            {
                return true;
            }

            if (!shouldContinue(target))
            {
                return false;
            }

            if (!VisitTargets(target.BeforeTargets, stack, visited, shouldContinue, "Before"))
            {
                return false;
            }

            if (!VisitTargets(target.DependsOnTargets, stack, visited, shouldContinue, "DependsOn"))
            {
                return false;
            }

            if (!VisitTargets(target.AfterTargets, stack, visited, shouldContinue, "After"))
            {
                return false;
            }

            return true;
        }

        public string ToEdotorString()
        {
            var sb = new StringBuilder();
            sb.AppendLine("digraph d {");

            foreach (var targetNode in targetNodes)
            {
                var node = targetNode.Value;
                var name = node.Name;
                sb.AppendLine($"{name}");

                foreach (var before in node.BeforeTargets)
                {
                    sb.AppendLine($"{name} -> {before.Name} [label = before]");
                }

                foreach (var after in node.AfterTargets)
                {
                    sb.AppendLine($"{name} -> {after.Name} [label = after]");
                }

                foreach (var depends in node.DependsOnTargets)
                {
                    sb.AppendLine($"{name} -> {depends.Name} [label = depends]");
                }
            }

            sb.AppendLine("}");

            return sb.ToString();
        }
    }
}

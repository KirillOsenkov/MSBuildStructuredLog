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
            if (evaluation == null)
            {
                return null;
            }

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

            string targetName = null;
            ProjectEvaluation evaluation = null;
            TargetGraph graph = null;

            if (resultCollector.Count == 0)
            {
                var projectMatcher = matcher.ProjectMatchers.FirstOrDefault();
                if (projectMatcher == null)
                {
                    return false;
                }

                var allEvaluations = Build.EvaluationFolder?.Children.OfType<ProjectEvaluation>();
                var matchingProjects = allEvaluations
                    .Where(e => projectMatcher.IsMatch(e.Name) is { } match && match != SearchResult.EmptyQueryMatch)
                    .ToArray();
                if (matchingProjects.Select(e => e.SourceFilePath).Distinct(StringComparer.OrdinalIgnoreCase).Count() != 1)
                {
                    return false;
                }

                evaluation = matchingProjects.LastOrDefault();
                graph = GetTargetGraph(evaluation);
                targetName = graph.GetTarget(matcher.Terms[0].Word).Name;
            }
            else if (resultCollector.All(t => t.Node is Target) && resultCollector.Select(t => ((Target)t.Node).Name).Distinct().Count() == 1)
            {
                var target = resultCollector.Select(n => n.Node as Target).FirstOrDefault();
                if (target == null || target.Project == null)
                {
                    return false;
                }

                evaluation = target.Project.GetEvaluation(Build);
                graph = GetTargetGraph(evaluation);
                targetName = target.Name;
            }

            if (targetName == null || evaluation == null || graph == null)
            {
                return false;
            }

            var node = graph.GetTarget(targetName);
            if (node == null)
            {
                return false;
            }

            var before = graph.AllTargets.Where(t => t.BeforeTargets.Contains(node)).ToArray();
            var depends = graph.AllTargets.Where(t => t.DependsOnTargets.Contains(node)).ToArray();
            var after = graph.AllTargets.Where(t => t.AfterTargets.Contains(node)).ToArray();

            if (before.Length == 0 &&
                depends.Length == 0 &&
                after.Length == 0 &&
                node.BeforeTargets.Count == 0 &&
                node.DependsOnTargets.Count == 0 &&
                node.AfterTargets.Count == 0)
            {
                return false;
            }

            var graphFolder = new Folder { Name = "Target Graph", IsExpanded = true };

            if (before.Length > 0)
            {
                var folder = new Folder { Name = $"BeforeTargets" };
                Add(folder, before.Reverse());
                graphFolder.AddChild(folder);
            }

            if (after.Length > 0)
            {
                var folder = new Folder { Name = $"AfterTargets" };
                Add(folder, after.Reverse());
                graphFolder.AddChild(folder);
            }

            if (node.DependsOnTargets.Count > 0)
            {
                var folder = new Folder { Name = "DependsOnTargets" };
                Add(folder, node.DependsOnTargets);
                graphFolder.AddChild(folder);
            }

            if (node.BeforeTargets.Count > 0)
            {
                var folder = new Folder { Name = $"Targets that run before {targetName}" };
                Add(folder, node.BeforeTargets.OrderBy(s => s.Name));
                graphFolder.AddChild(folder);
            }

            if (node.AfterTargets.Count > 0)
            {
                var folder = new Folder { Name = $"Targets that run after {targetName}" };
                Add(folder, node.AfterTargets.OrderBy(s => s.Name));
                graphFolder.AddChild(folder);
            }

            if (depends.Length > 0)
            {
                var folder = new Folder { Name = $"Targets that depend on {targetName}" };
                Add(folder, depends.OrderBy(s => s.Name));
                graphFolder.AddChild(folder);
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

                var node = result.GetOrCreateTarget(name);

                foreach (var before in beforeTargets)
                {
                    var dest = result.GetOrCreateTarget(before);
                    dest.BeforeTargets.Insert(0, node);
                }

                foreach (var after in afterTargets)
                {
                    var dest = result.GetOrCreateTarget(after);
                    dest.AfterTargets.Insert(0, node);
                }

                foreach (var dep in dependsOnTargets)
                {
                    var dest = result.GetOrCreateTarget(dep);
                    node.DependsOnTargets.Add(dest);
                }
            }

            return result;
        }

        public TargetNode GetTarget(string name)
        {
            targetNodes.TryGetValue(name, out var result);
            return result;
        }

        public TargetNode GetOrCreateTarget(string name)
        {
            if (!targetNodes.TryGetValue(name, out var node))
            {
                node = new TargetNode { Name = name };
                targetNodes.Add(name, node);
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

            return
                TextUtilities.SplitSemicolonDelimitedList(expression)
                .Select(s => s.Trim('\r', '\n', ' ', '\t'))
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToArray();
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

            VisitTargets(entryTargets.Select(GetOrCreateTarget), stack, visited, target =>
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

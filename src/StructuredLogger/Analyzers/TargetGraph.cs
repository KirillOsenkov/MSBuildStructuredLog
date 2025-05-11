using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class TargetGraphManager
    {
        public Build Build { get; }

        public TargetGraphManager(Build build)
        {
            Build = build;
        }

        private Dictionary<ProjectEvaluation, TargetGraph> graphs = new();

        public void AddTargetGraph(ProjectEvaluation evaluation, TargetGraph graph)
        {
            graphs[evaluation] = graph;
        }

        public TargetGraph GetTargetGraph(ProjectEvaluation evaluation)
        {
            graphs.TryGetValue(evaluation, out var graph);
            return graph;
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

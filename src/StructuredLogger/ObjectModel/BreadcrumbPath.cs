using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public enum BreadcrumbPathResult
    {
        Found,
        Invalid,
        NotFound,
        Ambiguous
    }

    public static class BreadcrumbPath
    {
        private const string Header = "MSBuild path v1";

        public static string Create(BaseNode node)
        {
            if (node?.GetRoot() is not Build)
            {
                return null;
            }

            var text = new StringBuilder(Header);
            foreach (var part in node.GetParentChainIncludingThis())
            {
                text.AppendLine();
                text.Append(part.TypeName);
                text.Append(": ");
                text.Append(Escape(GetIdentity(part)));
            }

            return text.ToString();
        }

        public static BreadcrumbPathResult Resolve(Build build, string text, out BaseNode node)
        {
            node = null;
            if (build == null || string.IsNullOrEmpty(text))
            {
                return BreadcrumbPathResult.Invalid;
            }

            var lines = text.TrimEnd('\r', '\n').Replace("\r\n", "\n").Split('\n');
            if (lines[0] != Header)
            {
                return BreadcrumbPathResult.Invalid;
            }

            var steps = new List<KeyValuePair<string, string>>();
            foreach (var line in lines.Skip(1))
            {
                var separator = line.IndexOf(": ", StringComparison.Ordinal);
                if (separator <= 0 || !TryUnescape(line.Substring(separator + 2), out var name))
                {
                    return BreadcrumbPathResult.Invalid;
                }

                steps.Add(new KeyValuePair<string, string>(line.Substring(0, separator), name));
            }

            var candidates = new List<BaseNode> { build };
            for (var i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                var matches = new List<BaseNode>();
                foreach (var parent in candidates.OfType<TreeNode>())
                {
                    foreach (var child in parent.Children)
                    {
                        if (Matches(child, step.Key, step.Value))
                        {
                            matches.Add(child);
                            if (i == steps.Count - 1 && matches.Count == 2)
                            {
                                return BreadcrumbPathResult.Ambiguous;
                            }
                        }
                    }
                }

                if (matches.Count == 0)
                {
                    return BreadcrumbPathResult.NotFound;
                }

                candidates = matches;
            }

            node = candidates[0];
            return BreadcrumbPathResult.Found;
        }

        private static bool Matches(BaseNode node, string type, string name)
        {
            if (!string.Equals(node.TypeName, type, StringComparison.Ordinal))
            {
                return false;
            }

            var identity = GetIdentity(node);
            if (node is Project or ProjectEvaluation)
            {
                identity = identity.Replace('\\', '/');
                name = name.Replace('\\', '/');
            }

            return string.Equals(identity, name, StringComparison.OrdinalIgnoreCase);
        }

        private static string GetIdentity(BaseNode node) => node switch
        {
            Project project => string.IsNullOrEmpty(project.ProjectFile) ? project.Name ?? "" : project.ProjectFile,
            ProjectEvaluation evaluation => string.IsNullOrEmpty(evaluation.ProjectFile) ? evaluation.Name ?? "" : evaluation.ProjectFile,
            NameValueNode value => value.Name ?? "",
            NamedNode named => named.Name ?? "",
            TextNode text => text.Text ?? "",
            _ => node.Title ?? node.TypeName
        };

        private static string Escape(string text) => text
            .Replace("%", "%25")
            .Replace("\0", "%00")
            .Replace("\r", "%0D")
            .Replace("\n", "%0A");

        private static bool TryUnescape(string text, out string value)
        {
            var result = new StringBuilder();
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] != '%')
                {
                    result.Append(text[i]);
                    continue;
                }

                if (i + 2 >= text.Length)
                {
                    value = null;
                    return false;
                }

                var code = text.Substring(i + 1, 2);
                switch (code)
                {
                    case "25": result.Append('%'); break;
                    case "00": result.Append('\0'); break;
                    case "0D": result.Append('\r'); break;
                    case "0A": result.Append('\n'); break;
                    default:
                        value = null;
                        return false;
                }

                i += 2;
            }

            value = result.ToString();
            return true;
        }
    }
}

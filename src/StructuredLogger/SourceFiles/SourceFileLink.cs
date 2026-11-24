using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public readonly struct SourceFileLink
    {
        private static readonly Regex quotedPath = new Regex(
            "\"(?<Path>[^\"\\r\\n]+)\"|(?<!\\w)'(?<Path>[^'\\r\\n]+)'(?!\\w)",
            RegexOptions.Compiled);

        private static readonly Regex fileLocation = new Regex(
            @"^(?<File>.+?)\s*\(\s*(?<Line>[0-9]+)\s*,\s*(?<Column>[0-9]+)\s*\)$",
            RegexOptions.Compiled);

        // Some translations reorder the paths or leave them unquoted.
        private static readonly Regex[] overridingTargetPatterns = StringsSet.ResourcesCollection.Keys
            .Select(culture => Strings.CreateRegex(new StringsSet(culture).GetString("OverridingTarget"), 4, capture: true))
            .ToArray();

        public int Start { get; }
        public int Length => Text.Length;
        public string Text { get; }
        public string FilePath { get; }
        public int LineNumber { get; }
        public int ColumnNumber { get; }

        private SourceFileLink(Group group)
        {
            Start = group.Index;
            Text = group.Value;
            FilePath = Text;
            LineNumber = 0;
            ColumnNumber = 0;

            var location = fileLocation.Match(Text);
            if (location.Success
                && int.TryParse(location.Groups["Line"].Value, out var line) && line > 0
                && int.TryParse(location.Groups["Column"].Value, out var column) && column > 0)
            {
                FilePath = location.Groups["File"].Value.TrimEnd();
                LineNumber = line;
                ColumnNumber = column;
            }
        }

        public static IEnumerable<SourceFileLink> Find(string text, ISourceFileResolver resolver, int maxLength = int.MaxValue)
        {
            if (string.IsNullOrEmpty(text) || resolver == null || (text.IndexOf('\\') < 0 && text.IndexOf('/') < 0))
            {
                yield break;
            }

            foreach (var pattern in overridingTargetPatterns)
            {
                var match = pattern.Match(text);
                if (!match.Success)
                {
                    continue;
                }

                foreach (var group in new[] { match.Groups["group1"], match.Groups["group3"] }.OrderBy(g => g.Index))
                {
                    var link = new SourceFileLink(group);
                    if (group.Length > 0 && group.Index + group.Length <= maxLength && resolver.GetSourceFileText(link.FilePath) != null)
                    {
                        yield return link;
                    }
                }

                yield break;
            }

            foreach (Match match in quotedPath.Matches(text))
            {
                var group = match.Groups["Path"];
                if (group.Index + group.Length > maxLength)
                {
                    yield break;
                }

                var link = new SourceFileLink(group);
                var path = link.FilePath;
                if ((path.IndexOf('\\') >= 0 || path.IndexOf('/') >= 0) && IsSourceFile(path) && resolver.GetSourceFileText(path) != null)
                {
                    yield return link;
                }
            }
        }

        private static bool IsSourceFile(string path)
        {
            // Avoid loading binary task/reference assemblies while rendering messages.
            var dot = path.LastIndexOf('.');
            if (dot < 0)
            {
                return false;
            }

            var extension = path.Substring(dot);
            return extension.EndsWith("proj", StringComparison.OrdinalIgnoreCase)
                || sourceExtensions.Contains(extension);
        }

        private static readonly HashSet<string> sourceExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".targets", ".props", ".sln", ".slnx", ".cs", ".vb", ".fs", ".rsp",
            ".config", ".xml", ".xaml", ".json", ".txt"
        };
    }
}

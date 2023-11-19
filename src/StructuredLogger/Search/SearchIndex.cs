using System;
using System.Collections.Generic;
using System.Threading;
using StructuredLogViewer;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class SearchIndex
    {
        private readonly Build build;

        private readonly string[] strings;
        private int stringCount;

        private Dictionary<string, int> stringToIndexMap = new Dictionary<string, int>();

        private List<NodeEntry> nodeEntries = new List<NodeEntry>();

        public SearchIndex(Build build)
        {
            this.build = build;

            var stringInstances = (ICollection<string>)build.StringTable.Instances;
            stringCount = stringInstances.Count;
            strings = new string[stringCount];

            stringInstances.CopyTo(strings, 0);

            for (int i = 0; i < stringCount; i++)
            {
                var stringInstance = strings[i];
                stringToIndexMap[stringInstance] = i;
            }

            Visit(build);
        }

        private int GetStringIndex(string text)
        {
            stringToIndexMap.TryGetValue(text, out int index);
            return index;
        }

        private void Visit(TreeNode node)
        {
            var entry = GetEntry(node);
            nodeEntries.Add(entry);

            if (node.HasChildren)
            {
                var children = node.Children;
                for (int i = 0; i < children.Count; i++)
                {
                    var child = children[i];
                    if (child is TreeNode childNode)
                    {
                        Visit(childNode);
                    }
                }
            }
        }

        private NodeEntry GetEntry(TreeNode node)
        {
            var entry = new NodeEntry(node);

            var fields = NodeQueryMatcher.PopulateSearchFields(node);
            int count = fields.count;

            entry.Field1 = GetStringIndex(fields.array[0]);
            if (count > 1)
            {
                entry.Field2 = GetStringIndex(fields.array[1]);
                if (count > 2)
                {
                    entry.Field3 = GetStringIndex(fields.array[2]);
                    if (count > 3)
                    {
                        entry.Field4 = GetStringIndex(fields.array[3]);
                        if (count > 4)
                        {
                            entry.Field5 = GetStringIndex(fields.array[4]);
                            if (count > 5)
                            {
                                entry.Field6 = GetStringIndex(fields.array[5]);
                            }
                        }
                    }
                }
            }

            return entry;
        }

        public IEnumerable<SearchResult> FindNodes(string query, CancellationToken cancellationToken)
        {
            List<SearchResult> results = new List<SearchResult>();

            var matcher = new NodeQueryMatcher(query, strings, cancellationToken, precomputeMatchesInStrings: false);

            for (int i = 0; i < nodeEntries.Count; i++)
            {
                var entry = nodeEntries[i];

                if (matcher.IsMatch(entry) is { } searchResult)
                {
                    results.Add(searchResult);
                }
            }

            return results;
        }
    }

    internal class NodeEntry
    {
        public NodeEntry(BaseNode node)
        {
            Node = node;
        }

        public BaseNode Node { get; }

        public int Field1;
        public int Field2;
        public int Field3;
        public int Field4;
        public int Field5;
        public int Field6;
    }
}

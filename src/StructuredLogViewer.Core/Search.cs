using System.Collections.Generic;
using Microsoft.Build.Logging.StructuredLogger;

namespace StructuredLogViewer
{
    public class Search
    {
        public const int DefaultMaxResults = 1000;

        private readonly Build build;
        private readonly int maxResults;
        private List<SearchResult> resultSet;

        public Search(Build build, int maxResults = DefaultMaxResults)
        {
            this.build = build;
            this.maxResults = maxResults;
        }

        public IEnumerable<SearchResult> FindNodes(string query)
        {
            var matcher = new NodeQueryMatcher(query, build.StringTable.Instances);

            resultSet = new List<SearchResult>();
            Visit(build, matcher);

            return resultSet;
        }

        public static void ClearSearchResults(Build build)
        {
            build.VisitAllChildren<BaseNode>(node =>
            {
                node.IsSearchResult = false;
                node.ContainsSearchResult = false;
            });
        }

        private bool Visit(object node, NodeQueryMatcher matcher)
        {
            var isMatch = false;
            var containsMatch = false;

            if (resultSet.Count < maxResults)
            {
                var result = matcher.IsMatch(node);
                if (result != null)
                {
                    isMatch = true;
                    resultSet.Add(result);
                }
            }

            switch (node)
            {
                case TreeNode treeNode:
                {
                    treeNode.IsSearchResult = isMatch;

                    if (treeNode.HasChildren)
                    {
                        foreach (var child in treeNode.Children)
                        {
                            containsMatch |= Visit(child, matcher);
                        }
                    }

                    treeNode.ContainsSearchResult = containsMatch;
                    break;
                }

                case BaseNode baseNode:
                {
                    baseNode.IsSearchResult = isMatch;
                    baseNode.ContainsSearchResult = false;
                    break;
                }
            }

            return isMatch || containsMatch;
        }
    }
}

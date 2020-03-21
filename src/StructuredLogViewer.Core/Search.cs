using System.Collections.Generic;
using System.Threading;
using Microsoft.Build.Logging.StructuredLogger;

namespace StructuredLogViewer
{
    public class Search
    {
        public const int DefaultMaxResults = 1000;

        private readonly Build build;
        private readonly int maxResults;
        private List<SearchResult> resultSet;
        private bool markResultsInTree = false;

        public Search(Build build, int maxResults)
        {
            this.build = build;
            this.maxResults = maxResults;
            this.markResultsInTree = SettingsService.MarkResultsInTree;
        }

        public IEnumerable<SearchResult> FindNodes(string query, CancellationToken cancellationToken)
        {
            var matcher = new NodeQueryMatcher(query, build.StringTable.Instances);

            resultSet = new List<SearchResult>();
            Visit(build, matcher, cancellationToken);

            return resultSet;
        }

        public static void ClearSearchResults(Build build)
        {
            if (!SettingsService.MarkResultsInTree)
            {
                return;
            }

            build.VisitAllChildren<BaseNode>(node =>
            {
                node.IsSearchResult = false;
                node.ContainsSearchResult = false;
            });
        }

        private bool Visit(object node, NodeQueryMatcher matcher, CancellationToken cancellationToken)
        {
            var isMatch = false;
            var containsMatch = false;

            if (cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            if (resultSet.Count < maxResults)
            {
                var result = matcher.IsMatch(node);
                if (result != null)
                {
                    isMatch = true;
                    resultSet.Add(result);
                }
            }
            else if (!markResultsInTree)
            {
                // we save a lot of time if we don't have to visit the entire tree to mark results
                // after we've found maximum allowed results
                return false;
            }

            if (node is TreeNode treeNode && treeNode.HasChildren)
            {
                foreach (var child in treeNode.Children)
                {
                    containsMatch |= Visit(child, matcher, cancellationToken);
                }
            }

            // setting these flags on each node is expensive so do it only if the feature is enabled
            if (markResultsInTree && node is BaseNode baseNode)
            {
                baseNode.IsSearchResult = isMatch;
                baseNode.ContainsSearchResult = containsMatch;
            }

            return isMatch || containsMatch;
        }
    }
}

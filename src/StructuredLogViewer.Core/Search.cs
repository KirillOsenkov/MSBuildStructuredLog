using System.Collections.Generic;
using System.Linq;
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

        public Search(Build build, int maxResults = DefaultMaxResults)
        {
            this.build = build;
            this.maxResults = maxResults;
        }

        public IEnumerable<SearchResult> FindNodes(string query)
        {
            var matcher = new NodeQueryMatcher(query, build.StringTable.Instances);

            resultSet = new List<SearchResult>();

            var cts = new CancellationTokenSource();
            build.VisitAllChildren<object>(node => Visit(node, matcher, cts), cts.Token);

            MarkSearchResults(build, resultSet.Select(i => i.Node).OfType<BaseNode>());

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

        private void Visit(object node, NodeQueryMatcher matcher, CancellationTokenSource cancellationTokenSource)
        {
            if (cancellationTokenSource.IsCancellationRequested)
            {
                return;
            }

            if (resultSet.Count >= maxResults)
            {
                cancellationTokenSource.Cancel();
                return;
            }

            var result = matcher.IsMatch(node);
            if (result != null)
            {
                resultSet.Add(result);
            }
        }

        private static void MarkSearchResults(Build build, IEnumerable<BaseNode> searchResults)
        {
            var resultSet = new HashSet<BaseNode>(searchResults);
            var ancestorNodes = new HashSet<BaseNode>();

            foreach (var node in resultSet)
            {
                var current = (node as ParentedNode)?.Parent;
                while (current != null && ancestorNodes.Add(current))
                {
                    current = current.Parent;
                }
            }

            build.VisitAllChildren<BaseNode>(node =>
            {
                node.IsSearchResult = resultSet.Contains(node);
                node.ContainsSearchResult = ancestorNodes.Contains(node);
            });
        }
    }
}

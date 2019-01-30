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

        public Search(Build build, int maxResults = DefaultMaxResults)
        {
            this.build = build;
            this.maxResults = maxResults;
        }

        public IEnumerable<SearchResult> FindNodes(string query)
        {
            ClearSearchResults(build);

            var matcher = new NodeQueryMatcher(query, build.StringTable.Instances);

            resultSet = new List<SearchResult>();

            var cts = new CancellationTokenSource();
            build.VisitAllChildren<object>(node => Visit(node, matcher, cts), cts.Token);

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
                MarkAsSearchResult(node);
            }
        }

        private static void MarkAsSearchResult(object node)
        {
            if (node is BaseNode baseNode)
            {
                baseNode.IsSearchResult = true;

                var current = (baseNode as ParentedNode)?.Parent;
                while (current?.ContainsSearchResult == false)
                {
                    current.ContainsSearchResult = true;
                    current = current.Parent;
                }
            }
        }
    }
}

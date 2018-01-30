using System.Collections.Generic;
using System.Threading;
using Microsoft.Build.Logging.StructuredLogger;

namespace StructuredLogViewer
{
    public class Search
    {
        public const int MaxResults = 1000;

        private Build build;
        private List<SearchResult> resultSet;

        public Search(Build build)
        {
            this.build = build;
        }

        public IEnumerable<SearchResult> FindNodes(string query)
        {
            var matcher = new NodeQueryMatcher(query, build.StringTable.Instances);

            resultSet = new List<SearchResult>();

            var cts = new CancellationTokenSource();
            build.VisitAllChildren<object>(node => Visit(node, matcher, cts), cts.Token);

            return resultSet;
        }

        private void Visit(object node, NodeQueryMatcher matcher, CancellationTokenSource cancellationTokenSource)
        {
            if (cancellationTokenSource.IsCancellationRequested)
            {
                return;
            }

            if (resultSet.Count >= MaxResults)
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
    }
}

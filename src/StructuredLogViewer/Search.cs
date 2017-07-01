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
            var matcher = new NodeQueryMarcher(query, build.StringTable.Instances);

            resultSet = new List<SearchResult>();

            var cts = new CancellationTokenSource();
            build.VisitAllChildren<object>(node => Visit(node, matcher, cts), cts.Token);

            if (matcher.Under != null)
            {
                matcher = new NodeQueryMarcher(matcher.Under, build.StringTable.Instances);

                for (int i = resultSet.Count - 1; i >= 0; i--)
                {
                    var result = resultSet[i];
                    if (!IsUnder(matcher, result))
                    {
                        resultSet.RemoveAt(i);
                    }
                }
            }

            return resultSet;
        }

        private bool IsUnder(NodeQueryMarcher matcher, SearchResult result)
        {
            if (!(result.Node is ParentedNode parented))
            {
                return true;
            }

            foreach (var parent in parented.GetParentChain())
            {
                if (matcher.IsMatch(parent) != null)
                {
                    return true;
                }
            }

            return false;
        }

        private void Visit(object node, NodeQueryMarcher matcher, CancellationTokenSource cancellationTokenSource)
        {
            if (cancellationTokenSource.IsCancellationRequested)
            {
                return;
            }

            if (resultSet.Count > MaxResults)
            {
                cancellationTokenSource.Cancel();
                return;
            }

            var result = matcher.IsMatch(node);
            if (result != null)
            {
                result.Node = node;
                resultSet.Add(result);
            }
        }
    }
}

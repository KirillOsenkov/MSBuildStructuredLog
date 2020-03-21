using System.Collections.Generic;
using System.Threading;
using Microsoft.Build.Logging.StructuredLogger;
using TPLTask = System.Threading.Tasks.Task;

namespace StructuredLogViewer
{
    public class Search
    {
        public const int DefaultMaxResults = 1000;

        private readonly Build build;
        private readonly int maxResults;
        private int resultCount;
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

            var resultSet = new List<SearchResult>();
            Visit(build, matcher, resultSet, cancellationToken);
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

        private bool Visit(object node, NodeQueryMatcher matcher, List<SearchResult> results, CancellationToken cancellationToken)
        {
            var isMatch = false;
            var containsMatch = false;

            if (cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            if (resultCount < maxResults)
            {
                var result = matcher.IsMatch(node);
                if (result != null)
                {
                    isMatch = true;
                    lock (results)
                    {
                        results.Add(result);
                        resultCount++;
                    }
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
                if (node is Project)
                {
                    var tasks = new List<System.Threading.Tasks.Task<List<SearchResult>>>();

                    foreach (var child in treeNode.Children)
                    {
                        var task = TPLTask.Run(() =>
                        {
                            var list = new List<SearchResult>();
                            Visit(child, matcher, list, cancellationToken);
                            return list;
                        });
                        tasks.Add(task);
                    }

                    TPLTask.WaitAll(tasks.ToArray());

                    lock (results)
                    {
                        foreach (var task in tasks)
                        {
                            var subList = task.Result;
                            results.AddRange(subList);
                            containsMatch |= subList.Count > 0;
                        }
                    }
                }
                else
                {
                    foreach (var child in treeNode.Children)
                    {
                        containsMatch |= Visit(child, matcher, results, cancellationToken);
                    }
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

using System.Collections.Generic;
using System.Threading;
using Microsoft.Build.Logging.StructuredLogger;
using TPLTask = System.Threading.Tasks.Task;

namespace StructuredLogViewer
{
    public class Search
    {
        public const int DefaultMaxResults = 1000;

        private readonly IEnumerable<TreeNode> roots;
        private readonly IEnumerable<string> strings;
        private readonly int maxResults;
        private int resultCount;
        private bool markResultsInTree = false;

        public Search(IEnumerable<TreeNode> roots, IEnumerable<string> strings, int maxResults, bool markResultsInTree)
        {
            this.roots = roots;
            this.strings = strings;
            this.maxResults = maxResults;
            this.markResultsInTree = markResultsInTree;
        }

        public IEnumerable<SearchResult> FindNodes(string query, CancellationToken cancellationToken)
        {
            var matcher = new NodeQueryMatcher(query, strings, cancellationToken);

            var resultSet = new List<SearchResult>();
            foreach (var root in roots)
            {
                Visit(root, matcher, resultSet, cancellationToken);
            }

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

        private bool Visit(BaseNode node, NodeQueryMatcher matcher, List<SearchResult> results, CancellationToken cancellationToken)
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
                var children = treeNode.Children;
                if (node is Project)
                {
                    var tasks = new System.Threading.Tasks.Task<List<SearchResult>>[children.Count];

                    for (int i = 0; i < children.Count; i++)
                    {
                        var child = children[i];
                        var task = TPLTask.Run(() =>
                        {
                            var list = new List<SearchResult>();
                            Visit(child, matcher, list, cancellationToken);
                            return list;
                        });
                        tasks[i] = task;
                    }

                    TPLTask.WaitAll(tasks);

                    lock (results)
                    {
                        for (int i = 0; i < tasks.Length; i++)
                        {
                            var task = tasks[i];
                            var subList = task.Result;
                            results.AddRange(subList);
                            containsMatch |= subList.Count > 0;
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < children.Count; i++)
                    {
                        var child = children[i];
                        containsMatch |= Visit(child, matcher, results, cancellationToken);
                    }
                }
            }

            // setting these flags on each node is expensive so do it only if the feature is enabled
            if (markResultsInTree)
            {
                node.IsSearchResult = isMatch;
                node.ContainsSearchResult = containsMatch;
            }

            return isMatch || containsMatch;
        }
    }
}

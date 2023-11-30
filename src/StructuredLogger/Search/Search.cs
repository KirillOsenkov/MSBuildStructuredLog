using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Build.Logging.StructuredLogger;
using TPLTask = System.Threading.Tasks.Task;

namespace StructuredLogViewer
{
    public class Search
    {
        public const int DefaultMaxResults = 300;

        private readonly IEnumerable<TreeNode> roots;
        private readonly IEnumerable<string> strings;
        private readonly int maxResults;
        private int resultCount;
        private bool markResultsInTree = false;
        private readonly bool useMultithreading = PlatformUtilities.HasThreads;

        public TimeSpan PrecalculationDuration;

        public Search(IEnumerable<TreeNode> roots, IEnumerable<string> strings, int maxResults, bool markResultsInTree)
        {
            this.roots = roots;
            this.strings = strings;
            this.maxResults = maxResults;
            this.markResultsInTree = markResultsInTree;
        }

        public IEnumerable<SearchResult> FindNodes(string query, CancellationToken cancellationToken)
        {
            var resultSet = new List<SearchResult>();

            var matcher = new NodeQueryMatcher(query);

            if (roots.FirstOrDefault() is Build build)
            {
                foreach (var searchExtension in build.SearchExtensions)
                {
                    if (searchExtension.TryGetResults(matcher, resultSet, maxResults))
                    {
                        return resultSet;
                    }
                }
            }

            matcher.Initialize(strings, cancellationToken);

            foreach (var root in roots)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                Visit(root, matcher, resultSet, cancellationToken);
            }

            PrecalculationDuration = matcher.PrecalculationDuration;

            return resultSet;
        }

        public static void ClearSearchResults(Build build, bool markResultsInTree)
        {
            if (!markResultsInTree)
            {
                return;
            }

            build.VisitAllChildren<BaseNode>(node =>
            {
                node.ResetSearchResultStatus();
            });
        }

        private bool Visit(BaseNode node, NodeQueryMatcher matcher, List<SearchResult> results, CancellationToken cancellationToken)
        {
            var isMatch = false;
            var containsMatch = false;
            bool visitChildren = true;

            if (cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            if (resultCount < maxResults)
            {
                var result = matcher?.IsMatch(node);
                if (result != null)
                {
                    if (matcher.HasTimeIntervalConstraints && !matcher.IsTimeIntervalMatch(node))
                    {
                        // if the current node is outside the requested time interval, only
                        // visit the children if we mark results in the tree
                        visitChildren = markResultsInTree;

                        // ensure recursive calls don't bother matching, since we've failed the time
                        // interval test. Only refresh the result marks.
                        matcher = null;
                    }
                    else
                    {
                        isMatch = true;
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

            if (visitChildren && node is TreeNode treeNode && treeNode.HasChildren)
            {
                var children = treeNode.Children;

                bool parallelSearch = useMultithreading && node is Project;

                if (parallelSearch)
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

                    for (int i = 0; i < tasks.Length; i++)
                    {
                        var task = tasks[i];
                        var subList = task.Result;
                        results.AddRange(subList);
                        containsMatch |= subList.Count > 0;
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

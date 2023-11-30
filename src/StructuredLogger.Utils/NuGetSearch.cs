using System;
using System.Collections.Generic;
using StructuredLogViewer;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class NuGetSearch : ISearchExtension
    {
        public bool TryGetResults(NodeQueryMatcher matcher, IList<SearchResult> resultCollector, int maxResults)
        {
            if (!string.Equals(matcher.TypeKeyword, "nuget", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            resultCollector.Add(new SearchResult(new Item { Text = "NuGet" }));

            return true;
        }
    }
}
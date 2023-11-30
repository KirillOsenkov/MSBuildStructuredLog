using System.Collections.Generic;
using StructuredLogViewer;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public interface ISearchExtension
    {
        bool TryGetResults(NodeQueryMatcher nodeQueryMatcher, IList<SearchResult> resultCollector, int maxResults);
    }
}
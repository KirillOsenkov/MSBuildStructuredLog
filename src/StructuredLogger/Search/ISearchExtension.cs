using System.Collections.Generic;
using StructuredLogViewer;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public interface ISearchExtension
    {
        bool AugmentOtherResults { get; }
        bool TryGetResults(NodeQueryMatcher nodeQueryMatcher, IList<SearchResult> resultCollector, int maxResults);
    }
}
using System;

namespace Microsoft.Build.Logging.StructuredLogger
{
    [Flags]
    internal enum NodeFlags : byte
    {
        None = 0,
        Hidden = 1 << 0,
        Expanded = 1 << 1,
        SearchResult = 1 << 2,
        ContainsSearchResult = 1 << 3,
        LowRelevance = 1 << 4,
        DisableChildrenCache = 1 << 5
    }
}

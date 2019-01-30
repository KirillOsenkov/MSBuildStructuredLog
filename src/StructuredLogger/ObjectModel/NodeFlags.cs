using System;

namespace Microsoft.Build.Logging.StructuredLogger
{
    [Flags]
    internal enum NodeFlags : byte
    {
        None = 0,
        Hidden = 1 << 0,
        Expanded = 1 << 1
    }
}
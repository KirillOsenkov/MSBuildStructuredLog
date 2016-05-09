using System;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class Message : TextNode
    {
        public DateTime Timestamp { get; set; }
    }
}

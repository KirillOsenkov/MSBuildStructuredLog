using System;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class Message : LogProcessNode
    {
        public string Text { get; set; }
        public DateTime Timestamp { get; set; }
    }
}

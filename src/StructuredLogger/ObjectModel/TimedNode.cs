using System;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class TimedNode : NamedNode
    {
        public int Id { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
    }
}

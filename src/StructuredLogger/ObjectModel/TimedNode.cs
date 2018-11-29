using System;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class TimedNode : NamedNode
    {
        public int Id { get; set; }
        public int NodeId { get; set; }

        /// <summary>
        /// Unique index of the node in the build tree, can be used as a 
        /// "URL" to node
        /// </summary>
        public int Index { get; set; }

        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration => EndTime - StartTime;

        public string DurationText => TextUtilities.DisplayDuration(Duration);
    }
}

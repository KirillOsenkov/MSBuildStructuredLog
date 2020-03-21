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

        public override string TypeName => nameof(TimedNode);

        public override string ToolTip
        {
            get
            {
                return $@"Start: {TextUtilities.Display(StartTime, displayDate: true)}
End: {TextUtilities.Display(EndTime, displayDate: true)}
Duration: {DurationText}";
            }
        }
    }
}

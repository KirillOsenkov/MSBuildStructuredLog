using System;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class TimedNode : NamedNode
    {
        public int Id { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration => EndTime - StartTime;

        public string DurationText
        {
            get
            {
                var result = Duration.ToString(@"s\.fff");
                if (result == "0.000")
                {
                    return "";
                }

                return $" ({result}s)";
            }
        }
    }
}

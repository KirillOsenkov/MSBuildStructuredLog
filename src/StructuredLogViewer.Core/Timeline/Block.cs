using System;
using System.IO;
using Microsoft.Build.Logging.StructuredLogger;

namespace StructuredLogViewer
{
    public class Block
    {
        public string Text { get; set; }
        public double Start { get; set; }
        public double End { get; set; }
        public double Length => End - Start;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration => EndTime - StartTime;
        public BaseNode Node { get; set; }
        public int Indent { get; set; }

        public BlockEndpoint StartPoint;
        public BlockEndpoint EndPoint;

        public string GetTooltip()
        {
            var text = TextUtilities.ShortenValue(Node.ToString(), maxChars: 100);

            var project = Node.GetNearestParent<Project>();
            if (project != null)
            {
                text += "\n" + Path.GetFileName(project.ProjectFile);
            }

            if (Node is TimedNode timedNode)
            {
                text += "\n" + TextUtilities.DisplayDuration(timedNode.Duration);
                text += "\nStart: " + TextUtilities.Display(timedNode.StartTime);
                text += "\nEnd: " + TextUtilities.Display(timedNode.EndTime);
            }

            return text;
        }
    }

    public class BlockEndpoint
    {
        public Block Block;
        public long Timestamp;
        public bool IsStart;
    }
}

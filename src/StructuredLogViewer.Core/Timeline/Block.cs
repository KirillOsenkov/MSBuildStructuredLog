using System;
using System.Diagnostics;
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

    public class BlockEndpoint : IComparable
    {
        public Block Block;
        public long Timestamp;
        public bool IsStart;
        public int CompareTo(BlockEndpoint other)
        {
            int timeCompare = Timestamp.CompareTo(other.Timestamp);

            // If Endpoints can have the same timestamp
            if (timeCompare == 0)
            {
                // If both are starting (or both ending),
                if (IsStart == other.IsStart)
                {
                    // Favor Project >> Targets >> Task >> Others
                    int lValue, rValue;

                    switch (Block.Node)
                    {
                        case Project:
                            lValue = 3; break;
                        case Target:
                            lValue = 2; break;
                        case Task:
                            lValue = 1; break;
                        default:
                            lValue = 0; break;
                    }

                    switch (other.Block.Node)
                    {
                        case Project:
                            rValue = 3; break;
                        case Target:
                            rValue = 2; break;
                        case Task:
                            rValue = 1; break;
                        default:
                            rValue = 0; break;
                    }

                    if (rValue == lValue)
                    {
                        if (IsStart)
                            return Block.Text.CompareTo(other.Block.Text);
                        else
                            return other.Block.Text.CompareTo(Block.Text);
                    }

                    // ascending or decending edge
                    if (IsStart)
                        return rValue - lValue;
                    else
                        return lValue - rValue;
                }

                // Close existing node before starting
                return IsStart ? 1 : -1;
            }

            return timeCompare;
        }

        public int CompareTo(object obj)
        {
            return CompareTo(obj as BlockEndpoint);
        }
    }
}

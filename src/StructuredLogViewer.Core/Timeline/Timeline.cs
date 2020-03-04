using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Logging.StructuredLogger;

namespace StructuredLogViewer
{
    public class Timeline
    {
        private Build build;

        public Dictionary<int, Lane> Lanes { get; set; } = new Dictionary<int, Lane>();

        public Timeline(Build build)
        {
            this.build = build;

            Populate(build);
        }

        private void Populate(Build build)
        {
            build.VisitAllChildren<TimedNode>(node =>
            {
                if (node is Build)
                {
                    return;
                }

                if (node is Microsoft.Build.Logging.StructuredLogger.Task task && string.Equals(task.Name, "MSBuild", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                var nodeId = node.NodeId;
                if (!Lanes.TryGetValue(nodeId, out var lane))
                {
                    lane = new Lane();
                    Lanes[nodeId] = lane;
                }

                var block = CreateBlock(node);
                lane.Add(block);
            });
        }

        private Block CreateBlock(TimedNode node)
        {
            var block = new Block();
            block.StartTime = node.StartTime;
            block.EndTime = node.EndTime;
            block.Text = node.Name;
            block.Indent = node.GetParentChainIncludingThis().Count();
            block.Node = node;
            return block;
        }
    }
}

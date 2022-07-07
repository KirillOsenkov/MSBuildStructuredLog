using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Build.Logging.StructuredLogger;
using static System.Reflection.Metadata.BlobBuilder;

namespace StructuredLogViewer
{
    public class Timeline
    {


        public Dictionary<int, Lane> Lanes { get; set; } = new Dictionary<int, Lane>();

        public Timeline(Build build, bool analyzeCpp)
        {
            Populate(build, analyzeCpp: analyzeCpp);
        }

        private void Populate(Build build, bool analyzeCpp = false)
        {
            build.VisitAllChildren<TimedNode>(node =>
            {
                if (node is Build)
                {
                    return;
                }

                if (node is Microsoft.Build.Logging.StructuredLogger.Task task &&
                    (string.Equals(task.Name, "MSBuild", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(task.Name, "CallTarget", StringComparison.OrdinalIgnoreCase)))
                {
                    return;
                }

                var nodeId = node.NodeId;
                if (!Lanes.TryGetValue(nodeId, out var lane))
                {
                    lane = new Lane();
                    Lanes[nodeId] = lane;
                }

                if (analyzeCpp)
                {
                    IEnumerable<Block> blocks = PopulateCppNodes(node);
                    lane.AddRange(blocks);
                }

                lane.Add(CreateBlock(node));
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

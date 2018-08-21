﻿using System.Collections.Generic;
using System.Linq;

using Microsoft.Build.Logging.StructuredLogger;

namespace StructuredLogViewer.Core.Timeline
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
            var block = new Block
            {
                StartTime = node.StartTime,
                EndTime = node.EndTime,
                Text = node.Name,
                Indent = node.GetParentChainIncludingThis().Count(),
                Node = node
            };
            return block;
        }
    }
}

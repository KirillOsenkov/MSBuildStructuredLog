using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace StructuredLogViewer
{
    public class Lane
    {
        public ConcurrentBag<Block> Blocks { get; set; } = new();

        public void Add(Block block)
        {
            if (block.StartTime == default(DateTime) || block.EndTime == default(DateTime))
            {
                return;
            }

            if (block.EndTime <= block.StartTime)
            {
                return;
            }

            Blocks.Add(block);
        }
    }
}

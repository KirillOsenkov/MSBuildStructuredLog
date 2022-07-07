using System;
using System.Collections.Generic;

namespace StructuredLogViewer
{
    public class Lane
    {
        public List<Block> Blocks { get; set; } = new List<Block>();

        public void Add(Block block)
        {
            if (IsValid(block))
                Blocks.Add(block);
        }

        private bool IsValid(Block block)
        {
            if (block.StartTime == default(DateTime) || block.EndTime == default(DateTime))
            {
                return false;
            }

            if (block.EndTime <= block.StartTime)
            {
                return false;
            }

            return true;
        }
    }
}

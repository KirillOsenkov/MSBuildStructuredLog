using System;
using System.Collections.Generic;
using System.Linq;

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

        public void AddRange(IEnumerable<Block> blocks)
        {
            Blocks.AddRange(blocks.Where(b => IsValid(b)));
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

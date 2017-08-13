using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Logging.StructuredLogger;

namespace StructuredLogViewer
{
    public class Lane
    {
        public List<Block> Blocks { get; set; } = new List<Block>();

        public void Add(Block block)
        {
            if (block.StartTime == default(DateTime) || block.EndTime == default(DateTime))
            {
                return;
            }

            Blocks.Add(block);
        }
    }
}

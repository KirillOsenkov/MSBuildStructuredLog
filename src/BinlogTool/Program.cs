using System;
using System.Linq;

namespace BinlogTool
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 3 && string.Equals(args[0], "savefiles", StringComparison.OrdinalIgnoreCase))
            {
                var binlog = args[1];
                var outputRoot = args[2];

                new SaveFiles(args).Run(binlog, outputRoot);
            }

            Console.WriteLine("Usage: binlogtool savefiles input.binlog");
        }
    }
}

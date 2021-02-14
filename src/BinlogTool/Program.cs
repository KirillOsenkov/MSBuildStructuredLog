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
                return;
            }

            if (args.Length == 3 && string.Equals(args[0], "savestrings", StringComparison.OrdinalIgnoreCase))
            {
                var binlog = args[1];
                var outputFile = args[2];

                new SaveStrings().Run(binlog, outputFile);
                return;
            }

            Console.WriteLine("Usage: binlogtool savefiles input.binlog");
        }
    }
}

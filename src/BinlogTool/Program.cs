using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Logging.StructuredLogger;

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

            Console.WriteLine("Usage: binlogtool savefiles input.binlog output_path");
        }

        private static void CompareStrings()
        {
            var left = Serialization.ReadStringsFromFile(@"C:\temp\1.txt");
            var right = Serialization.ReadStringsFromFile(@"C:\temp\2.txt");

            var onlyLeft = left.Except(right).ToArray();
            var onlyRight = right.Except(left).ToArray();

            File.WriteAllLines(@"C:\temp\onlyLeft.txt", onlyLeft);
            File.WriteAllLines(@"C:\temp\onlyRight.txt", onlyRight);
        }
    }
}

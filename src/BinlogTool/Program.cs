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
            if (args.Length == 0)
            {
                Console.WriteLine(@"Usage:
    binlogtool listtools input.binlog
    binlogtool savefiles input.binlog output_path
    binlogtool reconstruct input.binlog output_path
    binlogtool savestrings input.binlog output.txt
    binlogtool search *.binlog search string");
                return;
            }

            var firstArg = args[0];

            if (args.Length == 3 && string.Equals(firstArg, "savefiles", StringComparison.OrdinalIgnoreCase))
            {
                var binlog = args[1];
                var outputRoot = args[2];

                new SaveFiles(args).Run(binlog, outputRoot);
                return;
            }

            if (args.Length == 3 && string.Equals(firstArg, "reconstruct", StringComparison.OrdinalIgnoreCase))
            {
                var binlog = args[1];
                var outputRoot = args[2];

                new SaveFiles(args).Run(binlog, outputRoot, reconstruct: true);
                return;
            }

            if (args.Length == 3 && string.Equals(firstArg, "savestrings", StringComparison.OrdinalIgnoreCase))
            {
                var binlog = args[1];
                var outputFile = args[2];

                new SaveStrings().Run(binlog, outputFile);
                return;
            }

            if (args.Length == 2 && string.Equals(firstArg, "listtools", StringComparison.OrdinalIgnoreCase))
            {
                var binlog = args[1];

                new ListTools().Run(binlog);
                return;
            }

            if (firstArg == "search")
            {
                if (args.Length < 3)
                {
                    Console.Error.WriteLine("binlogtool search *.binlog search string");
                    return;
                }

                var binlogs = args[1];
                var search = string.Join(" ", args.Skip(2));
                Searcher.Search(binlogs, search);
                return;
            }

            Console.Error.WriteLine("Invalid arguments");
        }

        private static void ReadStrings()
        {
            var strings = Serialization.ReadStringsFromFile(@"C:\temp\strings2.zip");
            var ordered = strings.OrderByDescending(s => s.Length).ToArray();
            var top100 = ordered.Take(3000);

            int i = 1;
            foreach (var str in top100)
            {
                File.WriteAllText($@"C:\temp\strings2\{i++}.txt", str);
            }
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

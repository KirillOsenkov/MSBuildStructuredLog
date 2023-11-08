using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Logging.StructuredLogger;
using StructuredLogger.Utils;

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
    binlogtool search *.binlog search string
    binlogtool redact --input:path --recurse --in-place -p:list -p:of -p:secrets -p:to -p:redact");
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

            if (firstArg == "redact")
            {
                List<string> redactTokens = new List<string>();
                List<string> inputPaths = new List<string>();
                bool recurse = false;
                bool inPlace = false;

                foreach (var arg in args.Skip(1))
                {
                    if (arg.StartsWith("--input:", StringComparison.OrdinalIgnoreCase))
                    {
                        var input = arg.Substring("--input:".Length);
                        if (string.IsNullOrEmpty(input))
                        {
                            Console.Error.WriteLine("Invalid input path");
                            return;
                        }

                        inputPaths.Add(input);
                    }
                    else if (arg.StartsWith("-p:", StringComparison.OrdinalIgnoreCase))
                    {
                        var redactToken = arg.Substring("-p:".Length);
                        if (string.IsNullOrEmpty(redactToken))
                        {
                            Console.Error.WriteLine("Invalid redact token");
                            return;
                        }

                        redactTokens.Add(redactToken);
                    }
                    else if (arg.Equals("--recurse", StringComparison.OrdinalIgnoreCase))
                    {
                        recurse = true;
                    }
                    else if (arg.Equals("--in-place", StringComparison.OrdinalIgnoreCase))
                    {
                        inPlace = true;
                    }
                    else
                    {
                        Console.Error.WriteLine($"Invalid argument: {arg}");
                        Console.Error.WriteLine("binlogtool redact --input:path --recurse --in-place -p:list -p:of -p:secrets -p:to -p:redact");
                        Console.Error.WriteLine("All arguments are optional (missing input assumes current working directory. Missing tokens lead only to autoredactions. Missing --in-place will create new logs with suffix.)");
                        return;
                    }
                }

                Redact.Run(inputPaths, redactTokens, inPlace, recurse);
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

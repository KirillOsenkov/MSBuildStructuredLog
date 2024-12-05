using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Build.Logging.StructuredLogger;

namespace BinlogTool
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine(@"Usage:
    binlogtool listtools input.binlog
    binlogtool savefiles input.binlog output_path
    binlogtool listnuget input.binlog output_path
    binlogtool listproperties input.binlog
    binlogtool reconstruct input.binlog output_path
    binlogtool savestrings input.binlog output.txt
    binlogtool search *.binlog search string
    binlogtool redact --input:path --recurse --in-place -p:list -p:of -p:secrets -p:to -p:redact
    binlogtool dumprecords [[--input:]path] [--include-total] [--include-rollup] [--exclude-details]");
                return 0;
            }

            var firstArg = args[0];

            if (args.Length == 3 && string.Equals(firstArg, "savefiles", StringComparison.OrdinalIgnoreCase))
            {
                var binlog = args[1];
                var outputRoot = args[2];

                new SaveFiles(args).Run(binlog, outputRoot);
                return 0;
            }

            if (args.Length >= 2 && string.Equals(firstArg, "listnuget", StringComparison.OrdinalIgnoreCase))
            {
                var binlog = args[1];
                if (!File.Exists(binlog))
                {
                    Console.Error.WriteLine($"Binlog file {binlog} not found");
                    return -6;
                }

                string outputFile = null;
                if (args.Length == 3)
                {
                    outputFile = args[2];
                }

                int result = new ListNuget(args).Run(binlog, outputFile);
                return result;
            }

            if (args.Length == 3 && string.Equals(firstArg, "reconstruct", StringComparison.OrdinalIgnoreCase))
            {
                var binlog = args[1];
                var outputRoot = args[2];

                new SaveFiles(args).Run(binlog, outputRoot, reconstruct: true);
                return 0;
            }

            if (args.Length == 3 && string.Equals(firstArg, "savestrings", StringComparison.OrdinalIgnoreCase))
            {
                var binlog = args[1];
                var outputFile = args[2];

                new SaveStrings().Run(binlog, outputFile);
                return 0;
            }

            if (args.Length == 2 && string.Equals(firstArg, "listProperties", StringComparison.OrdinalIgnoreCase))
            {
                var binlog = args[1];
                new ListProperties().Run(binlog);
                return 0;
            }

            if (string.Equals(firstArg, "compilerinvocations", StringComparison.OrdinalIgnoreCase))
            {
                string binlog = null;
                string outputFile = null;
                if (args.Length >= 2)
                {
                    binlog = args[1];
                }
                else if (args.Length == 3)
                {
                    outputFile = args[2];
                }

                if (File.Exists(binlog))
                {
                    new CompilerInvocations().Run(binlog, outputFile);
                }

                return 0;
            }

            if (args.Length == 2 && string.Equals(firstArg, "listtools", StringComparison.OrdinalIgnoreCase))
            {
                var binlog = args[1];

                new ListTools().Run(binlog);
                return 0;
            }

            if (firstArg == "search")
            {
                if (args.Length < 3)
                {
                    Console.Error.WriteLine("binlogtool search *.binlog search string");
                    return -2;
                }

                var binlogs = args[1];
                var search = string.Join(" ", args.Skip(2));
                new Searcher().Search2(binlogs, search);
                return 0;
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
                            return -3;
                        }

                        inputPaths.Add(input);
                    }
                    else if (arg.StartsWith("-p:", StringComparison.OrdinalIgnoreCase))
                    {
                        var redactToken = arg.Substring("-p:".Length);
                        if (string.IsNullOrEmpty(redactToken))
                        {
                            Console.Error.WriteLine("Invalid redact token");
                            return -4;
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
                        return -5;
                    }
                }

                Redact.Run(inputPaths, redactTokens, inPlace, recurse);
                return 0;
            }

            if (firstArg == "dumprecords")
            {
                bool includeTotal = false;
                bool includeRollup = false;
                bool includeDetails = true;
                List<string> inputPaths = new List<string>();

                foreach (var arg in args.Skip(1))
                {
                    if (arg.StartsWith("--input:", StringComparison.OrdinalIgnoreCase))
                    {
                        var input = arg.Substring("--input:".Length);
                        if (string.IsNullOrEmpty(input))
                        {
                            Console.Error.WriteLine("Invalid input path");
                            return -3;
                        }

                        inputPaths.Add(input);
                    }
                    // [--include-total] [--include-rollup] [--exclude-details]
                    else if (arg.Equals("--include-total", StringComparison.OrdinalIgnoreCase))
                    {
                        includeTotal = true;
                    }
                    else if (arg.Equals("--include-rollup", StringComparison.OrdinalIgnoreCase))
                    {
                        includeRollup = true;
                    }
                    else if (arg.Equals("--exclude-details", StringComparison.OrdinalIgnoreCase))
                    {
                        includeDetails = false;
                    }
                    else if (arg.EndsWith(".binlog", StringComparison.OrdinalIgnoreCase))
                    {
                        inputPaths.Add(arg);
                    }
                    else
                    {
                        Console.Error.WriteLine($"Invalid argument: {arg}");
                        Console.Error.WriteLine("binlogtool dumprecords [[--input:]path] [--include-total] [--include-rollup] [--exclude-details]");
                        Console.Error.WriteLine("All arguments are optional (Missing input assumes current working directory. Rollup and total is disabled by default, detail overview is enabled by default. Input(s) arguments can be specified without switch.)");
                        return -5;
                    }
                }

                DumpRecords.Run(inputPaths, includeTotal, includeRollup, includeDetails);
                return 0;
            }

            Console.Error.WriteLine("Invalid arguments");
            return -1;
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

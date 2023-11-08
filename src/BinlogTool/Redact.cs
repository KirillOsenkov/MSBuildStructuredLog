using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using StructuredLogger.Utils;

namespace BinlogTool
{
    internal static class Redact
    {
        public static void Run(List<string> inputs, List<string> tokens, bool inPlace, bool recurse)
        {
            if (!inputs.Any())
            {
                // Default - current directory
                inputs.Add(string.Empty);
            }

            var inputBinlogs = inputs.SelectMany(input => Searcher.FindBinlogs(input, recurse)).ToList();

            if (!inputBinlogs.Any())
            {
                Log.WriteError("No binlogs found.");
                return;
            }

            if (inputBinlogs.Count > 1)
            {
                Log.WriteLine(
                    $"Found {inputBinlogs.Count} binlog files. Will redact secrets in all. (found files: {(string.Join(',', inputBinlogs))})");
            }

            BinlogRedactorOptions options = new BinlogRedactorOptions(string.Empty)
            {
                TokensToRedact = tokens.ToArray(),
                IdentifyReplacemenets = true,
                AutodetectCommonPatterns = true,
                AutodetectUsername = true,
                ProcessEmbeddedFiles = true,
            };

            var overallStopwatch = Stopwatch.StartNew();

            foreach (var inputBinlog in inputBinlogs)
            {
                options.InputPath = inputBinlog;
                options.OutputFileName = GetOutputFileName(inputBinlog);

                Log.WriteLine($"Redacting binlog {inputBinlog} to {options.OutputFileName} ({GetFileSizeInKB(inputBinlog)} KB)");

                var stopwatch = Stopwatch.StartNew();

                BinlogRedactor.RedactSecrets(options, progress: null);

                stopwatch.Stop();
                Log.WriteLine($"Redacting done. Duration: {stopwatch.Elapsed}");
            }

            overallStopwatch.Stop();
            if(inputBinlogs.Count > 1)
            {
                Log.WriteLine($"Redacting all binlogs done. Duration: {overallStopwatch.Elapsed}");
            }

            string GetOutputFileName(string inputFileName)
            {
                if (inPlace)
                {
                    return inputFileName;
                }

                return Path.ChangeExtension(inputFileName, ".redacted.binlog");
            }

            long GetFileSizeInKB(string path)
                => new FileInfo(path).Length / 1024;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Logging.StructuredLogger;
using Microsoft.Build.Logging;
using StructuredLogger.Utils;

namespace BinlogTool
{
    internal static class DumpRecords
    {
        public static void Run(List<string> inputs, bool includeTotal, bool includeRollup, bool includeDetails)
        {
            if (!inputs.Any())
            {
                // Default - current directory
                inputs.Add(string.Empty);
            }

            var inputBinlogs = inputs.SelectMany(input => Searcher.FindBinlogs(input, true)).ToList();

            if (!inputBinlogs.Any())
            {
                Log.WriteError("No binlogs found.");
                return;
            }

            if (inputBinlogs.Count > 1)
            {
                Log.WriteLine(
                    $"Found {inputBinlogs.Count} binlog files. Will provide overview for all. (found files: {(string.Join(',', inputBinlogs))})");
            }

            foreach (var inputBinlog in inputBinlogs)
            {
                if (inputBinlogs.Count > 1)
                {
                    Log.WriteLine();
                    Log.WriteLine($"Overview for {inputBinlog}");
                    Log.WriteLine();
                }

                RunSingle(inputBinlog, includeTotal, includeRollup, includeDetails);
            }
        }

        public static void RunSingle(string input, bool includeTotal, bool includeRollup, bool includeDetails)
        {
            bool needsGreedyEnumerate = includeRollup && includeDetails;

            IEnumerable<(BinaryLogRecordKind, long)> records =
                new BinLogReader().ChunkBinlog(input)
                    .Select(r => (r.Kind, r.Length));

            int totalRecords = 0;

            if (needsGreedyEnumerate)
            {
                records = records.ToList();
                totalRecords = records.Count();
            }

            if (includeRollup)
            {
                totalRecords = 0;
                Log.WriteLine("RecordType, Count, Size[bytes]");
                Log.WriteLine("==============================");
                foreach (var group in records.GroupBy(r => r.Item1))
                {
                    Log.WriteLine($"{group.Key}, {group.Count()}, {group.Sum(r => r.Item2) / (double)group.Count():0.00}");
                    totalRecords += group.Count();
                }
                Log.WriteLine();
            }

            if (includeDetails)
            {
                totalRecords = 0;
                Log.WriteLine("RecordType, Size[bytes]");
                Log.WriteLine("=======================");
                foreach (var record in records)
                {
                    Log.WriteLine($"{record.Item1}, {record.Item2}");
                    totalRecords++;
                }
            }

            if (includeTotal)
            {
                Log.WriteLine($"Total records: {totalRecords}");
                Log.WriteLine();
            }
        }
    }
}

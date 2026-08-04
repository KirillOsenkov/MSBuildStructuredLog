using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.Build.Logging.StructuredLogger;

namespace BinlogTool
{
    public class Stats
    {
        private const int TopSamples = 10;
        private const int SamplesPerDecile = 10;
        private const int SampleMaxLength = 500;
        private const int MaxSampleScanAttempts = 1000;
        private const int TopStringCount = 100;
        private const int TopStringMaxLength = 500;

        public int Run(string binlogFilePath, string htmlOutput, string textOutput, bool includeStrings)
        {
            if (string.IsNullOrEmpty(binlogFilePath) || !File.Exists(binlogFilePath))
            {
                Log.WriteError($"Binlog file {binlogFilePath} not found");
                return -1;
            }

            binlogFilePath = Path.GetFullPath(binlogFilePath);

            // when neither output is specified, print the text report to stdout
            // and keep stdout free of status messages
            bool writeToConsole = htmlOutput == null && textOutput == null;

            if (!writeToConsole)
            {
                Log.WriteLine($"Reading {binlogFilePath}...");
            }

            var stats = BinlogStats.Calculate(binlogFilePath, trackStrings: includeStrings, sort: true);
            var report = CreateReport(binlogFilePath, stats, includeStrings);

            if (writeToConsole)
            {
                Console.Out.Write(StatsTextWriter.Write(report));
                return 0;
            }

            if (textOutput != null)
            {
                File.WriteAllText(textOutput, StatsTextWriter.Write(report));
                Log.WriteLine($"Wrote {new FileInfo(textOutput).Length:N0} bytes to {textOutput}", ConsoleColor.Green);
            }

            if (htmlOutput != null)
            {
                File.WriteAllText(htmlOutput, StatsHtmlWriter.Write(report));
                Log.WriteLine($"Wrote {new FileInfo(htmlOutput).Length:N0} bytes to {htmlOutput}", ConsoleColor.Green);
            }

            return 0;
        }

        private static StatsReport CreateReport(string binlogFilePath, BinlogStats stats, bool includeStrings)
        {
            var report = new StatsReport
            {
                FileName = Path.GetFileName(binlogFilePath),
                FilePath = binlogFilePath,
                Generated = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " UTC",
                FileFormatVersion = stats.FileFormatVersion,
                FileSize = stats.FileSize,
                UncompressedSize = stats.UncompressedStreamSize,
                RecordCount = stats.RecordCount,
                Strings = new StorageBucket
                {
                    Count = stats.StringCount,
                    Size = stats.StringTotalSize,
                    Largest = stats.StringLargest
                },
                NameValueLists = new StorageBucket
                {
                    Count = stats.NameValueListCount,
                    Size = stats.NameValueListTotalSize,
                    Largest = stats.NameValueListLargest
                },
                Blobs = new StorageBucket
                {
                    Count = stats.BlobCount,
                    Size = stats.BlobTotalSize,
                    Largest = stats.BlobLargest
                },
                Root = CreateNode(stats.CategorizedRecords, isRoot: true)
            };

            if (includeStrings && stats.AllStrings.Count > 0)
            {
                // AllStrings is already sorted by length descending
                report.TopStrings = stats.AllStrings
                    .Take(TopStringCount)
                    .Select(s => new StringSample { Len = s.Length, Text = Truncate(s, TopStringMaxLength) })
                    .ToList();
            }

            return report;
        }

        private static StatsNode CreateNode(BinlogStats.RecordsByType bucket, bool isRoot = false)
        {
            var node = new StatsNode
            {
                Name = isRoot ? "Event records" : bucket.Type,
                Size = bucket.TotalLength,
                Count = bucket.Count,
                Largest = bucket.Largest
            };

            if (bucket.CategorizedRecords is { Count: > 0 })
            {
                node.Children = bucket.CategorizedRecords.Select(c => CreateNode(c)).ToList();
            }

            node.SampleGroups = CollectSamples(bucket);
            return node;
        }

        private static List<SampleGroup> CollectSamples(BinlogStats.RecordsByType bucket)
        {
            var records = bucket.Records as IReadOnlyList<Record> ?? bucket.Records.ToArray();
            if (records.Count == 0)
            {
                return null;
            }

            var groups = new List<SampleGroup>();
            var seenTexts = new HashSet<string>();

            void AddGroup(string label, int startIndex, int count)
            {
                var group = new SampleGroup { Label = label, Samples = new List<Sample>() };
                int attempts = 0;
                for (int i = startIndex; i < records.Count && group.Samples.Count < count && attempts < MaxSampleScanAttempts; i++, attempts++)
                {
                    var text = GetRecordText(records[i]);
                    if (string.IsNullOrEmpty(text))
                    {
                        continue;
                    }

                    var truncated = Truncate(text, SampleMaxLength);
                    if (!seenTexts.Add(truncated))
                    {
                        continue;
                    }

                    group.Samples.Add(new Sample { Len = text.Length, Text = truncated });
                }

                if (group.Samples.Count > 0)
                {
                    groups.Add(group);
                }
            }

            // records in each bucket are sorted by size descending, so index maps to a size percentile
            AddGroup("largest", 0, TopSamples);

            if (records.Count > TopSamples)
            {
                for (int decile = 1; decile <= 9; decile++)
                {
                    int start = (int)((long)records.Count * decile / 10);
                    AddGroup($"p{100 - decile * 10}", start, SamplesPerDecile);
                }
            }

            return groups.Count > 0 ? groups : null;
        }

        private static string GetRecordText(Record record)
        {
            var args = record.Args;
            if (args == null)
            {
                return null;
            }

            try
            {
                if (args is EnvironmentVariableReadEventArgs env)
                {
                    return $"{env.EnvironmentVariableName} = {env.Message}";
                }

                var message = args.Message;
                if (!string.IsNullOrEmpty(message))
                {
                    return message;
                }
            }
            catch
            {
            }

            return args.GetType().Name;
        }

        internal static string Truncate(string text, int maxLength)
        {
            return text.Length <= maxLength ? text : text.Substring(0, maxLength) + "…";
        }
    }

    internal class StatsReport
    {
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public string Generated { get; set; }
        public int FileFormatVersion { get; set; }
        public long FileSize { get; set; }
        public long UncompressedSize { get; set; }
        public long RecordCount { get; set; }
        public StorageBucket Strings { get; set; }
        public StorageBucket NameValueLists { get; set; }
        public StorageBucket Blobs { get; set; }
        public StatsNode Root { get; set; }
        public List<StringSample> TopStrings { get; set; }

        /// <summary>
        /// Total uncompressed bytes accounted for: event records + string table + name-value lists + blobs.
        /// Used as the denominator for all percentages.
        /// </summary>
        [JsonIgnore]
        public long TotalAccounted =>
            (Root?.Size ?? 0) + Strings.Size + NameValueLists.Size + Blobs.Size;
    }

    internal class StorageBucket
    {
        public int Count { get; set; }
        public long Size { get; set; }
        public int Largest { get; set; }
    }

    internal class StatsNode
    {
        public string Name { get; set; }
        public long Size { get; set; }
        public int Count { get; set; }
        public int Largest { get; set; }
        public List<StatsNode> Children { get; set; }
        public List<SampleGroup> SampleGroups { get; set; }
    }

    internal class SampleGroup
    {
        public string Label { get; set; }
        public List<Sample> Samples { get; set; }
    }

    internal class Sample
    {
        /// <summary>Length of the full record text in characters, before truncation.</summary>
        public int Len { get; set; }
        public string Text { get; set; }
    }

    internal class StringSample
    {
        public int Len { get; set; }
        public string Text { get; set; }
    }

    internal static class StatsTextWriter
    {
        private const int TextTopSamples = 5;
        private const int TextSamplesPerDecile = 2;
        private const int TextSampleMaxLength = 300;

        public static string Write(StatsReport report)
        {
            var sb = new StringBuilder();
            long total = Math.Max(1, report.TotalAccounted);

            sb.AppendLine("Binlog statistics");
            sb.AppendLine("=================");
            sb.AppendLine($"File:            {report.FilePath}");
            sb.AppendLine($"File size:       {report.FileSize:N0} bytes on disk (compressed)");
            sb.AppendLine($"Uncompressed:    {report.UncompressedSize:N0} bytes ({Ratio(report.UncompressedSize, report.FileSize)}x compression ratio)");
            sb.AppendLine($"Event records:   {report.RecordCount:N0}");
            sb.AppendLine($"Format version:  {report.FileFormatVersion}");
            sb.AppendLine($"Generated:       {report.Generated} by binlogtool stats");
            sb.AppendLine();

            sb.AppendLine("How to read this report");
            sb.AppendLine("-----------------------");
            sb.AppendLine("- All sizes are bytes in the uncompressed binlog stream unless stated otherwise.");
            sb.AppendLine("- Percentages are relative to the total accounted uncompressed size");
            sb.AppendLine("  (event records + string table + name-value lists + blobs).");
            sb.AppendLine("- Strings are deduplicated and stored once in the string table; event records");
            sb.AppendLine("  only reference them. A record can therefore be tiny (its 'largest' can be a");
            sb.AppendLine("  few bytes) while its message text is long. To understand what takes up space,");
            sb.AppendLine("  look at both the record buckets and the Strings bucket.");
            sb.AppendLine("- Buckets are sorted by total size, largest first. Bucket format:");
            sb.AppendLine("  Name  total=<bytes> (<percent>)  count=<records>  largest=<bytes of largest single record>");
            sb.AppendLine("- Lines starting with '~' are sample record texts from the bucket above them:");
            sb.AppendLine("  'largest' samples are the biggest records; pNN samples sit at the NNth size");
            sb.AppendLine("  percentile. len is the full text length in characters; sample texts are");
            sb.AppendLine("  deduplicated, truncated and have newlines escaped as \\n.");
            sb.AppendLine();

            sb.AppendLine("Space breakdown");
            sb.AppendLine("---------------");
            sb.AppendLine($"Total accounted uncompressed size: {total:N0} bytes");
            sb.AppendLine();

            // flatten the top level: record types compete with the storage buckets
            var topLevel = new List<(long size, Action write)>();

            if (report.Root?.Children != null)
            {
                foreach (var child in report.Root.Children)
                {
                    var node = child;
                    topLevel.Add((node.Size, () => WriteNode(sb, node, 0, total)));
                }
            }
            else if (report.Root != null)
            {
                var node = report.Root;
                topLevel.Add((node.Size, () => WriteNode(sb, node, 0, total)));
            }

            topLevel.Add((report.Strings.Size, () =>
            {
                WriteBucketLine(sb, 0, "Strings (deduplicated string table)", report.Strings.Size, total, report.Strings.Count, report.Strings.Largest);
            }));

            if (report.NameValueLists.Size > 0)
            {
                topLevel.Add((report.NameValueLists.Size, () =>
                {
                    WriteBucketLine(sb, 0, "NameValueLists (deduplicated property/metadata lists)", report.NameValueLists.Size, total, report.NameValueLists.Count, report.NameValueLists.Largest);
                }));
            }

            if (report.Blobs.Size > 0)
            {
                topLevel.Add((report.Blobs.Size, () =>
                {
                    WriteBucketLine(sb, 0, "Blobs (embedded files archive)", report.Blobs.Size, total, report.Blobs.Count, report.Blobs.Largest);
                }));
            }

            foreach (var entry in topLevel.OrderByDescending(e => e.size))
            {
                entry.write();
            }

            sb.AppendLine();
            WriteStrings(sb, report);

            return sb.ToString();
        }

        private static void WriteStrings(StringBuilder sb, StatsReport report)
        {
            sb.AppendLine("Strings");
            sb.AppendLine("-------");
            sb.AppendLine($"String table: {report.Strings.Count:N0} unique strings, {report.Strings.Size:N0} bytes total, largest {report.Strings.Largest:N0} bytes.");

            if (report.TopStrings != null)
            {
                sb.AppendLine($"Top {report.TopStrings.Count} largest strings (len is characters; texts truncated, newlines escaped):");
                int index = 1;
                foreach (var str in report.TopStrings)
                {
                    sb.AppendLine($"{index}. len={str.Len:N0}: {Flatten(str.Text)}");
                    index++;
                }
            }
            else
            {
                sb.AppendLine("Run with --strings to include the largest strings here.");
            }

            sb.AppendLine("Use 'binlogtool savestrings' to dump the complete string table.");
        }

        private static void WriteNode(StringBuilder sb, StatsNode node, int indent, long total)
        {
            WriteBucketLine(sb, indent, node.Name, node.Size, total, node.Count, node.Largest);

            if (node.SampleGroups != null)
            {
                string prefix = new string(' ', (indent + 1) * 4);
                foreach (var group in node.SampleGroups)
                {
                    int take = group.Label == "largest" ? TextTopSamples : TextSamplesPerDecile;
                    foreach (var sample in group.Samples.Take(take))
                    {
                        sb.AppendLine($"{prefix}~ {group.Label} len={sample.Len:N0}: {Flatten(Stats.Truncate(sample.Text, TextSampleMaxLength))}");
                    }
                }
            }

            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    WriteNode(sb, child, indent + 1, total);
                }
            }
        }

        private static void WriteBucketLine(StringBuilder sb, int indent, string name, long size, long total, int count, int largest)
        {
            sb.Append(new string(' ', indent * 4));
            sb.AppendLine($"{name}  total={size:N0} ({Percent(size, total)})  count={count:N0}  largest={largest:N0}");
        }

        private static string Percent(long size, long total)
        {
            double percent = total == 0 ? 0 : size * 100.0 / total;
            return percent >= 10 ? $"{percent:N1}%" : $"{percent:N2}%";
        }

        private static string Ratio(long uncompressed, long compressed)
        {
            return compressed == 0 ? "?" : $"{(double)uncompressed / compressed:N1}";
        }

        private static string Flatten(string text)
        {
            return text
                .Replace("\r\n", "\\n")
                .Replace("\n", "\\n")
                .Replace("\r", "\\n")
                .Replace("\t", "\\t");
        }
    }
}

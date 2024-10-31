using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.Logging.StructuredLogger;
using Xunit;
using Xunit.Abstractions;

namespace StructuredLogger.Tests
{
    public class BinarySerializationTests
    {
        private readonly ITestOutputHelper output;

        public BinarySerializationTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        internal void TimeRead()
        {
            var sw = Stopwatch.StartNew();
            var build = Serialization.Read(@"1.binlog");
            System.Windows.Forms.MessageBox.Show(sw.Elapsed.ToString());
        }

        internal void DumpTimedNodes()
        {
            var build = Serialization.Read("1.binlog");
            var sb = new StringBuilder();
            build.VisitAllChildren<TimedNode>(t => sb.AppendLine($"{t.Index}: {t}"));
            System.Windows.Forms.Clipboard.SetText(sb.ToString());
        }

        //[Fact]
        internal void GetBlobSize()
        {
            var filePath = @"1.binlog";
            long size = GetBinlogBlobSize(filePath);
            output.WriteLine(size.ToString());
        }

        public static long GetBinlogBlobSize(string filePath)
        {
            long result = 0;
            var reader = new BinLogReader();
            reader.OnBlobRead += (kind, bytes) =>
            {
                result = bytes.LongLength;
            };
            var records = reader.ReadRecords(filePath);
            foreach (var record in records)
            {
            }

            return result;
        }

        //[Fact]
        internal void RecordStats()
        {
            var reader = new BinLogReader();
            var records = reader.ReadRecords(@"C:\msbuild\msbuild.binlog");

            var recordsByType = new Dictionary<string, List<Microsoft.Build.Logging.Record>>();

            foreach (var record in records)
            {
                var args = record.Args;
                if (args == null)
                {
                    // probably a blob
                    continue;
                }

                string argsType = args.GetType().Name;
                if (!recordsByType.TryGetValue(argsType, out var bucket))
                {
                    bucket = new List<Microsoft.Build.Logging.Record>();
                    recordsByType[argsType] = bucket;
                }

                bucket.Add(record);
            }

            var mostRecords = recordsByType
                .Select(kvp => (name: kvp.Key, count: kvp.Value.Count, totalLength: kvp.Value.Sum(r => r.Length), list: kvp.Value))
                .OrderByDescending(t => t.totalLength)
                .ToArray();

            foreach (var type in mostRecords)
            {
                type.list.Sort((l, r) => Math.Sign(r.Length - l.Length));
            }

            var messages = mostRecords[1].list;
            var messageGroups = messages.GroupBy(m => GetMessageType(m.Args?.Message))
                .Select(g => (messageType: g.Key, count: g.Count(), totalLength: g.Sum(m => m.Args?.Message?.Length ?? 0), messages: g.OrderByDescending(r => r.Length).ToArray()))
                .OrderByDescending(g => g.Item3)
                .ToArray();
        }

        private string GetMessageType(string message)
        {
            if (message == null || message.Length < 50)
            {
                return "Misc";
            }

            var first = message.Substring(0, 10);
            switch (first)
            {
                case "Did not co":
                    return "Did not copy";
                case "Input file":
                    return "Input files";
                case "Output fil":
                    return "Output files";
                case "Primary re":
                    return "Primary reference";
                case "Encountere":
                    return "Encountered conflict";
                case "Output Ite":
                    return "Output Item";
                case "Task Param":
                    return "Task Parameter";
                case "Added Item":
                    return "Added Item";
                case "Removed It":
                    return "Removed Item";
                case "Overriding":
                    return "Overriding target";
                case "Removing P":
                    return "Removing Property";
                case "Using Task":
                    return "Using Task";
                case "Property r":
                    return "Property reassignment";
                case "    Resolv":
                    return "Resolved file path";
                case "    This r":
                    return "This reference is not";
                default:
                    break;
            }

            return first;
        }

        //[Fact]
        internal void Stats()
        {
            var sw = Stopwatch.StartNew();
            var build = Serialization.Read(@"C:\temp\vsmac.binlog");
            var stats = build.Statistics;

            //var messages = stats.TruncatedMessages.OrderByDescending(m => m.text.Length).Select(m => (m.taskName, m.text.Length, m.text)).ToArray();
            //var lengths = messages.Select(m => m.taskName + "," + m.Length.ToString()).ToArray();
            //System.Windows.Forms.Clipboard.SetText(string.Join("\n", lengths));

            var messagesDirectlyUnderTask = build
                .FindChildrenRecursive<Message>()
                .Where(m => m.Parent is Task)
                .GroupBy(m => ((Task)m.Parent).Name)
                .Select(g => (g.Key, g.Count(), g.Sum(m => m.Text.Length), g.OrderByDescending(m => m.Text.Length).ToArray()))
                .OrderByDescending(g => g.Item3)
                .ToArray();

            var parameters = build
                .FindChildrenRecursive<Parameter>()
                .Where(m => m.Parent is Folder f && f.Name == "Parameters")
                .GroupBy(m => ((Task)m.Parent.Parent).Name)
                .Select(g => (g.Key, g.Count(), g.ToArray()))
                .OrderByDescending(g => g.Item2)
                .ToArray();

            foreach (var bucket in stats.TaskParameterMessagesByTask.Values)
            {
                bucket.Sort((l, r) => Math.Sign(r.Length - l.Length));
            }

            foreach (var bucket in stats.OutputItemMessagesByTask.Values)
            {
                bucket.Sort((l, r) => Math.Sign(r.Length - l.Length));
            }

            var parametersList = stats.TaskParameterMessagesByTask
                .Select(m => (m.Key, m.Value.Sum(l => l.Length), m.Value.Count, m.Value))
                .OrderByDescending(g => g.Item2)
                .ToArray();

            var outputItemList = stats.OutputItemMessagesByTask
                .Select(m => (m.Key, m.Value.Sum(l => l.Length), m.Value.Count, m.Value))
                .OrderByDescending(g => g.Item2)
                .ToArray();

            var elapsed = sw.Elapsed;
        }

        private static void WriteToDisk((string taskName, int Length, string text)[] messages)
        {
            var root = @"C:\temp\tasks";
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }

            Directory.CreateDirectory(root);

            for (int i = 0; i < messages.Length; i++)
            {
                var filePath = Path.Combine(root, $"{i.ToString().PadLeft(5, '0')}_{messages[i].taskName}_{messages[i].Length}.txt");
                File.WriteAllText(filePath, messages[i].text);
            }
        }

        //[Fact]
        internal void TestWriter()
        {
            var build = Serialization.Read(@"D:\XmlBuildLogs\contentsync.xml");
            Serialization.Write(build, @"D:\1.buildlog");
        }

        [Fact]
        public void SimpleBuild()
        {
            var build = new Build();
            build.AddChild(new Message() { Text = "MessageText" });
            build.AddChild(new Property() { Name = "PropertyName", Value = "PropertyValue" });
            var xmlFile1 = @"1.xml";
            var xmlFile2 = @"2.xml";
            Serialization.Write(build, xmlFile1);
            var buildLogFile = @"1.buildlog";
            Serialization.Write(build, buildLogFile);
            build = Serialization.Read(buildLogFile);
            Serialization.Write(build, xmlFile2);
            Serialization.Write(build, @"2.buildlog");
            AssertEx.EqualOrDiff(File.ReadAllText(xmlFile1), File.ReadAllText(xmlFile2));
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Build.Logging.StructuredLogger;
using Xunit;

namespace StructuredLogger.Tests
{
    public class BinarySerializationTests
    {
        //[Fact]
        public void RecordStats()
        {
            var reader = new BinLogReader();
            var records = reader.ReadRecords(@"C:\temp\vsmac.binlog");

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

            var messages = mostRecords[0].list;
            var messageGroups = messages.GroupBy(m => GetMessageType(m.Args?.Message))
                .Select(g => (g.Key, g.Count(), g.Sum(m => m.Args?.Message?.Length ?? 0), g.ToArray()))
                .OrderByDescending(g => g.Item3)
                .ToArray();

            var projectStarted = mostRecords[1].list;
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
                case "Output Ite":
                    return "Output Item";
                case "Task Param":
                    return "Task Parameter";
                case "Added Item":
                    return "Added Item";
                case "Removed It":
                    return "Removed Item";
                default:
                    break;
            }

            return "Misc";
        }

        //[Fact]
        public void Stats()
        {
            var sw = Stopwatch.StartNew();
            var build = Serialization.Read(@"C:\temp\WithoutPerfFix.binlog");
            var stats = build.Statistics;

            //var messages = stats.TruncatedMessages.OrderByDescending(m => m.text.Length).Select(m => (m.taskName, m.text.Length, m.text)).ToArray();
            //var lengths = messages.Select(m => m.taskName + "," + m.Length.ToString()).ToArray();
            //System.Windows.Forms.Clipboard.SetText(string.Join("\n", lengths));

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
        public void TestWriter()
        {
            var build = Serialization.Read(@"D:\XmlBuildLogs\contentsync.xml");
            Serialization.Write(build, @"D:\1.buildlog");
        }

        [Fact]
        public void SimpleBuild()
        {
            var build = new Build();
            build.Succeeded = true;
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
            Differ.AreDifferent(xmlFile1, xmlFile2);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class BinlogStats
    {
        public static BinlogStats Calculate(string binlogFilePath)
        {
            var reader = new BinLogReader();
            var records = reader.ReadRecords(binlogFilePath);
            return Calculate(records);
        }

        public static BinlogStats Calculate(IEnumerable<Record> records)
        {
            var stats = new BinlogStats();
            stats.Process(records);
            return stats;
        }

        public RecordsByType CategorizedRecords { get; private set; }

        public class RecordsByType
        {
            private readonly List<Record> list = new List<Record>();

            Dictionary<string, RecordsByType> recordsByType = new Dictionary<string, RecordsByType>();
            public IReadOnlyList<RecordsByType> CategorizedRecords { get; private set; }

            public RecordsByType(string type = null)
            {
                this.Type = type;
            }

            public static RecordsByType Create(string type)
            {
                return new RecordsByType(type);
            }

            public string Type { get; }
            public long TotalLength { get; private set; }
            public int Count { get; private set; }

            public virtual void Add(Record record, string type = null)
            {
                if (type != null)
                {
                    type = GetMessageType(type, record.Args);

                    if (!recordsByType.TryGetValue(type, out var bucket))
                    {
                        bucket = Create(type);
                        recordsByType[type] = bucket;
                    }

                    bucket.Add(record);
                }
                else
                {
                    list.Add(record);
                }

                Count += 1;
                TotalLength += record.Length;
            }

            public virtual void Seal()
            {
                list.Sort((l, r) => Math.Sign(r.Length - l.Length));

                foreach (var type in recordsByType)
                {
                    type.Value.Seal();
                }

                CategorizedRecords = recordsByType.Values.OrderByDescending(m => m.TotalLength).ToArray();
            }

            public override string ToString()
            {
                return $"{Type.PadRight(30, ' ')}\t\t\tTotal size: {TotalLength:N0}\t\t\tCount: {Count:N0}";
            }
        }

        private void Process(IEnumerable<Record> records)
        {
            var recordsByType = new RecordsByType("Statistics:");

            foreach (var record in records)
            {
                var args = record.Args;
                if (args == null)
                {
                    // auxiliary (data storage) record, such as String, Blob or NameValueList
                    continue;
                }

                string argsType = args.GetType().Name;
                if (argsType.EndsWith("EventArgs"))
                {
                    argsType = argsType.Substring(0, argsType.Length - 9);
                }

                recordsByType.Add(record, argsType);
            }

            recordsByType.Seal();

            CategorizedRecords = recordsByType;
        }

        private static string GetMessageType(string message, BuildEventArgs args)
        {
            if (message != "BuildMessage")
            {
                return message; 
            }

            message = args.Message;
            if (message == null || message.Length < 50)
            {
                return "BuildMessage";
            }

            var first = message.Substring(0, 10);
            switch (first)
            {
                case "Output Ite":
                    return "Task Output Item";
                case "Task Param":
                    return "Task Input Item";
                case "Added Item":
                    return "Added Item";
                case "Removed It":
                    return "Removed Item";
                default:
                    break;
            }

            return "BuildMessage";
        }
    }
}
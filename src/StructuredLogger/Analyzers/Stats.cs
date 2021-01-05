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

        public IReadOnlyList<RecordsByType> CategorizedRecords { get; private set; }

        public class RecordsByType
        {
            private readonly List<Record> list = new List<Record>();

            public RecordsByType(string type)
            {
                this.Type = type;
            }

            public static RecordsByType Create(string type)
            {
                if (type == "BuildMessage")
                {
                    return new MessageRecords();
                }

                return new RecordsByType(type);
            }

            public string Type { get; }
            public long TotalLength { get; private set; }
            public int Count { get; private set; }

            public virtual void Add(Record record)
            {
                list.Add(record);
                Count += 1;
                TotalLength += record.Length;
            }

            public virtual void Seal()
            {
                list.Sort((l, r) => Math.Sign(r.Length - l.Length));
            }

            public override string ToString()
            {
                return $"{Type.PadRight(30, ' ')}\t\t\tTotal size: {TotalLength:N0}\t\t\tCount: {Count:N0}";
            }
        }

        public class MessageRecords : RecordsByType
        {
            Dictionary<string, RecordsByType> recordsByType = new Dictionary<string, RecordsByType>();
            public IReadOnlyList<RecordsByType> CategorizedRecords { get; private set; }

            public MessageRecords() : base("BuildMessage")
            {
            }

            public override void Add(Record record)
            {
                base.Add(record);

                string messageType = GetMessageType(record.Args.Message);
                if (!recordsByType.TryGetValue(messageType, out var bucket))
                {
                    bucket = Create(messageType);
                    recordsByType[messageType] = bucket;
                }

                bucket.Add(record);
            }

            public override void Seal()
            {
                base.Seal();

                foreach (var type in recordsByType)
                {
                    type.Value.Seal();
                }

                CategorizedRecords = recordsByType.Values.OrderByDescending(m => m.TotalLength).ToArray();
            }
        }

        private void Process(IEnumerable<Record> records)
        {
            var recordsByType = new Dictionary<string, RecordsByType>();

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

                if (!recordsByType.TryGetValue(argsType, out var bucket))
                {
                    bucket = RecordsByType.Create(argsType);
                    recordsByType[argsType] = bucket;
                }

                bucket.Add(record);
            }

            foreach (var type in recordsByType)
            {
                type.Value.Seal();
            }

            CategorizedRecords = recordsByType.Values.OrderByDescending(v => v.TotalLength).ToArray();
        }

        private static string GetMessageType(string message)
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
    }
}
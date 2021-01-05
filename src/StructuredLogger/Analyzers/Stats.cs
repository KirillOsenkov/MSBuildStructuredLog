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
            var stats = new BinlogStats();

            var reader = new BinLogReader();
            reader.OnBlobRead += (kind, bytes) => stats.OnBlobRead(kind, bytes);
            reader.OnStringRead += text => stats.OnStringRead(text);
            reader.OnNameValueListRead += list => stats.OnNameValueListRead(list);

            var records = reader.ReadRecords(binlogFilePath);
            stats.Process(records);
            return stats;
        }

        public int NameValueListCount;
        public long NameValueListTotalSize;
        public int NameValueListLargest;

        public int StringCount;
        public long StringTotalSize;
        public int StringLargest;

        public int BlobCount;
        public int BlobTotalSize;
        public int BlobLargest;

        private void OnNameValueListRead(IReadOnlyList<KeyValuePair<string, string>> list)
        {
            NameValueListCount += 1;
            var size = list.Sum(kvp => kvp.Key.Length * 2 + kvp.Value.Length * 2);
            NameValueListTotalSize += size;
            NameValueListLargest = Math.Max(NameValueListLargest, size);
        }

        private void OnStringRead(string text)
        {
            StringCount += 1;
            int length = text.Length * 2;
            StringTotalSize += length;
            StringLargest = Math.Max(StringLargest, length);
        }

        private void OnBlobRead(BinaryLogRecordKind kind, byte[] bytes)
        {
            BlobCount += 1;
            BlobTotalSize += bytes.Length;
            BlobLargest = Math.Max(BlobLargest, bytes.Length);
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
            public int Largest { get; private set; }

            public virtual void Add(Record record, string type = null)
            {
                if (type != null)
                {
                    if (!recordsByType.TryGetValue(type, out var bucket))
                    {
                        bucket = Create(type);
                        recordsByType[type] = bucket;
                    }

                    type = GetMessageSubType(type, record.Args);
                    bucket.Add(record, type);
                }
                else
                {
                    list.Add(record);
                }

                Count += 1;
                TotalLength += record.Length;
                Largest = Math.Max(Largest, (int)record.Length);
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
                return GetString(Type, TotalLength, Count, Largest);
            }
        }

        public static string GetString(string name, long total, int count, int largest)
        {
            return $"{name.PadRight(60, ' ')}\t\t\tTotal size: {total:N0}\t\t\tCount: {count:N0}\t\t\tLargest: {largest:N0}";
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

        private static string GetMessageSubType(string message, BuildEventArgs args)
        {
            if (message != "BuildMessage")
            {
                return null;
            }

            message = args.Message;
            if (message == null || message.Length < 50)
            {
                return "Other";
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
                    return "Other";
            }
        }
    }
}
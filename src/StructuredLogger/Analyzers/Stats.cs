using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class BinlogStats
    {
        private static bool TrackStrings = true;
        private static bool Sort = true;

        public static BinlogStats Calculate(string binlogFilePath)
        {
            var stats = new BinlogStats();
            stats.FileSize = new FileInfo(binlogFilePath).Length;

            bool expensive = stats.FileSize > 20_000_000;
            TrackStrings = !expensive;
            Sort = !expensive;

            var reader = new BinLogReader();
            reader.OnBlobRead += (kind, bytes) => stats.OnBlobRead(kind, bytes);
            reader.OnStringRead += (text, lengthBytes) => stats.OnStringRead(text, lengthBytes);
            reader.OnNameValueListRead += (list, recordLengthBytes) => stats.OnNameValueListRead(list, recordLengthBytes);

            var records = reader.ReadRecords(binlogFilePath);
            stats.Process(records);
            return stats;
        }

        public long FileSize;
        public long UncompressedStreamSize;
        public long RecordCount;

        public int NameValueListCount;
        public long NameValueListTotalSize;
        public int NameValueListLargest;

        public int StringCount;
        public long StringTotalSize;
        public int StringLargest;

        public int BlobCount;
        public int BlobTotalSize;
        public int BlobLargest;

        private void OnNameValueListRead(IDictionary<string, string> list, long recordLengthBytes)
        {
            UncompressedStreamSize += recordLengthBytes;
            NameValueListCount += 1;
            var size = (int)recordLengthBytes;
            NameValueListTotalSize += size;
            NameValueListLargest = Math.Max(NameValueListLargest, size);
        }

        public List<int> StringSizes = new List<int>();

        public List<string> AllStrings = new List<string>();

        private void OnStringRead(string text, long lengthInBytes)
        {
            if (TrackStrings)
            {
                AllStrings.Add(text);
            }

            int length = (int)lengthInBytes;

            UncompressedStreamSize += lengthInBytes;

            if (TrackStrings)
            {
                if (StringSizes.Count <= length)
                {
                    for (int i = StringSizes.Count; i <= length; i++)
                    {
                        StringSizes.Add(0);
                    }
                }

                StringSizes[length] += 1;
            }

            StringCount += 1;
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

            public virtual void Add(Record record, string type = null, BinlogStats stats = null)
            {
                if (type != null)
                {
                    if (!recordsByType.TryGetValue(type, out var bucket))
                    {
                        bucket = Create(type);
                        recordsByType[type] = bucket;
                    }

                    type = stats.GetSubType(type, record.Args);
                    bucket.Add(record, type, stats);
                }
                else
                {
                    list.Add(record);
                }

                Count += 1;
                TotalLength += record.Length;
                Largest = Math.Max(Largest, (int)record.Length);
            }

            public IEnumerable<Record> Records => list;

            public virtual void Seal()
            {
                if (Sort)
                {
                    list.Sort((l, r) =>
                    {
                        if (l == null || r == null)
                        {
                            return 0;
                        }

                        if (r.Args is BuildMessageEventArgs rightMessageArgs &&
                            l.Args is BuildMessageEventArgs leftMessageArgs &&
                            rightMessageArgs.Message is string rightMessage &&
                            leftMessageArgs.Message is string leftMessage)
                        {
                            return Math.Sign(rightMessage.Length - leftMessage.Length);
                        }

                        if (r.Length != l.Length)
                        {
                            return Math.Sign(r.Length - l.Length);
                        }

                        return 0;
                    });
                }

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
            return $"{name.PadRight(30, ' ')}\t\t\tTotal size: {total:N0}\t\t\tCount: {count:N0}\t\t\tLargest: {largest:N0}";
        }

        private Dictionary<(int node, int target), string> targetNamesById = new Dictionary<(int node, int target), string>();
        private Dictionary<(int node, int task), string> taskNamesById = new Dictionary<(int node, int task), string>();

        private void Process(IEnumerable<Record> records)
        {
            var recordsByType = new RecordsByType(Strings.Statistics + ":");

            long totalSize = 0;
            long recordCount = 0;

            foreach (var record in records)
            {
                totalSize += record.Length;
                recordCount++;

                var args = record.Args;
                if (args == null)
                {
                    // auxiliary (data storage) record, such as String, Blob or NameValueList
                    continue;
                }

                if (args is TargetStartedEventArgs targetStarted)
                {
                    var context = targetStarted.BuildEventContext;
                    targetNamesById[(context.NodeId, context.TargetId)] = targetStarted.TargetName;
                }
                else if (args is TaskStartedEventArgs taskStarted)
                {
                    var context = taskStarted.BuildEventContext;
                    taskNamesById[(context.NodeId, context.TaskId)] = taskStarted.TaskName;
                }

                string argsType = args.GetType().Name;
                if (argsType.EndsWith("EventArgs"))
                {
                    argsType = argsType.Substring(0, argsType.Length - 9);
                }

                recordsByType.Add(record, argsType, this);
            }

            UncompressedStreamSize += totalSize;
            RecordCount = recordCount;

            recordsByType.Seal();

            CategorizedRecords = recordsByType;

            AllStrings.Sort((l, r) => r.Length == l.Length ? string.CompareOrdinal(l, r) : r.Length - l.Length);
        }

        private string GetSubType(string message, BuildEventArgs args)
        {
            if (message == "BuildMessage")
            {
                var context = args.BuildEventContext;
                if (context.EvaluationId != -1)
                {
                    return "BuildMessage/Evaluation";
                }

                if (context.TaskId != BuildEventContext.InvalidTaskId)
                {
                    return "BuildMessage/Task";
                }

                if (context.TargetId != BuildEventContext.InvalidTargetId)
                {
                    return "BuildMessage/Target";
                }

                if (context.ProjectContextId != BuildEventContext.InvalidProjectContextId)
                {
                    return "BuildMessage/ProjectContext";
                }

                if (context.ProjectInstanceId != BuildEventContext.InvalidProjectInstanceId)
                {
                    return "BuildMessage/ProjectInstance";
                }

                return "BuildMessage/Other";
            }
            else if (message == "TaskParameter")
            {
                if (args.BuildEventContext.TaskId != BuildEventContext.InvalidTaskId)
                {
                    return "TaskParameter/Task";
                }
                else
                {
                    return "TaskParameter/Target";
                }
            }
            else if (message == "TaskParameter/Task" || message == "TaskParameter/Target")
            {
                return GetTaskParameterSubType(message, (TaskParameterEventArgs)args);
            }
            else if (
                message == "BuildMessage/Evaluation" ||
                message == "BuildMessage/Task" ||
                message == "BuildMessage/Target" ||
                message == "BuildMessage/ProjectContext" ||
                message == "BuildMessage/ProjectInstance" ||
                message == "BuildMessage/Other")
            {
                return GetMessageSubType(message, args);
            }

            return null;
        }

        private string GetTaskParameterSubType(string message, TaskParameterEventArgs args)
        {
            var context = args.BuildEventContext;
            if (args.Kind == TaskParameterMessageKind.TaskInput || args.Kind == TaskParameterMessageKind.TaskOutput)
            {
                taskNamesById.TryGetValue((context.NodeId, context.TaskId), out var name);
                return $"Task {name} {args.Kind} {args.ItemType}";
            }
            else
            {
                targetNamesById.TryGetValue((context.NodeId, context.TargetId), out var name);
                return $"Target {name} {args.Kind} {args.ItemType}";
            }
        }

        private static string GetMessageSubType(string message, BuildEventArgs args)
        {
            message = args.Message;
            if (message == null)
            {
                return "null";
            }

            if (message.Length < 50)
            {
                return "Short";
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
                case "Output Pro":
                    return "Output Property";
                case "Set Proper":
                    return "Set Property";
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
                case "    Refere":
                    return "References which depend on";
                case "    This r":
                    return "This reference is not";
                case "(in) Annot":
                    return "GetReferenceNearestTargetFrameworkTask (in) Annotated";
                case "(out) Assi":
                    return "GetReferenceNearestTargetFrameworkTask (out) Assigned";
                case "The target":
                    return "Target not found";
                case "Trying to ":
                    return "Trying to import";
                case "Copying fi":
                    return "Copying file";
                case "Task \"Warn":
                    return "Task skipped";
                case "Task \"Erro":
                    return "Task skipped";
                case "Task \"MSBu":
                    return "Task skipped";
                case "Task \"GetR":
                    return "Task skipped";
                case "Task \"Reso":
                    return "Task skipped";
                case "Task \"Writ":
                    return "Task skipped";
                case "Task \"Crea":
                    return "Task skipped";
                case "Task \"Gene":
                    return "Task skipped";
                case "Task \"Copy":
                    return "Task skipped";
                case "Task \"Work":
                    return "Task skipped";
                case "Task \"NETS":
                    return "Task skipped";
                case "Task \"NETB":
                    return "Task skipped";
                case "Task \"Assi":
                    return "Task skipped";
                case "        Hi":
                    return "Hintpath";
                default:
                    break;
            }

            return "Other";
        }
    }
}
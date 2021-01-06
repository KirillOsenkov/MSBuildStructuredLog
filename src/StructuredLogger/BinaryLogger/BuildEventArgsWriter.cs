using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Framework;
using Microsoft.Build.Framework.Profiler;
using Microsoft.Build.Internal;

namespace Microsoft.Build.Logging.StructuredLogger
{
    /// <summary>
    /// Serializes BuildEventArgs-derived objects into a provided BinaryWriter
    /// </summary>
    internal class BuildEventArgsWriter
    {
        private readonly Stream originalStream;
        private readonly MemoryStream currentRecordStream;

        private readonly BinaryWriter originalBinaryWriter;
        private readonly BinaryWriter currentRecordWriter;
        private BinaryWriter binaryWriter;

        /// <summary>
        /// Initializes a new instance of BuildEventArgsWriter with a BinaryWriter
        /// </summary>
        /// <param name="binaryWriter">A BinaryWriter to write the BuildEventArgs instances to</param>
        public BuildEventArgsWriter(BinaryWriter binaryWriter)
        {
            this.originalStream = binaryWriter.BaseStream;
            this.currentRecordStream = new MemoryStream(65536);

            this.originalBinaryWriter = binaryWriter;
            this.currentRecordWriter = new BinaryWriter(currentRecordStream);

            this.binaryWriter = currentRecordWriter;
        }

        /// <summary>
        /// Write a provided instance of BuildEventArgs to the BinaryWriter
        /// </summary>
        public void Write(BuildEventArgs e)
        {
            WriteCore(e);
            currentRecordStream.WriteTo(originalStream);
            currentRecordStream.SetLength(0);
        }

        private void WriteCore(BuildEventArgs e)
        {
            // the cases are ordered by most used first for performance
            if (e is BuildMessageEventArgs)
            {
                Write((BuildMessageEventArgs)e);
            }
            else if (e is TaskStartedEventArgs)
            {
                Write((TaskStartedEventArgs)e);
            }
            else if (e is TaskFinishedEventArgs)
            {
                Write((TaskFinishedEventArgs)e);
            }
            else if (e is TargetStartedEventArgs)
            {
                Write((TargetStartedEventArgs)e);
            }
            else if (e is TargetFinishedEventArgs)
            {
                Write((TargetFinishedEventArgs)e);
            }
            else if (e is BuildErrorEventArgs)
            {
                Write((BuildErrorEventArgs)e);
            }
            else if (e is BuildWarningEventArgs)
            {
                Write((BuildWarningEventArgs)e);
            }
            else if (e is ProjectStartedEventArgs)
            {
                Write((ProjectStartedEventArgs)e);
            }
            else if (e is ProjectFinishedEventArgs)
            {
                Write((ProjectFinishedEventArgs)e);
            }
            else if (e is BuildStartedEventArgs)
            {
                Write((BuildStartedEventArgs)e);
            }
            else if (e is BuildFinishedEventArgs)
            {
                Write((BuildFinishedEventArgs)e);
            }
            else if (e is ProjectEvaluationStartedEventArgs)
            {
                Write((ProjectEvaluationStartedEventArgs)e);
            }
            else if (e is ProjectEvaluationFinishedEventArgs)
            {
                Write((ProjectEvaluationFinishedEventArgs)e);
            }
            else
            {
                // convert all unrecognized objects to message
                // and just preserve the message
                var buildMessageEventArgs = new BuildMessageEventArgs(
                    e.Message,
                    e.HelpKeyword,
                    e.SenderName,
                    MessageImportance.Normal,
                    e.Timestamp);
                buildMessageEventArgs.BuildEventContext = e.BuildEventContext ?? BuildEventContext.Invalid;
                Write(buildMessageEventArgs);
            }
        }

        public void WriteBlob(BinaryLogRecordKind kind, byte[] bytes)
        {
            Write(kind);
            Write(bytes.Length);
            Write(bytes);
        }

        private void Write(BuildStartedEventArgs e)
        {
            Write(BinaryLogRecordKind.BuildStarted);
            WriteBuildEventArgsFields(e);
            Write(e.BuildEnvironment);
        }

        private void Write(BuildFinishedEventArgs e)
        {
            Write(BinaryLogRecordKind.BuildFinished);
            WriteBuildEventArgsFields(e);
            Write(e.Succeeded);
        }

        private void Write(ProjectEvaluationStartedEventArgs e)
        {
            Write(BinaryLogRecordKind.ProjectEvaluationStarted);
            WriteBuildEventArgsFields(e);
            Write(e.ProjectFile);
        }

        private void Write(ProjectEvaluationFinishedEventArgs e)
        {
            Write(BinaryLogRecordKind.ProjectEvaluationFinished);

            WriteBuildEventArgsFields(e);
            Write(e.ProjectFile);

            Write(e.ProfilerResult.HasValue);
            if (e.ProfilerResult.HasValue)
            {
                Write(e.ProfilerResult.Value.ProfiledLocations.Count);

                foreach (var item in e.ProfilerResult.Value.ProfiledLocations)
                {
                    Write(item.Key);
                    Write(item.Value);
                }
            }
        }

        private void Write(ProjectStartedEventArgs e)
        {
            Write(BinaryLogRecordKind.ProjectStarted);
            WriteBuildEventArgsFields(e);

            if (e.ParentProjectBuildEventContext == null)
            {
                Write(false);
            }
            else
            {
                Write(true);
                Write(e.ParentProjectBuildEventContext);
            }

            WriteDeduplicatedString(e.ProjectFile);

            Write(e.ProjectId);
            WriteDeduplicatedString(e.TargetNames);
            WriteDeduplicatedString(e.ToolsVersion);

            if (e.GlobalProperties == null)
            {
                Write(false);
            }
            else
            {
                Write(true);
                Write(e.GlobalProperties);
            }

            WriteProperties(e.Properties);

            WriteProjectItems(e.Items);
        }

        private void Write(ProjectFinishedEventArgs e)
        {
            Write(BinaryLogRecordKind.ProjectFinished);
            WriteBuildEventArgsFields(e);
            WriteOptionalString(e.ProjectFile);
            Write(e.Succeeded);
        }

        private void Write(TargetStartedEventArgs e)
        {
            Write(BinaryLogRecordKind.TargetStarted);
            WriteBuildEventArgsFields(e);
            WriteOptionalString(e.TargetName);
            WriteOptionalString(e.ProjectFile);
            WriteOptionalString(e.TargetFile);
            WriteOptionalString(e.ParentTarget);
            Write((int) e.BuildReason);
        }

        private void Write(TargetFinishedEventArgs e)
        {
            Write(BinaryLogRecordKind.TargetFinished);
            WriteBuildEventArgsFields(e);
            Write(e.Succeeded);
            WriteOptionalString(e.ProjectFile);
            WriteOptionalString(e.TargetFile);
            WriteOptionalString(e.TargetName);
            WriteTaskItemList(e.TargetOutputs);
        }

        private void Write(TaskStartedEventArgs e)
        {
            Write(BinaryLogRecordKind.TaskStarted);
            WriteBuildEventArgsFields(e);
            WriteOptionalString(e.TaskName);
            WriteOptionalString(e.ProjectFile);
            WriteOptionalString(e.TaskFile);
        }

        private void Write(TaskFinishedEventArgs e)
        {
            Write(BinaryLogRecordKind.TaskFinished);
            WriteBuildEventArgsFields(e);
            Write(e.Succeeded);
            WriteOptionalString(e.TaskName);
            WriteOptionalString(e.ProjectFile);
            WriteOptionalString(e.TaskFile);
        }

        private void Write(BuildErrorEventArgs e)
        {
            Write(BinaryLogRecordKind.Error);
            WriteBuildEventArgsFields(e);
            WriteOptionalString(e.Subcategory);
            WriteOptionalString(e.Code);
            WriteOptionalString(e.File);
            WriteOptionalString(e.ProjectFile);
            Write(e.LineNumber);
            Write(e.ColumnNumber);
            Write(e.EndLineNumber);
            Write(e.EndColumnNumber);
        }

        private void Write(BuildWarningEventArgs e)
        {
            Write(BinaryLogRecordKind.Warning);
            WriteBuildEventArgsFields(e);
            WriteOptionalString(e.Subcategory);
            WriteOptionalString(e.Code);
            WriteOptionalString(e.File);
            WriteOptionalString(e.ProjectFile);
            Write(e.LineNumber);
            Write(e.ColumnNumber);
            Write(e.EndLineNumber);
            Write(e.EndColumnNumber);
        }

        private void Write(BuildMessageEventArgs e)
        {
            if (e is CriticalBuildMessageEventArgs)
            {
                Write((CriticalBuildMessageEventArgs)e);
                return;
            }

            if (e is TaskCommandLineEventArgs)
            {
                Write((TaskCommandLineEventArgs)e);
                return;
            }

            if (e is ProjectImportedEventArgs)
            {
                Write((ProjectImportedEventArgs)e);
                return;
            }

            if (e is TargetSkippedEventArgs)
            {
                Write((TargetSkippedEventArgs)e);
                return;
            }

            if (e is PropertyReassignmentEventArgs)
            {
                Write((PropertyReassignmentEventArgs)e);
                return;
            }

            if (e is UninitializedPropertyReadEventArgs)
            {
                Write((UninitializedPropertyReadEventArgs)e);
                return;
            }

            if (e is EnvironmentVariableReadEventArgs)
            {
                Write((EnvironmentVariableReadEventArgs)e);
                return;
            }

            if (e is PropertyInitialValueSetEventArgs)
            {
                Write((PropertyInitialValueSetEventArgs)e);
                return;
            }

            Write(BinaryLogRecordKind.Message);
            WriteMessageFields(e);
        }

        private void Write(ProjectImportedEventArgs e)
        {
            Write(BinaryLogRecordKind.ProjectImported);
            WriteMessageFields(e);
            Write(e.ImportIgnored);
            WriteOptionalString(e.ImportedProjectFile);
            WriteOptionalString(e.UnexpandedProject);
        }

        private void Write(TargetSkippedEventArgs e)
        {
            Write(BinaryLogRecordKind.TargetSkipped);
            WriteMessageFields(e);
            WriteOptionalString(e.TargetFile);
            WriteOptionalString(e.TargetName);
            WriteOptionalString(e.ParentTarget);
            Write((int)e.BuildReason);
        }

        private void Write(CriticalBuildMessageEventArgs e)
        {
            Write(BinaryLogRecordKind.CriticalBuildMessage);
            WriteMessageFields(e);
        }

        private void Write(PropertyReassignmentEventArgs e)
        {
            Write(BinaryLogRecordKind.PropertyReassignment);
            WriteMessageFields(e);
            Write(e.PropertyName);
            Write(e.PreviousValue);
            Write(e.NewValue);
            Write(e.Location);
        }

        private void Write(UninitializedPropertyReadEventArgs e)
        {
            Write(BinaryLogRecordKind.UninitializedPropertyRead);
            WriteMessageFields(e);
            Write(e.PropertyName);
        }

        private void Write(PropertyInitialValueSetEventArgs e)
        {
            Write(BinaryLogRecordKind.PropertyInitialValueSet);
            WriteMessageFields(e);
            Write(e.PropertyName);
            Write(e.PropertyValue);
            Write(e.PropertySource);
        }

        private void Write(EnvironmentVariableReadEventArgs e)
        {
            Write(BinaryLogRecordKind.EnvironmentVariableRead);
            WriteMessageFields(e);
            Write(e.EnvironmentVariableName);
        }

        private void Write(TaskCommandLineEventArgs e)
        {
            Write(BinaryLogRecordKind.TaskCommandLine);
            WriteMessageFields(e);
            WriteOptionalString(e.CommandLine);
            WriteOptionalString(e.TaskName);
        }

        private void WriteBuildEventArgsFields(BuildEventArgs e)
        {
            var flags = GetBuildEventArgsFieldFlags(e);
            Write((int)flags);
            WriteBaseFields(e, flags);
        }

        private void WriteBaseFields(BuildEventArgs e, BuildEventArgsFieldFlags flags)
        {
            if ((flags & BuildEventArgsFieldFlags.Message) != 0)
            {
                WriteDeduplicatedString(e.Message);
            }

            if ((flags & BuildEventArgsFieldFlags.BuildEventContext) != 0)
            {
                Write(e.BuildEventContext);
            }

            if ((flags & BuildEventArgsFieldFlags.ThreadId) != 0)
            {
                Write(e.ThreadId);
            }

            if ((flags & BuildEventArgsFieldFlags.HelpHeyword) != 0)
            {
                Write(e.HelpKeyword);
            }

            if ((flags & BuildEventArgsFieldFlags.SenderName) != 0)
            {
                Write(e.SenderName);
            }

            if ((flags & BuildEventArgsFieldFlags.Timestamp) != 0)
            {
                Write(e.Timestamp);
            }
        }

        private void WriteMessageFields(BuildMessageEventArgs e)
        {
            var flags = GetBuildEventArgsFieldFlags(e);
            flags = GetMessageFlags(e, flags);

            Write((int)flags);

            WriteBaseFields(e, flags);

            if ((flags & BuildEventArgsFieldFlags.Subcategory) != 0)
            {
                WriteDeduplicatedString(e.Subcategory);
            }

            if ((flags & BuildEventArgsFieldFlags.Code) != 0)
            {
                WriteDeduplicatedString(e.Code);
            }

            if ((flags & BuildEventArgsFieldFlags.File) != 0)
            {
                WriteDeduplicatedString(e.File);
            }

            if ((flags & BuildEventArgsFieldFlags.ProjectFile) != 0)
            {
                WriteDeduplicatedString(e.ProjectFile);
            }

            if ((flags & BuildEventArgsFieldFlags.LineNumber) != 0)
            {
                Write(e.LineNumber);
            }

            if ((flags & BuildEventArgsFieldFlags.ColumnNumber) != 0)
            {
                Write(e.ColumnNumber);
            }

            if ((flags & BuildEventArgsFieldFlags.EndLineNumber) != 0)
            {
                Write(e.EndLineNumber);
            }

            if ((flags & BuildEventArgsFieldFlags.EndColumnNumber) != 0)
            {
                Write(e.EndColumnNumber);
            }

            Write((int)e.Importance);
        }

        private static BuildEventArgsFieldFlags GetMessageFlags(BuildMessageEventArgs e, BuildEventArgsFieldFlags flags)
        {
            if (e.Subcategory != null)
            {
                flags |= BuildEventArgsFieldFlags.Subcategory;
            }

            if (e.Code != null)
            {
                flags |= BuildEventArgsFieldFlags.Code;
            }

            if (e.File != null)
            {
                flags |= BuildEventArgsFieldFlags.File;
            }

            if (e.ProjectFile != null)
            {
                flags |= BuildEventArgsFieldFlags.ProjectFile;
            }

            if (e.LineNumber != 0)
            {
                flags |= BuildEventArgsFieldFlags.LineNumber;
            }

            if (e.ColumnNumber != 0)
            {
                flags |= BuildEventArgsFieldFlags.ColumnNumber;
            }

            if (e.EndLineNumber != 0)
            {
                flags |= BuildEventArgsFieldFlags.EndLineNumber;
            }

            if (e.EndColumnNumber != 0)
            {
                flags |= BuildEventArgsFieldFlags.EndColumnNumber;
            }

            return flags;
        }

        private static BuildEventArgsFieldFlags GetBuildEventArgsFieldFlags(BuildEventArgs e)
        {
            var flags = BuildEventArgsFieldFlags.None;
            if (e.BuildEventContext != null)
            {
                flags |= BuildEventArgsFieldFlags.BuildEventContext;
            }

            if (e.HelpKeyword != null)
            {
                flags |= BuildEventArgsFieldFlags.HelpHeyword;
            }

            if (!string.IsNullOrEmpty(e.Message))
            {
                flags |= BuildEventArgsFieldFlags.Message;
            }

            // no need to waste space for the default sender name
            if (e.SenderName != null && e.SenderName != "MSBuild")
            {
                flags |= BuildEventArgsFieldFlags.SenderName;
            }

            if (e.ThreadId > 0)
            {
                flags |= BuildEventArgsFieldFlags.ThreadId;
            }

            if (e.Timestamp != default(DateTime))
            {
                flags |= BuildEventArgsFieldFlags.Timestamp;
            }

            return flags;
        }

        private void WriteTaskItemList(IEnumerable items)
        {
            var taskItems = items as IEnumerable<ITaskItem>;
            if (taskItems == null)
            {
                Write(false);
                return;
            }

            Write(taskItems.Count());

            foreach (var item in taskItems)
            {
                Write(item);
            }
        }

        private void WriteProjectItems(IEnumerable items)
        {
            if (items == null)
            {
                Write(0);
                return;
            }

            var groups = items
                .OfType<DictionaryEntry>()
                .GroupBy(entry => entry.Key as string, entry => entry.Value as ITaskItem)
                .Where(group => !string.IsNullOrEmpty(group.Key))
                .ToArray();

            Write(groups.Length);

            foreach (var group in groups)
            {
                Write(group.Key);
                WriteTaskItemList(group);
            }
        }

        private void Write(ITaskItem item)
        {
            Write(item.ItemSpec);

            if (nameValueList.Count > 0)
            {
                nameValueList.Clear();
            }

            IDictionary customMetadata = item.CloneCustomMetadata();

            foreach (string metadataName in customMetadata.Keys)
            {
                string valueOrError;

                try
                {
                    valueOrError = item.GetMetadata(metadataName);
                }
                catch (InvalidProjectFileException e)
                {
                    valueOrError = e.Message;
                }
                // Temporarily try catch all to mitigate frequent NullReferenceExceptions in
                // the logging code until CopyOnWritePropertyDictionary is replaced with
                // ImmutableDictionary. Calling into Debug.Fail to crash the process in case
                // the exception occures in Debug builds.
                catch (Exception e)
                {
                    valueOrError = e.Message;
                    Debug.Fail(e.ToString());
                }

                nameValueList.Add(new KeyValuePair<string, string>(metadataName, valueOrError));
            }

            WriteNameValueList();
        }

        private void WriteProperties(IEnumerable properties)
        {
            if (properties == null)
            {
                Write(0);
                return;
            }

            if (nameValueList.Count > 0)
            {
                nameValueList.Clear();
            }

            // there are no guarantees that the properties iterator won't change, so 
            // take a snapshot and work with the readonly copy
            var propertiesArray = properties.OfType<DictionaryEntry>().ToArray();

            for (int i = 0; i < propertiesArray.Length; i++)
            {
                DictionaryEntry entry = propertiesArray[i];
                if (entry.Key is string key && entry.Value is string value)
                {
                    nameValueList.Add(new KeyValuePair<string, string>(key, value));
                }
                else
                {
                    nameValueList.Add(new KeyValuePair<string, string>(string.Empty, string.Empty));
                }
            }

            WriteNameValueList();
        }

        private void Write(BuildEventContext buildEventContext)
        {
            Write(buildEventContext.NodeId);
            Write(buildEventContext.ProjectContextId);
            Write(buildEventContext.TargetId);
            Write(buildEventContext.TaskId);
            Write(buildEventContext.SubmissionId);
            Write(buildEventContext.ProjectInstanceId);
            Write(buildEventContext.EvaluationId);
        }

        private readonly List<KeyValuePair<string, string>> nameValueList = new List<KeyValuePair<string, string>>(1024);
        private readonly List<KeyValuePair<int, int>> nameValueIndexList = new List<KeyValuePair<int, int>>(1024);
        private readonly Dictionary<HashKey, int> hashes = new Dictionary<HashKey, int>();

        internal const int NameValueRecordStartIndex = 10;
        private int nameValueRecordId = NameValueRecordStartIndex;

        internal struct HashKey : IEquatable<HashKey>
        {
            private int Int32;

            private HashKey(int i)
            {
                Int32 = i;
            }

            public HashKey(string text)
            {
                if (text == null)
                {
                    Int32 = -1;
                }
                else
                {
                    Int32 = text.GetHashCode();
                }
            }

            public static HashKey Combine(HashKey left, HashKey right)
            {
                return new HashKey((left.Int32, right.Int32).GetHashCode());
            }

            public HashKey Add(HashKey other) => Combine(this, other);

            public bool Equals(HashKey other)
            {
                return Int32 == other.Int32;
            }

            public override bool Equals(object obj)
            {
                if (obj is HashKey other)
                {
                    return Equals(other);
                }

                return false;
            }

            public override int GetHashCode()
            {
                return Int32;
            }

            public override string ToString()
            {
                return Int32.ToString();
            }
        }

        private void Write(IEnumerable<KeyValuePair<string, string>> keyValuePairs)
        {
            if (nameValueList.Count > 0)
            {
                nameValueList.Clear();
            }

            foreach (var kvp in keyValuePairs)
            {
                nameValueList.Add(kvp);
            }

            WriteNameValueList();
        }

        private void WriteNameValueList()
        {
            if (nameValueList.Count == 0)
            {
                Write((byte)0);
                return;
            }

            HashKey hash = HashAllStrings(nameValueList);
            if (!hashes.TryGetValue(hash, out var recordId))
            {
                recordId = nameValueRecordId;
                hashes[hash] = nameValueRecordId;

                WriteNameValueListRecord();

                nameValueRecordId += 1;
            }

            // length of our fake list is 1
            Write(1);

            // A special convention to reference a previously written list:
            // write a list with a single item where the key is a string consisting of a single 0 byte
            // and the value is the hash of the previous record
            Write((byte)1);
            Write((byte)0);
            Write(recordId.ToString());
        }

        /// <summary>
        /// In the middle of writing the current record we may discover that we want to write another record
        /// preceding the current one, specifically the list of names and values we want to reuse in the
        /// future. As we are writing the current record to a MemoryStream first, it's OK to temporarily
        /// switch to the direct underlying stream and write the NameValueList record first.
        /// When the current record is done writing, the MemoryStream will flush to the underlying stream
        /// and the current record will end up after the NameValueList record, as desired.
        /// </summary>
        private void WriteNameValueListRecord()
        {
            try
            {
                // Switch the binaryWriter used by the Write* methods to the direct underlying stream writer.
                // We want this record to precede the record we're currently writing to currentRecordWriter
                // which is backed by a MemoryStream buffer
                binaryWriter = this.originalBinaryWriter;

                Write(BinaryLogRecordKind.NameValueList);
                Write(nameValueIndexList.Count);
                for (int i = 0; i < nameValueList.Count; i++)
                {
                    var kvp = nameValueIndexList[i];
                    Write(kvp.Key);
                    Write(kvp.Value);
                }
            }
            finally
            {
                // switch back to continue writing the current record to the memory stream
                binaryWriter = this.currentRecordWriter;
            }
        }

        private HashKey HashAllStrings(List<KeyValuePair<string, string>> nameValueList)
        {
            HashKey hash = new HashKey();

            nameValueIndexList.Clear();

            for (int i = 0; i < nameValueList.Count; i++)
            {
                var kvp = nameValueList[i];
                var (keyIndex, keyHash) = HashString(kvp.Key);
                var (valueIndex, valueHash) = HashString(kvp.Value);
                hash = hash.Add(keyHash);
                hash = hash.Add(valueHash);
                nameValueIndexList.Add(new KeyValuePair<int, int>(keyIndex, valueIndex));
            }

            return hash;
        }

        private void Write(BinaryLogRecordKind kind)
        {
            Write((int)kind);
        }

        private void Write(int value)
        {
            Write7BitEncodedInt(binaryWriter, value);
        }

        private void Write(long value)
        {
            binaryWriter.Write(value);
        }

        private void Write7BitEncodedInt(BinaryWriter writer, int value)
        {
            // Write out an int 7 bits at a time.  The high bit of the byte,
            // when on, tells reader to continue reading more bytes.
            uint v = (uint)value;   // support negative numbers
            while (v >= 0x80)
            {
                writer.Write((byte)(v | 0x80));
                v >>= 7;
            }
            writer.Write((byte)v);
        }

        private void Write(byte[] bytes)
        {
            binaryWriter.Write(bytes);
        }

        private void Write(byte b)
        {
            binaryWriter.Write(b);
        }

        private void Write(bool boolean)
        {
            binaryWriter.Write(boolean);
        }

        private readonly Dictionary<HashKey, int> stringHashes = new Dictionary<HashKey, int>();

        internal const int StringStartIndex = 10;

        /// <summary>
        /// 0 is null, 1 is empty string
        /// 2-9 are reserved for future use.
        /// Start indexing at 10.
        /// </summary>
        private int stringRecordId = StringStartIndex;

        private void WriteDeduplicatedString(string text)
        {
            var (recordId, _) = HashString(text);
            Write(recordId);
        }

        private (int index, HashKey hash) HashString(string text)
        {
            if (text == null)
            {
                return (0, default);
            }
            else if (text.Length == 0)
            {
                return (1, default);
            }

            var hash = new HashKey(text);
            if (!stringHashes.TryGetValue(hash, out var recordId))
            {
                recordId = stringRecordId;
                stringHashes[hash] = stringRecordId;

                WriteStringRecord(text);

                stringRecordId += 1;
            }

            return (recordId, hash);
        }

        private void WriteStringRecord(string text)
        {
            try
            {
                // Switch the binaryWriter used by the Write* methods to the direct underlying stream writer.
                // We want this record to precede the record we're currently writing to currentRecordWriter
                // which is backed by a MemoryStream buffer
                binaryWriter = this.originalBinaryWriter;

                Write(BinaryLogRecordKind.String);
                Write(text);
            }
            finally
            {
                // switch back to continue writing the current record to the memory stream
                binaryWriter = this.currentRecordWriter;
            }
        }

        private void Write(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                binaryWriter.Write((byte)0);
                return;
            }

            binaryWriter.Write(text);
        }

        private void WriteOptionalString(string text)
        {
            if (text == null)
            {
                Write(false);
            }
            else
            {
                Write(true);
                Write(text);
            }
        }

        private void Write(DateTime timestamp)
        {
            binaryWriter.Write(timestamp.Ticks);
            Write((int)timestamp.Kind);
        }

        private void Write(TimeSpan timeSpan)
        {
            binaryWriter.Write(timeSpan.Ticks);
        }

        private void Write(EvaluationLocation item)
        {
            WriteOptionalString(item.ElementName);
            WriteOptionalString(item.ElementDescription);
            WriteOptionalString(item.EvaluationPassDescription);
            WriteOptionalString(item.File);
            Write((int)item.Kind);
            Write((int)item.EvaluationPass);

            Write(item.Line.HasValue);
            if (item.Line.HasValue)
            {
                Write(item.Line.Value);
            }

            Write(item.Id);
            Write(item.ParentId.HasValue);
            if (item.ParentId.HasValue)
            {
                Write(item.ParentId.Value);
            }
        }

        private void Write(ProfiledLocation e)
        {
            Write(e.NumberOfHits);
            Write(e.ExclusiveTime);
            Write(e.InclusiveTime);
        }
    }
}

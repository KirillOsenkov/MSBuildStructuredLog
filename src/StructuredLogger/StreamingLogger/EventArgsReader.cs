using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Logging.Serialization
{
    public class EventArgsReader
    {
        private readonly BinaryReader binaryReader;

        private static FieldInfo buildEventArgsFieldMessage =
            typeof(BuildEventArgs).GetField("message", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo buildEventArgsFieldContext =
            typeof(BuildEventArgs).GetField("buildEventContext", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo buildEventArgsFieldThreadId =
            typeof(BuildEventArgs).GetField("threadId", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo buildEventArgsFieldHelpKeyword =
            typeof(BuildEventArgs).GetField("helpKeyword", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo buildEventArgsFieldSenderName =
            typeof(BuildEventArgs).GetField("senderName", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo buildEventArgsFieldTimestamp =
            typeof(BuildEventArgs).GetField("timestamp", BindingFlags.Instance | BindingFlags.NonPublic);

        private static FieldInfo lazyFormattedLockerField =
            typeof(LazyFormattedBuildEventArgs).GetField("locker", BindingFlags.Instance | BindingFlags.NonPublic);

        private static FieldInfo messageEventArgsFieldSubcategory =
            typeof(LazyFormattedBuildEventArgs).GetField("subcategory", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo messageEventArgsFieldCode =
            typeof(LazyFormattedBuildEventArgs).GetField("code", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo messageEventArgsFieldFile =
            typeof(LazyFormattedBuildEventArgs).GetField("file", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo messageEventArgsFieldProjectFile =
            typeof(LazyFormattedBuildEventArgs).GetField("projectFile", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo messageEventArgsFieldLineNumber =
            typeof(LazyFormattedBuildEventArgs).GetField("lineNumber", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo messageEventArgsFieldColumnNumber =
            typeof(LazyFormattedBuildEventArgs).GetField("columnNumber", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo messageEventArgsFieldEndLineNumber =
            typeof(LazyFormattedBuildEventArgs).GetField("endLineNumber", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo messageEventArgsFieldEndColumnNumber =
            typeof(LazyFormattedBuildEventArgs).GetField("endColumnNumber", BindingFlags.Instance | BindingFlags.NonPublic);

        private static FieldInfo buildStartedFieldEnvironmentOnBuildStart =
            typeof(BuildStartedEventArgs).GetField("environmentOnBuildStart", BindingFlags.Instance | BindingFlags.NonPublic);

        private static FieldInfo buildFinishedFieldSucceeded =
            typeof(BuildFinishedEventArgs).GetField("succeeded", BindingFlags.Instance | BindingFlags.NonPublic);

        private static FieldInfo projectStartedFieldParentProjectBuildEventContext =
            typeof(ProjectStartedEventArgs).GetField("parentProjectBuildEventContext", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo projectStartedFieldProjectFile =
            typeof(ProjectStartedEventArgs).GetField("projectFile", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo projectStartedFieldProjectId =
            typeof(ProjectStartedEventArgs).GetField("projectId", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo projectStartedFieldTargetNames =
            typeof(ProjectStartedEventArgs).GetField("targetNames", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo projectStartedFieldToolsVersion =
            typeof(ProjectStartedEventArgs).GetField("toolsVersion", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo projectStartedFieldProperties =
            typeof(ProjectStartedEventArgs).GetField("properties", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo projectStartedFieldItems =
            typeof(ProjectStartedEventArgs).GetField("items", BindingFlags.Instance | BindingFlags.NonPublic);

        private static FieldInfo projectFinishedFieldProjectFile =
            typeof(ProjectFinishedEventArgs).GetField("projectFile", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo projectFinishedFieldSucceeded =
            typeof(ProjectFinishedEventArgs).GetField("succeeded", BindingFlags.Instance | BindingFlags.NonPublic);

        private static FieldInfo targetStartedFieldTargetName =
            typeof(TargetStartedEventArgs).GetField("targetName", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo targetStartedFieldProjectFile =
            typeof(TargetStartedEventArgs).GetField("projectFile", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo targetStartedFieldTargetFile =
            typeof(TargetStartedEventArgs).GetField("targetFile", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo targetStartedFieldParentTarget =
            typeof(TargetStartedEventArgs).GetField("parentTarget", BindingFlags.Instance | BindingFlags.NonPublic);

        private static FieldInfo targetFinishedFieldSucceeded =
            typeof(TargetFinishedEventArgs).GetField("succeeded", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo targetFinishedFieldProjectFile =
            typeof(TargetFinishedEventArgs).GetField("projectFile", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo targetFinishedFieldTargetFile =
            typeof(TargetFinishedEventArgs).GetField("targetFile", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo targetFinishedFieldTargetName =
            typeof(TargetFinishedEventArgs).GetField("targetName", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo targetFinishedFieldTargetOutputs =
            typeof(TargetFinishedEventArgs).GetField("targetOutputs", BindingFlags.Instance | BindingFlags.NonPublic);

        private static FieldInfo taskStartedFieldTaskName =
            typeof(TaskStartedEventArgs).GetField("taskName", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo taskStartedFieldProjectFile =
            typeof(TaskStartedEventArgs).GetField("projectFile", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo taskStartedFieldTaskFile =
            typeof(TaskStartedEventArgs).GetField("taskFile", BindingFlags.Instance | BindingFlags.NonPublic);

        private static FieldInfo taskFinishedFieldTaskName =
            typeof(TaskFinishedEventArgs).GetField("taskName", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo taskFinishedFieldProjectFile =
            typeof(TaskFinishedEventArgs).GetField("projectFile", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo taskFinishedFieldTaskFile =
            typeof(TaskFinishedEventArgs).GetField("taskFile", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo taskFinishedFieldSucceeded =
            typeof(TaskFinishedEventArgs).GetField("succeeded", BindingFlags.Instance | BindingFlags.NonPublic);

        public EventArgsReader(BinaryReader binaryReader)
        {
            this.binaryReader = binaryReader;
        }

        public BuildEventArgs Read()
        {
            LogRecordKind recordKind = (LogRecordKind)ReadInt32();
            BuildEventArgs result = null;
            switch (recordKind)
            {
                case LogRecordKind.EndOfFile:
                    break;
                case LogRecordKind.BuildStarted:
                    result = ReadBuildStartedEventArgs();
                    break;
                case LogRecordKind.BuildFinished:
                    result = ReadBuildFinishedEventArgs();
                    break;
                case LogRecordKind.ProjectStarted:
                    result = ReadProjectStartedEventArgs();
                    break;
                case LogRecordKind.ProjectFinished:
                    result = ReadProjectFinishedEventArgs();
                    break;
                case LogRecordKind.TargetStarted:
                    result = ReadTargetStartedEventArgs();
                    break;
                case LogRecordKind.TargetFinished:
                    result = ReadTargetFinishedEventArgs();
                    break;
                case LogRecordKind.TaskStarted:
                    result = ReadTaskStartedEventArgs();
                    break;
                case LogRecordKind.TaskFinished:
                    result = ReadTaskFinishedEventArgs();
                    break;
                case LogRecordKind.Error:
                    result = ReadBuildErrorEventArgs();
                    break;
                case LogRecordKind.Warning:
                    result = ReadBuildWarningEventArgs();
                    break;
                case LogRecordKind.Message:
                    result = ReadBuildMessageEventArgs();
                    break;
                case LogRecordKind.CriticalBuildMessage:
                    result = ReadCriticalBuildMessageEventArgs();
                    break;
                case LogRecordKind.TaskCommandLine:
                    result = ReadTaskCommandLineEventArgs();
                    break;
                case LogRecordKind.CustomEvent:
                    result = ReadCustomBuildEventArgs();
                    break;
                default:
                    break;
            }

            return result;
        }

        private BuildEventArgs ReadBuildStartedEventArgs()
        {
            var e = CreateInstance<BuildStartedEventArgs>();
            ReadBuildEventArgsFields(e);
            buildStartedFieldEnvironmentOnBuildStart.SetValue(e, ReadStringDictionary());
            return e;
        }

        private BuildEventArgs ReadBuildFinishedEventArgs()
        {
            var e = CreateInstance<BuildFinishedEventArgs>();
            ReadBuildEventArgsFields(e);
            buildFinishedFieldSucceeded.SetValue(e, ReadBoolean());
            return e;
        }

        private BuildEventArgs ReadProjectStartedEventArgs()
        {
            var e = CreateInstance<ProjectStartedEventArgs>();
            ReadBuildEventArgsFields(e);
            if (ReadBoolean())
            {
                projectStartedFieldParentProjectBuildEventContext.SetValue(e, ReadBuildEventContext());
            }

            ReadOptionalString(e, projectStartedFieldProjectFile);
            projectStartedFieldProjectId.SetValue(e, ReadInt32());
            projectStartedFieldTargetNames.SetValue(e, ReadString());
            ReadOptionalString(e, projectStartedFieldToolsVersion);

            var properties = ReadStringDictionary();
            if (properties != null)
            {
                var list = new ArrayList();
                foreach (var property in properties)
                {
                    var entry = new DictionaryEntry(property.Key, property.Value);
                    list.Add(entry);
                }

                projectStartedFieldProperties.SetValue(e, list);
            }

            projectStartedFieldItems.SetValue(e, ReadItems());

            return e;
        }

        private BuildEventArgs ReadProjectFinishedEventArgs()
        {
            var e = CreateInstance<ProjectFinishedEventArgs>();
            ReadBuildEventArgsFields(e);
            ReadOptionalString(e, projectFinishedFieldProjectFile);
            projectFinishedFieldSucceeded.SetValue(e, ReadBoolean());
            return e;
        }

        private BuildEventArgs ReadTargetStartedEventArgs()
        {
            var e = CreateInstance<TargetStartedEventArgs>();
            ReadBuildEventArgsFields(e);
            ReadOptionalString(e, targetStartedFieldTargetName);
            ReadOptionalString(e, targetStartedFieldProjectFile);
            ReadOptionalString(e, targetStartedFieldTargetFile);
            ReadOptionalString(e, targetStartedFieldParentTarget);
            return e;
        }

        private BuildEventArgs ReadTargetFinishedEventArgs()
        {
            var e = CreateInstance<TargetFinishedEventArgs>();
            ReadBuildEventArgsFields(e);
            targetFinishedFieldSucceeded.SetValue(e, ReadBoolean());
            ReadOptionalString(e, targetFinishedFieldProjectFile);
            ReadOptionalString(e, targetFinishedFieldTargetFile);
            ReadOptionalString(e, targetFinishedFieldTargetName);
            targetFinishedFieldTargetOutputs.SetValue(e, ReadItemList());
            return e;
        }

        private BuildEventArgs ReadTaskStartedEventArgs()
        {
            var e = CreateInstance<TaskStartedEventArgs>();
            ReadBuildEventArgsFields(e);
            ReadOptionalString(e, taskStartedFieldTaskName);
            ReadOptionalString(e, taskStartedFieldProjectFile);
            ReadOptionalString(e, taskStartedFieldTaskFile);
            return e;
        }

        private BuildEventArgs ReadTaskFinishedEventArgs()
        {
            var e = CreateInstance<TaskFinishedEventArgs>();
            ReadBuildEventArgsFields(e);
            taskFinishedFieldSucceeded.SetValue(e, ReadBoolean());
            ReadOptionalString(e, taskFinishedFieldTaskName);
            ReadOptionalString(e, taskFinishedFieldProjectFile);
            ReadOptionalString(e, taskFinishedFieldTaskFile);
            return e;
        }

        private BuildEventArgs ReadBuildErrorEventArgs()
        {
            var fields = ReadBuildEventArgsFields();

            ReadDiagnosticFields(fields);

            var result = new BuildErrorEventArgs(
                fields.Subcategory,
                fields.Code,
                fields.File,
                fields.LineNumber,
                fields.ColumnNumber,
                fields.EndLineNumber,
                fields.EndColumnNumber,
                fields.Message,
                fields.HelpKeyword,
                fields.SenderName,
                fields.Timestamp);
            result.BuildEventContext = fields.BuildEventContext;
            return result;
        }

        private BuildEventArgs ReadBuildWarningEventArgs()
        {
            var fields = ReadBuildEventArgsFields();

            ReadDiagnosticFields(fields);

            var result = new BuildWarningEventArgs(
                fields.Subcategory,
                fields.Code,
                fields.File,
                fields.LineNumber,
                fields.ColumnNumber,
                fields.EndLineNumber,
                fields.EndColumnNumber,
                fields.Message,
                fields.HelpKeyword,
                fields.SenderName,
                fields.Timestamp);
            result.BuildEventContext = fields.BuildEventContext;
            return result;
        }

        /// <summary>
        /// For errors and warnings these 8 fields are written out explicitly
        /// (their presence is not marked as a bit in the flags). So we have to
        /// read explicitly.
        /// </summary>
        /// <param name="fields"></param>
        private void ReadDiagnosticFields(BuildEventArgsFields fields)
        {
            fields.Subcategory = ReadOptionalString();
            fields.Code = ReadOptionalString();
            fields.File = ReadOptionalString();
            fields.ProjectFile = ReadOptionalString();
            fields.LineNumber = ReadInt32();
            fields.ColumnNumber = ReadInt32();
            fields.EndLineNumber = ReadInt32();
            fields.EndColumnNumber = ReadInt32();
        }

        private BuildEventArgs ReadBuildMessageEventArgs()
        {
            var importance = (MessageImportance)ReadInt32();
            var fields = ReadBuildEventArgsFields();
            var result = new BuildMessageEventArgs(
                fields.Subcategory,
                fields.Code,
                fields.File,
                fields.LineNumber,
                fields.ColumnNumber,
                fields.EndLineNumber,
                fields.EndColumnNumber,
                fields.Message,
                fields.HelpKeyword,
                fields.SenderName,
                importance,
                fields.Timestamp);
            result.BuildEventContext = fields.BuildEventContext;
            return result;
        }

        private BuildEventArgs ReadTaskCommandLineEventArgs()
        {
            var importance = (MessageImportance)ReadInt32();
            var fields = ReadBuildEventArgsFields();
            var commandLine = ReadOptionalString();
            var taskName = ReadOptionalString();
            var result = new TaskCommandLineEventArgs(
                commandLine,
                taskName,
                importance,
                fields.Timestamp);
            result.BuildEventContext = fields.BuildEventContext;
            return result;
        }

        private BuildEventArgs ReadCriticalBuildMessageEventArgs()
        {
            var importance = (MessageImportance)ReadInt32();
            var fields = ReadBuildEventArgsFields();
            var result = new CriticalBuildMessageEventArgs(
                fields.Subcategory,
                fields.Code,
                fields.File,
                fields.LineNumber,
                fields.ColumnNumber,
                fields.EndLineNumber,
                fields.EndColumnNumber,
                fields.Message,
                fields.HelpKeyword,
                fields.SenderName,
                fields.Timestamp);
            result.BuildEventContext = fields.BuildEventContext;
            return result;
        }

        private BuildEventArgs ReadCustomBuildEventArgs()
        {
            var e = CreateInstance<CustomBuildEventArgs>();
            ReadBuildEventArgsFields(e);
            return e;
        }

        private T CreateInstance<T>() where T : BuildEventArgs
        {
            T result = (T)FormatterServices.GetUninitializedObject(typeof(T));
            lazyFormattedLockerField.SetValue(result, result);
            return result;
        }

        private void ReadBuildEventArgsFields(BuildEventArgs e)
        {
            BuildEventArgsFieldFlags flags = (BuildEventArgsFieldFlags)ReadInt32();
            if ((flags & BuildEventArgsFieldFlags.Message) != 0)
            {
                buildEventArgsFieldMessage.SetValue(e, ReadString());
            }

            if ((flags & BuildEventArgsFieldFlags.BuildEventContext) != 0)
            {
                buildEventArgsFieldContext.SetValue(e, ReadBuildEventContext());
            }

            if ((flags & BuildEventArgsFieldFlags.ThreadId) != 0)
            {
                buildEventArgsFieldThreadId.SetValue(e, ReadInt32());
            }

            if ((flags & BuildEventArgsFieldFlags.HelpHeyword) != 0)
            {
                buildEventArgsFieldHelpKeyword.SetValue(e, ReadString());
            }

            if ((flags & BuildEventArgsFieldFlags.SenderName) != 0)
            {
                buildEventArgsFieldSenderName.SetValue(e, ReadString());
            }

            if ((flags & BuildEventArgsFieldFlags.Timestamp) != 0)
            {
                buildEventArgsFieldTimestamp.SetValue(e, ReadDateTime());
            }

            if ((flags & BuildEventArgsFieldFlags.Subcategory) != 0)
            {
                messageEventArgsFieldSubcategory.SetValue(e, ReadString());
            }

            if ((flags & BuildEventArgsFieldFlags.Code) != 0)
            {
                messageEventArgsFieldCode.SetValue(e, ReadString());
            }

            if ((flags & BuildEventArgsFieldFlags.File) != 0)
            {
                messageEventArgsFieldFile.SetValue(e, ReadString());
            }

            if ((flags & BuildEventArgsFieldFlags.ProjectFile) != 0)
            {
                messageEventArgsFieldProjectFile.SetValue(e, ReadString());
            }

            if ((flags & BuildEventArgsFieldFlags.LineNumber) != 0)
            {
                messageEventArgsFieldLineNumber.SetValue(e, ReadInt32());
            }

            if ((flags & BuildEventArgsFieldFlags.ColumnNumber) != 0)
            {
                messageEventArgsFieldColumnNumber.SetValue(e, ReadInt32());
            }

            if ((flags & BuildEventArgsFieldFlags.EndLineNumber) != 0)
            {
                messageEventArgsFieldEndLineNumber.SetValue(e, ReadInt32());
            }

            if ((flags & BuildEventArgsFieldFlags.EndColumnNumber) != 0)
            {
                messageEventArgsFieldEndColumnNumber.SetValue(e, ReadInt32());
            }
        }

        private BuildEventArgsFields ReadBuildEventArgsFields()
        {
            BuildEventArgsFieldFlags flags = (BuildEventArgsFieldFlags)ReadInt32();
            var result = new BuildEventArgsFields();

            if ((flags & BuildEventArgsFieldFlags.Message) != 0)
            {
                result.Message = ReadString();
            }

            if ((flags & BuildEventArgsFieldFlags.BuildEventContext) != 0)
            {
                result.BuildEventContext = ReadBuildEventContext();
            }

            if ((flags & BuildEventArgsFieldFlags.ThreadId) != 0)
            {
                result.ThreadId = ReadInt32();
            }

            if ((flags & BuildEventArgsFieldFlags.HelpHeyword) != 0)
            {
                result.HelpKeyword = ReadString();
            }

            if ((flags & BuildEventArgsFieldFlags.SenderName) != 0)
            {
                result.SenderName = ReadString();
            }

            if ((flags & BuildEventArgsFieldFlags.Timestamp) != 0)
            {
                result.Timestamp = ReadDateTime();
            }

            if ((flags & BuildEventArgsFieldFlags.Subcategory) != 0)
            {
                result.Subcategory = ReadString();
            }

            if ((flags & BuildEventArgsFieldFlags.Code) != 0)
            {
                result.Code = ReadString();
            }

            if ((flags & BuildEventArgsFieldFlags.File) != 0)
            {
                result.File = ReadString();
            }

            if ((flags & BuildEventArgsFieldFlags.ProjectFile) != 0)
            {
                result.ProjectFile = ReadString();
            }

            if ((flags & BuildEventArgsFieldFlags.LineNumber) != 0)
            {
                result.LineNumber = ReadInt32();
            }

            if ((flags & BuildEventArgsFieldFlags.ColumnNumber) != 0)
            {
                result.ColumnNumber = ReadInt32();
            }

            if ((flags & BuildEventArgsFieldFlags.EndLineNumber) != 0)
            {
                result.EndLineNumber = ReadInt32();
            }

            if ((flags & BuildEventArgsFieldFlags.EndColumnNumber) != 0)
            {
                result.EndColumnNumber = ReadInt32();
            }

            return result;
        }

        private BuildEventContext ReadBuildEventContext()
        {
            int nodeId = ReadInt32();
            int projectContextId = ReadInt32();
            int targetId = ReadInt32();
            int taskId = ReadInt32();
            int submissionId = ReadInt32();
            int projectInstanceId = ReadInt32();

            var result = new BuildEventContext(
                submissionId,
                nodeId,
                projectInstanceId,
                projectContextId,
                targetId,
                taskId);
            return result;
        }

        private Dictionary<string, string> ReadStringDictionary()
        {
            int count = ReadInt32();

            if (count == 0)
            {
                return null;
            }

            Dictionary<string, string> result = new Dictionary<string, string>();
            for (int i = 0; i < count; i++)
            {
                string key = ReadString();
                string value = ReadString();
                result[key] = value;
            }

            return result;
        }

        private class TaskItem : ITaskItem
        {
            public string ItemSpec { get; set; }
            public Dictionary<string, string> Metadata { get; } = new Dictionary<string, string>();

            public int MetadataCount => Metadata.Count;

            public ICollection MetadataNames => Metadata.Keys;

            public IDictionary CloneCustomMetadata()
            {
                return Metadata;
            }

            public void CopyMetadataTo(ITaskItem destinationItem)
            {
                throw new NotImplementedException();
            }

            public string GetMetadata(string metadataName)
            {
                return Metadata[metadataName];
            }

            public void RemoveMetadata(string metadataName)
            {
                throw new NotImplementedException();
            }

            public void SetMetadata(string metadataName, string metadataValue)
            {
                throw new NotImplementedException();
            }
        }

        private ITaskItem ReadItem()
        {
            var item = new TaskItem();
            item.ItemSpec = ReadString();

            int count = ReadInt32();
            for (int i = 0; i < count; i++)
            {
                string name = ReadString();
                string value = ReadString();
                item.Metadata[name] = value;
            }

            return item;
        }

        private IEnumerable ReadItems()
        {
            int count = ReadInt32();
            if (count == 0)
            {
                return null;
            }

            var list = new List<DictionaryEntry>(count);

            for (int i = 0; i < count; i++)
            {
                string key = ReadString();
                ITaskItem item = ReadItem();
                list.Add(new DictionaryEntry(key, item));
            }

            return list;
        }

        private IEnumerable ReadItemList()
        {
            int count = ReadInt32();
            if (count == 0)
            {
                return null;
            }

            var list = new List<ITaskItem>(count);

            for (int i = 0; i < count; i++)
            {
                ITaskItem item = ReadItem();
                list.Add(item);
            }

            return list;
        }

        private string ReadOptionalString()
        {
            if (ReadBoolean())
            {
                return ReadString();
            }
            else
            {
                return null;
            }
        }

        private void ReadOptionalString(object target, FieldInfo field)
        {
            if (ReadBoolean())
            {
                field.SetValue(target, ReadString());
            }
        }

        private string ReadString()
        {
            return binaryReader.ReadString();
        }

        private int ReadInt32()
        {
            return binaryReader.ReadInt32();
        }

        private bool ReadBoolean()
        {
            return binaryReader.ReadBoolean();
        }

        private DateTime ReadDateTime()
        {
            return new DateTime(binaryReader.ReadInt64(), (DateTimeKind)ReadInt32());
        }
    }
}

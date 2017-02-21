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
            typeof(BuildStartedEventArgs).GetField("succeeded", BindingFlags.Instance | BindingFlags.NonPublic);

        private static FieldInfo projectStartedFieldParentProjectBuildEventContext =
            typeof(BuildStartedEventArgs).GetField("parentProjectBuildEventContext", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo projectStartedFieldProjectFile =
            typeof(BuildStartedEventArgs).GetField("projectFile", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo projectStartedFieldProjectId =
            typeof(BuildStartedEventArgs).GetField("projectId", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo projectStartedFieldTargetNames =
            typeof(BuildStartedEventArgs).GetField("targetNames", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo projectStartedFieldToolsVersion =
            typeof(BuildStartedEventArgs).GetField("toolsVersion", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo projectStartedFieldProperties =
            typeof(BuildStartedEventArgs).GetField("properties", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo projectStartedFieldItems =
            typeof(BuildStartedEventArgs).GetField("items", BindingFlags.Instance | BindingFlags.NonPublic);

        private static FieldInfo projectFinishedFieldProjectFile =
            typeof(BuildStartedEventArgs).GetField("projectFile", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo projectFinishedFieldSucceeded =
            typeof(BuildStartedEventArgs).GetField("succeeded", BindingFlags.Instance | BindingFlags.NonPublic);

        private static FieldInfo targetStartedFieldTargetName =
            typeof(BuildStartedEventArgs).GetField("targetName", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo targetStartedFieldProjectFile =
            typeof(BuildStartedEventArgs).GetField("projectFile", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo targetStartedFieldTargetFile =
            typeof(BuildStartedEventArgs).GetField("targetFile", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo targetStartedFieldParentTarget =
            typeof(BuildStartedEventArgs).GetField("parentTarget", BindingFlags.Instance | BindingFlags.NonPublic);

        private static FieldInfo targetFinishedFieldSucceeded =
            typeof(BuildStartedEventArgs).GetField("succeeded", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo targetFinishedFieldProjectFile =
            typeof(BuildStartedEventArgs).GetField("projectFile", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo targetFinishedFieldTargetFile =
            typeof(BuildStartedEventArgs).GetField("targetFile", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo targetFinishedFieldTargetName =
            typeof(BuildStartedEventArgs).GetField("targetName", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo targetFinishedFieldTargetOutputs =
            typeof(BuildStartedEventArgs).GetField("targetOutputs", BindingFlags.Instance | BindingFlags.NonPublic);

        private static FieldInfo taskStartedFieldTaskName =
            typeof(BuildStartedEventArgs).GetField("taskName", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo taskStartedFieldProjectFile =
            typeof(BuildStartedEventArgs).GetField("projectFile", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo taskStartedFieldTaskFile =
            typeof(BuildStartedEventArgs).GetField("taskFile", BindingFlags.Instance | BindingFlags.NonPublic);

        private static FieldInfo taskFinishedFieldTaskName =
            typeof(BuildStartedEventArgs).GetField("taskName", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo taskFinishedFieldProjectFile =
            typeof(BuildStartedEventArgs).GetField("projectFile", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo taskFinishedFieldTaskFile =
            typeof(BuildStartedEventArgs).GetField("taskFile", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo taskFinishedFieldSucceeded =
            typeof(BuildStartedEventArgs).GetField("succeeded", BindingFlags.Instance | BindingFlags.NonPublic);

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

            projectStartedFieldItems.SetValue(e, ReadItems(e));

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
            ReadOptionalString(e, targetStartedFieldProjectFile);
            ReadOptionalString(e, targetStartedFieldTargetFile);
            ReadOptionalString(e, targetStartedFieldTargetName);
            targetFinishedFieldTargetOutputs.SetValue(e, ReadItems());
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
            throw new NotImplementedException();
        }

        private BuildEventArgs ReadBuildWarningEventArgs()
        {
            throw new NotImplementedException();
        }

        private BuildEventArgs ReadBuildMessageEventArgs()
        {
            throw new NotImplementedException();
        }

        private BuildEventArgs ReadCustomBuildEventArgs()
        {
            throw new NotImplementedException();
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

        private BuildEventContext ReadBuildEventContext()
        {
            int nodeId = ReadInt32();
            int projectContextId = ReadInt32();
            int targetId = ReadInt32();
            int taskId = ReadInt32();
            int submissionId = ReadInt32();
            int projectInstanceId = ReadInt32();

            var result = new BuildEventContext(submissionId, nodeId, projectInstanceId, projectContextId, targetId, taskId);
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
                throw new NotImplementedException();
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

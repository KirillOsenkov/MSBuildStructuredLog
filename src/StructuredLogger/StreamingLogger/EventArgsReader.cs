using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging.StructuredLogger;

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
            throw new NotImplementedException();
        }

        private BuildEventArgs ReadProjectFinishedEventArgs()
        {
            throw new NotImplementedException();
        }

        private BuildEventArgs ReadTargetStartedEventArgs()
        {
            throw new NotImplementedException();
        }

        private BuildEventArgs ReadTargetFinishedEventArgs()
        {
            throw new NotImplementedException();
        }

        private BuildEventArgs ReadTaskStartedEventArgs()
        {
            throw new NotImplementedException();
        }

        private BuildEventArgs ReadTaskFinishedEventArgs()
        {
            throw new NotImplementedException();
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

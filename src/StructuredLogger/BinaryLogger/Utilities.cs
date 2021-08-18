using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Build.Framework;

namespace Microsoft.Build.BackEnd
{
    internal class ItemGroupLoggingHelper
    {
        internal static FieldInfo LineNumberField = typeof(BuildMessageEventArgs).GetField("lineNumber", BindingFlags.Instance | BindingFlags.NonPublic);
        internal static FieldInfo ColumnNumberField = typeof(BuildMessageEventArgs).GetField("columnNumber", BindingFlags.Instance | BindingFlags.NonPublic);

        internal static TaskParameterEventArgs CreateTaskParameterEventArgs(
            BuildEventContext buildEventContext,
            TaskParameterMessageKind messageKind,
            string itemType,
            IList items,
            bool logItemMetadata,
            DateTime timestamp,
            int line,
            int column)
        {
            var args = new TaskParameterEventArgs(
                messageKind,
                itemType,
                items,
                logItemMetadata,
                timestamp);
            args.BuildEventContext = buildEventContext;

            // sigh this is terrible for perf
            LineNumberField.SetValue(args, line);
            ColumnNumberField.SetValue(args, column);

            // Should probably make these public
            // args.LineNumber = line;
            // args.ColumnNumber = column;
            return args;
        }
    }
}

namespace Microsoft.Build.Internal
{
    internal static class Utilities
    {
        public static void EnumerateProperties(IEnumerable properties, Action<KeyValuePair<string, string>> callback)
        {
            if (properties == null)
            {
                return;
            }

            //if (properties is PropertyDictionary<ProjectPropertyInstance> propertyDictionary)
            //{
            //    propertyDictionary.Enumerate((key, value) =>
            //    {
            //        callback(new KeyValuePair<string, string>(key, value));
            //    });
            //}
            //else
            {
                foreach (var item in properties)
                {
                    if (item is DictionaryEntry dictionaryEntry && dictionaryEntry.Key is string key && !string.IsNullOrEmpty(key))
                    {
                        callback(new KeyValuePair<string, string>(key, dictionaryEntry.Value as string ?? string.Empty));
                    }
                    else if (item is KeyValuePair<string, string> kvp)
                    {
                        callback(kvp);
                    }
                    else
                    {
                    }
                }
            }
        }

        public static void EnumerateItems(IEnumerable items, Action<DictionaryEntry> callback)
        {
            foreach (var item in items)
            {
                string itemType = default;
                object itemValue = null;

                if (item is DictionaryEntry dictionaryEntry)
                {
                    itemType = dictionaryEntry.Key as string;
                    itemValue = dictionaryEntry.Value;
                }

                if (string.IsNullOrEmpty(itemType))
                {
                    continue;
                }

                callback(new DictionaryEntry(itemType, itemValue));
            }
        }

        public static IEnumerable<KeyValuePair<string, string>> EnumerateMetadata(this ITaskItem taskItem)
        {
            // This runs if ITaskItem is Microsoft.Build.Utilities.TaskItem from Microsoft.Build.Utilities.v4.0.dll
            // that is loaded from the GAC.
            IDictionary customMetadata = taskItem.CloneCustomMetadata();
            if (customMetadata is IEnumerable<KeyValuePair<string, string>> enumerableMetadata)
            {
                return enumerableMetadata;
            }

            // In theory this should never be reachable.
            var list = new KeyValuePair<string, string>[customMetadata.Count];
            int i = 0;

            foreach (string metadataName in customMetadata.Keys)
            {
                string valueOrError;

                try
                {
                    valueOrError = taskItem.GetMetadata(metadataName);
                }
                // Temporarily try catch all to mitigate frequent NullReferenceExceptions in
                // the logging code until CopyOnWritePropertyDictionary is replaced with
                // ImmutableDictionary. Calling into Debug.Fail to crash the process in case
                // the exception occurres in Debug builds.
                catch (Exception e)
                {
                    valueOrError = e.Message;
                }

                list[i] = new KeyValuePair<string, string>(metadataName, valueOrError);
                i += 1;
            }

            return list;
        }

        public static bool EqualTo(this BuildEventContext buildEventContext, BuildEventContext other)
        {
            if (object.ReferenceEquals(buildEventContext, other))
            {
                return true;
            }

            if (buildEventContext == null || other == null)
            {
                return false;
            }

            return buildEventContext.TaskId == other.TaskId
                && buildEventContext.TargetId == other.TargetId
                && buildEventContext.ProjectContextId == other.ProjectContextId
                && buildEventContext.ProjectInstanceId == other.ProjectInstanceId
                && buildEventContext.NodeId == other.NodeId
                && buildEventContext.EvaluationId == other.EvaluationId
                && buildEventContext.SubmissionId == other.SubmissionId;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BuildEventContext ReadOptionalBuildEventContext(this BinaryReader reader)
        {
            if (reader.ReadByte() == 0)
            {
                return null;
            }

            return reader.ReadBuildEventContext();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BuildEventContext ReadBuildEventContext(this BinaryReader reader)
        {
            int nodeId = reader.ReadInt32();
            int projectContextId = reader.ReadInt32();
            int targetId = reader.ReadInt32();
            int taskId = reader.ReadInt32();
            int submissionId = reader.ReadInt32();
            int projectInstanceId = reader.ReadInt32();
            int evaluationId = reader.ReadInt32();

            var buildEventContext = new BuildEventContext(submissionId, nodeId, evaluationId, projectInstanceId, projectContextId, targetId, taskId);
            return buildEventContext;
        }
    }
}

namespace Microsoft.Build.Shared
{
    internal static class ItemTypeNames
    {
        /// <summary>
        /// References to other msbuild projects
        /// </summary>
        internal const string ProjectReference = nameof(ProjectReference);

        /// <summary>
        /// Statically specifies what targets a project calls on its references
        /// </summary>
        internal const string ProjectReferenceTargets = nameof(ProjectReferenceTargets);

        internal const string GraphIsolationExemptReference = nameof(GraphIsolationExemptReference);

        /// <summary>
        /// Declares a project cache plugin and its configuration.
        /// </summary>
        internal const string ProjectCachePlugin = nameof(ProjectCachePlugin);

        /// <summary>
        /// Embed specified files in the binary log
        /// </summary>
        internal const string EmbedInBinlog = nameof(EmbedInBinlog);
    }
}
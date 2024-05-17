using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using Microsoft.Build.Framework;

namespace Microsoft.Build.BackEnd
{
    internal class ItemGroupLoggingHelper
    {
        internal static TaskParameterEventArgs CreateTaskParameterEventArgs(
            BuildEventContext buildEventContext,
            TaskParameterMessageKind messageKind,
            string parameterName,
            string propertyName,
            string itemType,
            IList items,
            bool logItemMetadata,
            DateTime timestamp,
            int line,
            int column)
        {
            var args = new TaskParameterEventArgs2(
                messageKind,
                parameterName,
                propertyName,
                itemType,
                items,
                logItemMetadata,
                timestamp);
            args.BuildEventContext = buildEventContext;

            args.LineNumber = line;
            args.ColumnNumber = column;
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

    /// <summary>
    /// Constants that we want to be shareable across all our assemblies.
    /// </summary>
    internal static class MSBuildConstants
    {
        /// <summary>
        /// The name of the property that indicates the tools path
        /// </summary>
        internal const string ToolsPath = "MSBuildToolsPath";

        /// <summary>
        /// Name of the property that indicates the X64 tools path
        /// </summary>
        internal const string ToolsPath64 = "MSBuildToolsPath64";

        /// <summary>
        /// Name of the property that indicates the root of the SDKs folder
        /// </summary>
        internal const string SdksPath = "MSBuildSDKsPath";

        /// <summary>
        /// Name of the property that indicates that all warnings should be treated as errors.
        /// </summary>
        internal const string TreatWarningsAsErrors = "MSBuildTreatWarningsAsErrors";

        /// <summary>
        /// Name of the property that indicates a list of warnings to treat as errors.
        /// </summary>
        internal const string WarningsAsErrors = "MSBuildWarningsAsErrors";

        /// <summary>
        /// Name of the property that indicates the list of warnings to treat as messages.
        /// </summary>
        internal const string WarningsAsMessages = "MSBuildWarningsAsMessages";

        /// <summary>
        /// The name of the environment variable that users can specify to override where NuGet assemblies are loaded from in the NuGetSdkResolver.
        /// </summary>
        internal const string NuGetAssemblyPathEnvironmentVariableName = "MSBUILD_NUGET_PATH";

        /// <summary>
        /// The name of the target to run when a user specifies the /restore command-line argument.
        /// </summary>
        internal const string RestoreTargetName = "Restore";
        /// <summary>
        /// The most current Visual Studio Version known to this version of MSBuild.
        /// </summary>
        internal const string CurrentVisualStudioVersion = "16.0";

        /// <summary>
        /// The most current ToolsVersion known to this version of MSBuild.
        /// </summary>
        internal const string CurrentToolsVersion = "Current";

        internal const string MSBuildDummyGlobalPropertyHeader = "MSBuildProjectInstance";

        /// <summary>
        /// The most current VSGeneralAssemblyVersion known to this version of MSBuild.
        /// </summary>
        internal const string CurrentAssemblyVersion = "15.1.0.0";

        /// <summary>
        /// Current version of this MSBuild Engine assembly in the form, e.g, "12.0"
        /// </summary>
        internal const string CurrentProductVersion = "16.0";

        /// <summary>
        /// Symbol used in ProjectReferenceTarget items to represent default targets
        /// </summary>
        internal const string DefaultTargetsMarker = ".default";

        /// <summary>
        /// Symbol used in ProjectReferenceTarget items to represent targets specified on the ProjectReference item
        /// with fallback to default targets if the ProjectReference item has no targets specified.
        /// </summary>
        internal const string ProjectReferenceTargetsOrDefaultTargetsMarker = ".projectReferenceTargetsOrDefaultTargets";

        // One-time allocations to avoid implicit allocations for Split(), Trim().
        internal static readonly char[] SemicolonChar = { ';' };
        internal static readonly char[] SpaceChar = { ' ' };
        internal static readonly char[] SingleQuoteChar = { '\'' };
        internal static readonly char[] EqualsChar = { '=' };
        internal static readonly char[] ColonChar = { ':' };
        internal static readonly char[] BackslashChar = { '\\' };
        internal static readonly char[] NewlineChar = { '\n' };
        internal static readonly char[] CrLf = { '\r', '\n' };
        internal static readonly char[] ForwardSlash = { '/' };
        internal static readonly char[] ForwardSlashBackslash = { '/', '\\' };
        internal static readonly char[] WildcardChars = { '*', '?' };
        internal static readonly string[] CharactersForExpansion = { "*", "?", "$(", "@(", "%" };
        internal static readonly char[] CommaChar = { ',' };
        internal static readonly char[] HyphenChar = { '-' };
        internal static readonly char[] DirectorySeparatorChar = { Path.DirectorySeparatorChar };
        internal static readonly char[] DotChar = { '.' };
        internal static readonly string[] EnvironmentNewLine = { Environment.NewLine };
        internal static readonly char[] PipeChar = { '|' };
        internal static readonly char[] PathSeparatorChar = { Path.PathSeparator };
    }

    internal static class BinaryWriterExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteOptionalString(this BinaryWriter writer, string value)
        {
            if (value == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                writer.Write(value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteTimestamp(this BinaryWriter writer, DateTime timestamp)
        {
            writer.Write(timestamp.Ticks);
            writer.Write((Int32)timestamp.Kind);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Write7BitEncodedInt(this BinaryWriter writer, int value)
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteOptionalBuildEventContext(this BinaryWriter writer, BuildEventContext context)
        {
            if (context == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                writer.WriteBuildEventContext(context);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteBuildEventContext(this BinaryWriter writer, BuildEventContext context)
        {
            writer.Write(context.NodeId);
            writer.Write(context.ProjectContextId);
            writer.Write(context.TargetId);
            writer.Write(context.TaskId);
            writer.Write(context.SubmissionId);
            writer.Write(context.ProjectInstanceId);
            writer.Write(context.EvaluationId);
        }
    }

    internal static class BinaryReaderExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string ReadOptionalString(this BinaryReader reader)
        {
            return reader.ReadByte() == 0 ? null : reader.ReadString();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Read7BitEncodedInt(this BinaryReader reader)
        {
            // Read out an Int32 7 bits at a time.  The high bit
            // of the byte when on means to continue reading more bytes.
            int count = 0;
            int shift = 0;
            byte b;
            do
            {
                // Check for a corrupted stream.  Read a max of 5 bytes.
                // In a future version, add a DataFormatException.
                if (shift == 5 * 7)  // 5 bytes max per Int32, shift += 7
                {
                    throw new FormatException();
                }

                // ReadByte handles end of stream cases for us.
                b = reader.ReadByte();
                count |= (b & 0x7F) << shift;
                shift += 7;
            } while ((b & 0x80) != 0);
            return count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DateTime ReadTimestamp(this BinaryReader reader)
        {
            long timestampTicks = reader.ReadInt64();
            DateTimeKind kind = (DateTimeKind)reader.ReadInt32();
            var timestamp = new DateTime(timestampTicks, kind);
            return timestamp;
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

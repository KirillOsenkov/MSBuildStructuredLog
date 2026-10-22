using System;
using System.Collections.Generic;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Mirrors dotnet/msbuild's Microsoft.Build.Framework.AssemblyResolutionSearchResult (binlog format v28, dotnet/msbuild#14599).
    /// </summary>
    public enum AssemblyResolutionSearchResult
    {
        Unknown,
        FileNotFound,
        FusionNamesDidNotMatch,
        TargetHadNoFusionName,
        NotInGac,
        NotAFileNameOnDisk,
        ProcessorArchitectureDoesNotMatch,
    }

    [Flags]
    internal enum AssemblyResolutionSearchAttemptContext
    {
        None = 0,
        SearchPathUnchanged = 1 << 0,
        ParentAssemblyUnchanged = 1 << 1,
        AssemblyFoldersExUnchanged = 1 << 2,
    }

    /// <summary>
    /// Describes one candidate considered while resolving an assembly reference.
    /// Mirrors dotnet/msbuild's Microsoft.Build.Framework.AssemblyResolutionSearchAttempt.
    /// </summary>
    [Serializable]
    public sealed class AssemblyResolutionSearchAttempt
    {
        public AssemblyResolutionSearchAttempt(
            string fileNameAttempted,
            string searchPath,
            string parentAssembly,
            string assemblyName,
            AssemblyResolutionSearchResult result,
            string processorArchitecture,
            bool isAssemblyFoldersExSearch)
        {
            FileNameAttempted = fileNameAttempted;
            SearchPath = searchPath;
            ParentAssembly = parentAssembly;
            AssemblyName = assemblyName;
            Result = result;
            ProcessorArchitecture = processorArchitecture;
            IsAssemblyFoldersExSearch = isAssemblyFoldersExSearch;
        }

        public string FileNameAttempted { get; }
        public string SearchPath { get; }
        public string ParentAssembly { get; }
        public string AssemblyName { get; }
        public AssemblyResolutionSearchResult Result { get; }
        public string ProcessorArchitecture { get; }
        public bool IsAssemblyFoldersExSearch { get; }
    }

    /// <summary>
    /// Records all candidates considered while resolving one RAR reference (binlog format v28).
    /// Mirrors dotnet/msbuild's Microsoft.Build.Framework.AssemblyResolutionSearchTraceEventArgs,
    /// trimmed to the fields this reader needs - see dotnet/msbuild#14599. This fork does not
    /// implement the real type's lazy/localized Message formatting; Message is a simple eager summary.
    /// </summary>
    [Serializable]
    public class AssemblyResolutionSearchTraceEventArgs : BuildMessageEventArgs
    {
        public AssemblyResolutionSearchTraceEventArgs()
        {
        }

        public AssemblyResolutionSearchTraceEventArgs(
            string requestedAssemblyName,
            string targetProcessorArchitecture,
            IReadOnlyList<AssemblyResolutionSearchAttempt> searchAttempts,
            string senderName,
            MessageImportance importance,
            DateTime eventTimestamp)
            : base(
                $"Considered {searchAttempts.Count} candidate(s) while resolving \"{requestedAssemblyName}\".",
                null,
                senderName,
                importance,
                eventTimestamp)
        {
            RequestedAssemblyName = requestedAssemblyName;
            TargetProcessorArchitecture = targetProcessorArchitecture;
            SearchAttempts = searchAttempts;
        }

        public string RequestedAssemblyName { get; }

        public string TargetProcessorArchitecture { get; }

        public IReadOnlyList<AssemblyResolutionSearchAttempt> SearchAttempts { get; }
    }
}

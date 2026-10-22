using System;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Structured replacement for the legacy free-text MSB3277 "conflict" warning (binlog format v28).
    /// Mirrors dotnet/msbuild's Microsoft.Build.Framework.AssemblyConflictWarningEventArgs, trimmed to
    /// the fields this reader needs - see dotnet/msbuild#14601. This fork does not implement the real
    /// type's lazy/localized Message formatting; Message is a simple eager summary.
    /// </summary>
    [Serializable]
    public class AssemblyConflictWarningEventArgs(
        string simpleAssemblyName,
        AssemblyConflictLossReason lossReason,
        AssemblyConflictReferenceDetails victor,
        AssemblyConflictReferenceDetails victim,
        string code,
        string file,
        int lineNumber,
        int columnNumber,
        string helpKeyword,
        string senderName,
        DateTime eventTimestamp)
        : BuildWarningEventArgs(
            subcategory: null,
            code: code,
            file: file,
            lineNumber: lineNumber,
            columnNumber: columnNumber,
            endLineNumber: 0,
            endColumnNumber: 0,
            message: $"Found conflicts between different versions of \"{simpleAssemblyName}\" that could not be resolved.",
            helpKeyword: helpKeyword,
            senderName: senderName,
            eventTimestamp: eventTimestamp)
    {
        public string SimpleAssemblyName { get; } = simpleAssemblyName;

        public AssemblyConflictLossReason LossReason { get; } = lossReason;

        public AssemblyConflictReferenceDetails Victor { get; } = victor;

        public AssemblyConflictReferenceDetails Victim { get; } = victim;
    }
}

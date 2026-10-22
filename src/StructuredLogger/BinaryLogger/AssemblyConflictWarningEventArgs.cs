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
    public class AssemblyConflictWarningEventArgs : BuildWarningEventArgs
    {
        public AssemblyConflictWarningEventArgs()
        {
        }

        public AssemblyConflictWarningEventArgs(
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
            : base(
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
            SimpleAssemblyName = simpleAssemblyName;
            LossReason = lossReason;
            Victor = victor;
            Victim = victim;
        }

        public string SimpleAssemblyName { get; }

        public AssemblyConflictLossReason LossReason { get; }

        public AssemblyConflictReferenceDetails Victor { get; }

        public AssemblyConflictReferenceDetails Victim { get; }
    }
}

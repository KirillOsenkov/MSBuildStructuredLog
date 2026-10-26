using System;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Low-importance structured conflict detail (binlog format v28). Mirrors dotnet/msbuild's
    /// Microsoft.Build.Framework.AssemblyConflictDependencyDetailsMessageEventArgs, trimmed to the
    /// fields this reader needs - see dotnet/msbuild#14601. This fork does not implement the real
    /// type's localized Message formatting; Message is a simple summary, computed lazily on
    /// first access like <see cref="AssemblyLoadBuildEventArgs"/>'s does.
    /// </summary>
    [Serializable]
    public class AssemblyConflictDependencyDetailsMessageEventArgs(
        AssemblyConflictReferenceDetails victor,
        AssemblyConflictReferenceDetails victim,
        string senderName,
        MessageImportance importance,
        DateTime eventTimestamp)
        : BuildMessageEventArgs(null, null, senderName, importance, eventTimestamp)
    {
        public AssemblyConflictReferenceDetails Victor { get; } = victor;

        public AssemblyConflictReferenceDetails Victim { get; } = victim;

        public override string Message
        {
            get
            {
                RawMessage ??= $"""Conflict dependency details for "{Victor?.FusionName}" vs "{Victim?.FusionName}".""";
                return RawMessage;
            }
        }
    }
}

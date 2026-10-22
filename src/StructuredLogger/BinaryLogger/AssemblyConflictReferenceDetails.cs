using System;
using System.Collections.Generic;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Mirrors dotnet/msbuild's Microsoft.Build.Framework.AssemblyConflictLossReason (binlog format v28, dotnet/msbuild#14601).
    /// </summary>
    public enum AssemblyConflictLossReason
    {
        DidNotLose = 0,
        HadLowerVersion = 1,
        InsolubleConflict = 2,
        WasNotPrimary = 3,
        FusionEquivalentWithSameVersion = 4,
    }

    /// <summary>
    /// A reference that required the conflicting assembly, and the items that caused it to resolve.
    /// Mirrors dotnet/msbuild's Microsoft.Build.Framework.AssemblyConflictDependee.
    /// </summary>
    [Serializable]
    public sealed class AssemblyConflictDependee(string dependeeFullPath, IReadOnlyList<string> sourceItemSpecs)
    {
        public string DependeeFullPath { get; } = dependeeFullPath;

        public IReadOnlyList<string> SourceItemSpecs { get; } = sourceItemSpecs;
    }

    /// <summary>
    /// Describes the victor or victim of an assembly conflict, including the dependency chain that
    /// caused it to resolve. Mirrors dotnet/msbuild's Microsoft.Build.Framework.AssemblyConflictReferenceDetails.
    /// </summary>
    [Serializable]
    public sealed class AssemblyConflictReferenceDetails(
        string fusionName,
        string fullPath,
        bool isPrimary,
        bool isResolved,
        string unresolvedPrimaryItemSpec,
        IReadOnlyList<string> primarySourceItemSpecs,
        IReadOnlyList<AssemblyConflictDependee> dependees)
    {
        public string FusionName { get; } = fusionName;

        public string FullPath { get; } = fullPath;

        public bool IsPrimary { get; } = isPrimary;

        public bool IsResolved { get; } = isResolved;

        public string UnresolvedPrimaryItemSpec { get; } = unresolvedPrimaryItemSpec;

        public IReadOnlyList<string> PrimarySourceItemSpecs { get; } = primarySourceItemSpecs;

        public IReadOnlyList<AssemblyConflictDependee> Dependees { get; } = dependees;
    }
}

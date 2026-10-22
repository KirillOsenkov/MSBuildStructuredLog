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
        public string DependeeFullPath { get; private set; } = dependeeFullPath;

        public IReadOnlyList<string> SourceItemSpecs { get; private set; } = sourceItemSpecs;
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
        public string FusionName { get; private set; } = fusionName;

        public string FullPath { get; private set; } = fullPath;

        public bool IsPrimary { get; private set; } = isPrimary;

        public bool IsResolved { get; private set; } = isResolved;

        public string UnresolvedPrimaryItemSpec { get; private set; } = unresolvedPrimaryItemSpec;

        public IReadOnlyList<string> PrimarySourceItemSpecs { get; private set; } = primarySourceItemSpecs;

        public IReadOnlyList<AssemblyConflictDependee> Dependees { get; private set; } = dependees;
    }
}

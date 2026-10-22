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
    public sealed class AssemblyConflictDependee
    {
        public AssemblyConflictDependee(string dependeeFullPath, IReadOnlyList<string> sourceItemSpecs)
        {
            DependeeFullPath = dependeeFullPath;
            SourceItemSpecs = sourceItemSpecs;
        }

        public string DependeeFullPath { get; }
        public IReadOnlyList<string> SourceItemSpecs { get; }
    }

    /// <summary>
    /// Describes the victor or victim of an assembly conflict, including the dependency chain that
    /// caused it to resolve. Mirrors dotnet/msbuild's Microsoft.Build.Framework.AssemblyConflictReferenceDetails.
    /// </summary>
    [Serializable]
    public sealed class AssemblyConflictReferenceDetails
    {
        public AssemblyConflictReferenceDetails(
            string fusionName,
            string fullPath,
            bool isPrimary,
            bool isResolved,
            string unresolvedPrimaryItemSpec,
            IReadOnlyList<string> primarySourceItemSpecs,
            IReadOnlyList<AssemblyConflictDependee> dependees)
        {
            FusionName = fusionName;
            FullPath = fullPath;
            IsPrimary = isPrimary;
            IsResolved = isResolved;
            UnresolvedPrimaryItemSpec = unresolvedPrimaryItemSpec;
            PrimarySourceItemSpecs = primarySourceItemSpecs;
            Dependees = dependees;
        }

        public string FusionName { get; }
        public string FullPath { get; }
        public bool IsPrimary { get; }
        public bool IsResolved { get; }
        public string UnresolvedPrimaryItemSpec { get; }
        public IReadOnlyList<string> PrimarySourceItemSpecs { get; }
        public IReadOnlyList<AssemblyConflictDependee> Dependees { get; }
    }
}

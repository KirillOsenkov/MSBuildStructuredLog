using System;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// A reason why a target was skipped.
    /// </summary>
    public enum TargetSkipReason
    {
        /// <summary>
        /// The target was not skipped or the skip reason was unknown.
        /// </summary>
        None,

        /// <summary>
        /// The target previously built successfully.
        /// </summary>
        PreviouslyBuiltSuccessfully,

        /// <summary>
        /// The target previously built unsuccessfully.
        /// </summary>
        PreviouslyBuiltUnsuccessfully,

        /// <summary>
        /// All the target outputs were up-to-date with respect to their inputs.
        /// </summary>
        OutputsUpToDate,

        /// <summary>
        /// The condition on the target was evaluated as false.
        /// </summary>
        ConditionWasFalse,

        /// <summary>
        /// The target was skipped because it didn't exist and BuildRequestDataFlags.SkipNonexistentTargets was set to true.
        /// </summary>
        TargetDoesNotExist,
    }

    public class TargetSkippedEventArgs2 : TargetSkippedEventArgs
    {
        public TargetSkippedEventArgs2(string message)
            : base(message)
        {
        }

        public TargetSkipReason SkipReason { get; set; }
        public BuildEventContext OriginalBuildEventContext { get; set; }
    }
}
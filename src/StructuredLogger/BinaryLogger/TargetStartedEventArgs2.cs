using System;

namespace Microsoft.Build.Framework
{
    public class TargetStartedEventArgs2 : TargetStartedEventArgs
    {
        public TargetStartedEventArgs2(
            string message,
            string helpKeyword,
            string targetName,
            string projectFile,
            string targetFile,
            string parentTarget,
            TargetBuiltReason targetBuiltReason,
            DateTime eventTimestamp) : base(
                message,
                helpKeyword,
                targetName,
                projectFile,
                targetFile,
                parentTarget,
                eventTimestamp)
        {
            TargetBuiltReason = targetBuiltReason;
        }

        public TargetBuiltReason TargetBuiltReason { get; }
    }
}

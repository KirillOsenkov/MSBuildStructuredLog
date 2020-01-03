using System;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Logging.StructuredLogger
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
            BuildReason = targetBuiltReason;
        }

        public TargetBuiltReason BuildReason { get; }
    }
}

using System;

namespace Microsoft.Build.Framework
{
    public class TaskStartedEventArgs2 : TaskStartedEventArgs
    {
        public TaskStartedEventArgs2(
            string message,
            string helpKeyword,
            string projectFile,
            string taskFile,
            string taskName,
            DateTime eventTimestamp)
            : base(
                message,
                helpKeyword,
                projectFile,
                taskFile,
                taskName,
                eventTimestamp)
        {
        }

        public int LineNumber { get; set; }
        public int ColumnNumber { get; set; }
    }
}
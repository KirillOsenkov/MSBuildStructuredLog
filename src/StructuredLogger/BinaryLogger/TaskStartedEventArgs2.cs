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

        // the properties in the base class have an internal setter
        public new int LineNumber { get; set; }
        public new int ColumnNumber { get; set; }
        public string TaskAssemblyLocation { get; set; }
    }
}

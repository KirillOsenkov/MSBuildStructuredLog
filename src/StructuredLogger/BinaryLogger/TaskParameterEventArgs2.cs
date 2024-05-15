using System;
using System.Collections;

namespace Microsoft.Build.Framework
{
    public class TaskParameterEventArgs2 : TaskParameterEventArgs
    {
        public TaskParameterEventArgs2(
            TaskParameterMessageKind kind,
            string parameterName,
            string propertyName,
            string itemType,
            IList items,
            bool logItemMetadata,
            DateTime eventTimestamp)
            : base(kind, itemType, items, logItemMetadata, eventTimestamp)
        {
            ParameterName = parameterName;
            PropertyName = propertyName;
        }

        // Added in MSBuild 17.11
        public string ParameterName { get; set; }
        public string PropertyName { get; set; }

        // the properties in the base class have an internal setter
        public new int LineNumber { get; set; }
        public new int ColumnNumber { get; set; }
    }
}

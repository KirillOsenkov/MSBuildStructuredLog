using Microsoft.Build.Framework;

namespace StructuredLogger.BinaryLogger
{
    internal class ExtendedPropertyReassignmentEventArgs : BuildMessageEventArgs
    {
        /// <summary>
        /// Creates an instance of the <see cref="PropertyReassignmentEventArgs"/> class.
        /// </summary>
        /// <param name="propertyName">The name of the property whose value was reassigned.</param>
        /// <param name="previousValue">The previous value of the reassigned property.</param>
        /// <param name="newValue">The new value of the reassigned property.</param>
        /// <param name="file">The file associated with the event.</param>
        /// <param name="line">The line number (0 if not applicable).</param>
        /// <param name="column">The column number (0 if not applicable).</param>
        /// <param name="message">The message of the property.</param>
        /// <param name="helpKeyword">The help keyword.</param>
        /// <param name="senderName">The sender name of the event.</param>
        /// <param name="importance">The importance of the message.</param>
        public ExtendedPropertyReassignmentEventArgs(
            string propertyName,
            string previousValue,
            string newValue,
            string file,
            int line,
            int column,
            string message,
            string helpKeyword = null,
            string senderName = null,
            MessageImportance importance = MessageImportance.Low)
            : base(subcategory: null, code: null, file: file, lineNumber: line, columnNumber: column, 0, 0, message, helpKeyword, senderName, importance)
        {
            PropertyName = propertyName;
            PreviousValue = previousValue;
            NewValue = newValue;
        }

        /// <summary>
        /// The name of the property whose value was reassigned.
        /// </summary>
        public string PropertyName { get; set; }

        /// <summary>
        /// The previous value of the reassigned property.
        /// </summary>
        public string PreviousValue { get; set; }

        /// <summary>
        /// The new value of the reassigned property.
        /// </summary>
        public string NewValue { get; set; }
    }
}

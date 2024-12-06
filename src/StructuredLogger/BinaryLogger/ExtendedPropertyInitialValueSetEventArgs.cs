using Microsoft.Build.Framework;

namespace StructuredLogger.BinaryLogger
{
    internal class ExtendedPropertyInitialValueSetEventArgs : BuildMessageEventArgs
    {
        /// <summary>
        /// Creates an instance of the <see cref="ExtendedPropertyInitialValueSetEventArgs"/> class.
        /// </summary>
        /// <param name="propertyName">The name of the property.</param>
        /// <param name="propertyValue">The value of the property.</param>
        /// <param name="propertySource">The source of the property.</param>
        /// <param name="file">The file associated with the event.</param>
        /// <param name="line">The line number (0 if not applicable).</param>
        /// <param name="column">The column number (0 if not applicable).</param>
        /// <param name="message">The message of the property.</param>
        /// <param name="helpKeyword">The help keyword.</param>
        /// <param name="senderName">The sender name of the event.</param>
        /// <param name="importance">The importance of the message.</param>
        public ExtendedPropertyInitialValueSetEventArgs(
            string propertyName,
            string propertyValue,
            string propertySource,
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
            PropertyValue = propertyValue;
            PropertySource = propertySource;
        }

        /// <summary>
        /// The name of the property.
        /// </summary>
        public string PropertyName { get; set; }

        /// <summary>
        /// The value of the property.
        /// </summary>
        public string PropertyValue { get; set; }

        /// <summary>
        /// The source of the property.
        /// </summary>
        public string PropertySource { get; set; }
    }
}

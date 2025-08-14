using Microsoft.Build.Framework;

namespace StructuredLogger.BinaryLogger
{
    public class ExtendedPropertyInitialValueSetEventArgs : PropertyInitialValueSetEventArgs
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
            : base(propertyName, propertyValue, propertySource, message, helpKeyword, senderName, importance)
        {
        }
    }
}

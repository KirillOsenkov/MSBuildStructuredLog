namespace Microsoft.Build.Logging.StructuredLogger
{
    /// <summary>
    /// Class representation of a task output item group.
    /// </summary>
    internal class OutputItem : TaskParameter
    {
        public OutputItem()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OutputItem"/> class.
        /// </summary>
        /// <param name="message">The message from the logger..</param>
        /// <param name="prefix">The prefix string (e.g. 'Output Item(s): ').</param>
        public OutputItem(string message, string prefix)
            : base(message, prefix)
        {
        }
    }
}

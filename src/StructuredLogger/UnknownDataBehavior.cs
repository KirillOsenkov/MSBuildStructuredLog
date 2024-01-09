namespace Microsoft.Build.Logging.StructuredLogger
{
    /// <summary>
    /// Indication of how the unknown data in future versions of binlogs should be handled.
    /// </summary>
    public enum UnknownDataBehavior
    {
        /// <summary>
        /// When unknown data encountered - emit a single synthetic error message for the entire build.
        /// </summary>
        Error,

        /// <summary>
        /// When unknown data encountered - emit a single synthetic warning message for the entire build.
        /// </summary>
        Warning,

        /// <summary>
        /// When unknown data encountered - emit a single synthetic message for the entire build.
        /// </summary>
        Message,

        /// <summary>
        /// Ignore the unknown data and continue reading the rest of the build.
        /// </summary>
        Ignore,

        /// <summary>
        /// Throw an exception when unknown data is encountered.
        /// </summary>
        ThrowException
    }
}

namespace Microsoft.Build.Logging.StructuredLogger
{
    /// <summary>
    /// Settings for the <see cref="BinaryLogReader"/>.
    /// </summary>
    public class ReaderSettings
    {
        public static ReaderSettings Default { get; } =
            new() { UnknownDataBehavior = UnknownDataBehavior.Warning };

        /// <summary>
        /// Indication of how the unknown data in future versions of binlogs should be handled.
        /// </summary>
        public UnknownDataBehavior UnknownDataBehavior { get; set; }
    }
}

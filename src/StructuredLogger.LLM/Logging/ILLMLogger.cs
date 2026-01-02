namespace StructuredLogger.LLM.Logging
{
    /// <summary>
    /// Logging level for LLM operations.
    /// </summary>
    public enum LoggingLevel
    {
        Quiet = 0,    // Only errors
        Normal = 1,   // Errors and important info
        Verbose = 2   // All messages including debug
    }

    /// <summary>
    /// Simple logging abstraction for LLM client operations.
    /// </summary>
    public interface ILLMLogger
    {
        /// <summary>
        /// Current logging level.
        /// </summary>
        LoggingLevel Level { get; set; }

        /// <summary>
        /// Log verbose/debug information (only shown in Verbose mode).
        /// </summary>
        void LogVerbose(string message);

        /// <summary>
        /// Log normal informational messages (shown in Normal and Verbose modes).
        /// </summary>
        void LogInfo(string message);

        /// <summary>
        /// Log errors (always shown regardless of level).
        /// </summary>
        void LogError(string message);
    }

    /// <summary>
    /// No-op logger that discards all messages.
    /// </summary>
    public class NullLLMLogger : ILLMLogger
    {
        public static readonly NullLLMLogger Instance = new NullLLMLogger();

        private NullLLMLogger() { }

        public LoggingLevel Level { get; set; } = LoggingLevel.Quiet;

        public void LogVerbose(string message) { }
        public void LogInfo(string message) { }
        public void LogError(string message) { }
    }
}

using System;

namespace StructuredLogger.LLM.Logging
{
    /// <summary>
    /// Logger that writes messages to a chat window via a callback.
    /// Used in GUI mode to show LLM client debug/error messages in the chat interface.
    /// </summary>
    public class ChatWindowLogger : ILLMLogger
    {
        private readonly Action<string, bool> addMessageCallback;
        private LoggingLevel level;

        /// <summary>
        /// Creates a new ChatWindowLogger.
        /// </summary>
        /// <param name="addMessageCallback">Callback to add a message to the chat window (message, isError)</param>
        /// <param name="level">Initial logging level (default: Normal)</param>
        public ChatWindowLogger(Action<string, bool> addMessageCallback, LoggingLevel level = LoggingLevel.Normal)
        {
            this.addMessageCallback = addMessageCallback ?? throw new ArgumentNullException(nameof(addMessageCallback));
            this.level = level;
        }

        public LoggingLevel Level
        {
            get => level;
            set => level = value;
        }

        public void LogVerbose(string message)
        {
            if (level >= LoggingLevel.Verbose)
            {
                addMessageCallback($"[Verbose] {message}", false);
            }
        }

        public void LogInfo(string message)
        {
            if (level >= LoggingLevel.Normal)
            {
                addMessageCallback($"[Info] {message}", false);
            }
        }

        public void LogError(string message)
        {
            // Errors are always shown regardless of level
            addMessageCallback($"[Error] {message}", true);
        }
    }
}

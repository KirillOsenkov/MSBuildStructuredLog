using System;

namespace StructuredLogger.LLM.Logging
{
    /// <summary>
    /// Adapter that wraps BinlogTool's CliLogger to implement ILLMLogger.
    /// Used when running GitHub Copilot client in CLI mode.
    /// </summary>
    public class CliLoggerAdapter : ILLMLogger
    {
        private readonly object cliLogger;
        private readonly Action<string> logVerboseAction;
        private readonly Action<string> logInfoAction;
        private readonly Action<string> logErrorAction;

        public LoggingLevel Level { get; set; } = LoggingLevel.Normal;

        public CliLoggerAdapter(object cliLogger)
        {
            this.cliLogger = cliLogger ?? throw new ArgumentNullException(nameof(cliLogger));
            
            // Use reflection to get the methods from CliLogger
            var type = cliLogger.GetType();
            var logVerboseMethod = type.GetMethod("LogVerbose");
            var logInfoMethod = type.GetMethod("LogInfo");
            var logErrorMethod = type.GetMethod("LogError");
            
            if (logVerboseMethod == null || logErrorMethod == null)
            {
                throw new InvalidOperationException("CliLogger must have LogVerbose and LogError methods");
            }
            
            this.logVerboseAction = message => logVerboseMethod.Invoke(cliLogger, new object[] { message });
            this.logInfoAction = logInfoMethod != null 
                ? message => logInfoMethod.Invoke(cliLogger, new object[] { message })
                : message => logVerboseMethod.Invoke(cliLogger, new object[] { message }); // Fallback to verbose
            this.logErrorAction = message => logErrorMethod.Invoke(cliLogger, new object[] { message });
        }

        public void LogVerbose(string message)
        {
            logVerboseAction(message);
        }

        public void LogInfo(string message)
        {
            logInfoAction(message);
        }

        public void LogError(string message)
        {
            logErrorAction(message);
        }
    }
}

using System;
using System.Collections.Generic;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// One logger registered for the build. Mirrors dotnet/msbuild's Microsoft.Build.Framework.RegisteredLoggerInfo
    /// (binlog format 26).
    /// </summary>
    [Serializable]
    public sealed class RegisteredLoggerInfo(string loggerName, IReadOnlyList<string> outputFilePaths, LoggerVerbosity? verbosity, string parameters)
    {
        public string LoggerName { get; } = loggerName;

        public string Parameters { get; } = parameters;

        public IReadOnlyList<string> OutputFilePaths { get; } = outputFilePaths ?? Array.Empty<string>();

        public LoggerVerbosity? Verbosity { get; } = verbosity;
    }

    /// <summary>
    /// Reports the loggers registered for the build and their output file paths (binlog format 26).
    /// Mirrors dotnet/msbuild's Microsoft.Build.Framework.LoggersRegisteredEventArgs. The message is the
    /// fallback text MSBuild formats for consumers that do not know this event; MSBuild's own console
    /// loggers print the output paths in the build summary instead.
    /// </summary>
    [Serializable]
    public sealed class LoggersRegisteredEventArgs(
        IReadOnlyList<RegisteredLoggerInfo> loggers,
        string message,
        string helpKeyword,
        string senderName,
        DateTime eventTimestamp)
        : BuildStatusEventArgs(message, helpKeyword, senderName, eventTimestamp)
    {
        public IReadOnlyList<RegisteredLoggerInfo> Loggers { get; } = loggers ?? Array.Empty<RegisteredLoggerInfo>();
    }
}

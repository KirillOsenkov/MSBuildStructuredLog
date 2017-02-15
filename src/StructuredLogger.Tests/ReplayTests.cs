using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.Build.Logging.StructuredLogger;
using Xunit;

namespace StructuredLoggerTests
{
    public class ReplayTests
    {
        [Fact]
        public void ReplayEndToEnd()
        {
            var logReplayEventSource = new LogReplayEventSource();

            var structuredLogger = new Microsoft.Build.Logging.StructuredLogger.StructuredLogger();
            structuredLogger.Parameters = "D:\\2.xml";
            structuredLogger.Initialize(logReplayEventSource);

            var fileLogger = new FileLogger();
            fileLogger.Verbosity = LoggerVerbosity.Diagnostic;
            fileLogger.Parameters = $"ENABLEMPLOGGING;SHOWPROJECTFILE=TRUE;verbosity=diagnostic;logfile=D:\\2.txt";
            fileLogger.Initialize(logReplayEventSource);

            logReplayEventSource.Replay(@"D:\1.msbuildlog");
        }
    }
}

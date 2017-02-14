using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Logging.StructuredLogger;
using Xunit;

namespace StructuredLoggerTests
{
    public class ReplayTests
    {
        [Fact]
        public void ReplayEndToEnd()
        {
            var logReplayEventSource = new LogReplayEventSource(@"D:\1.msbuildlog");

            var structuredLogger = new Microsoft.Build.Logging.StructuredLogger.StructuredLogger();
            structuredLogger.Parameters = "D:\\2.xml";
            structuredLogger.Initialize(logReplayEventSource);

            logReplayEventSource.Replay();
        }
    }
}

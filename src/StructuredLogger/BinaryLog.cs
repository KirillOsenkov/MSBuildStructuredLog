namespace Microsoft.Build.Logging.StructuredLogger
{
    public class BinaryLog
    {
        public static Build ReadBuild(string filePath)
        {
            var eventSource = new BinaryLogReplayEventSource();

            StructuredLogger.SaveLogToDisk = false;
            StructuredLogger.CurrentBuild = null;
            var structuredLogger = new StructuredLogger();
            structuredLogger.Parameters = "build.buildlog";
            structuredLogger.Initialize(eventSource);

            eventSource.Replay(filePath);

            var build = StructuredLogger.CurrentBuild;
            StructuredLogger.CurrentBuild = null;

            return build;
        }
    }
}

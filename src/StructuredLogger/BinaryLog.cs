namespace Microsoft.Build.Logging.StructuredLogger
{
    public class BinaryLog
    {
        public static Build ReadBuild(string filePath)
        {
            var eventSource = new BinaryLogReplayEventSource();

            byte[] sourceArchive = null;

            eventSource.OnBlobRead += (kind, bytes) =>
            {
                if (kind == BinaryLogRecordKind.SourceArchive)
                {
                    sourceArchive = bytes;
                }
            };

            StructuredLogger.SaveLogToDisk = false;
            StructuredLogger.CurrentBuild = null;
            var structuredLogger = new StructuredLogger();
            structuredLogger.Parameters = "build.buildlog";
            structuredLogger.Initialize(eventSource);

            eventSource.Replay(filePath);

            var build = StructuredLogger.CurrentBuild;
            StructuredLogger.CurrentBuild = null;

            build.SourceFilesArchive = sourceArchive;

            return build;
        }
    }
}

using System.Diagnostics;
using System.IO;

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
                if (kind == BinaryLogRecordKind.ProjectImportArchive)
                {
                    sourceArchive = bytes;
                }
            };

            StructuredLogger.SaveLogToDisk = false;
            StructuredLogger.CurrentBuild = null;
            var structuredLogger = new StructuredLogger();
            structuredLogger.Parameters = "build.buildlog";
            structuredLogger.Initialize(eventSource);

            var sw = Stopwatch.StartNew();
            eventSource.Replay(filePath);
            var elapsed = sw.Elapsed;

            structuredLogger.Shutdown();

            var build = StructuredLogger.CurrentBuild;
            StructuredLogger.CurrentBuild = null;

            if (build == null)
            {
                build = new Build() { Succeeded = false };
                build.AddChild(new Error() { Text = "Error when opening the file: " + filePath });
            }

            var projectImportsZip = Path.ChangeExtension(filePath, ".ProjectImports.zip");
            if (sourceArchive == null && File.Exists(projectImportsZip))
            {
                sourceArchive = File.ReadAllBytes(projectImportsZip);
            }

            build.SourceFilesArchive = sourceArchive;
            // build.AddChildAtBeginning(new Message { Text = "Elapsed: " + elapsed.ToString() });

            return build;
        }
    }
}

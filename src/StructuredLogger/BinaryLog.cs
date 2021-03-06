using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class BinaryLog
    {
        public static IEnumerable<Record> ReadRecords(string binLogFilePath)
        {
            var reader = new BinLogReader();
            return reader.ReadRecords(binLogFilePath);
        }

        public static IEnumerable<Record> ReadRecords(Stream binlogStream)
        {
            var reader = new BinLogReader();
            return reader.ReadRecords(binlogStream);
        }

        public static IEnumerable<Record> ReadRecords(byte[] binlogBytes)
        {
            var reader = new BinLogReader();
            return reader.ReadRecords(binlogBytes);
        }

        public static Build ReadBuild(string filePath)
        {
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var projectImportsZipFile = Path.ChangeExtension(filePath, ".ProjectImports.zip");
                byte[] projectImportsArchive = null;
                if (File.Exists(projectImportsZipFile))
                {
                    projectImportsArchive = File.ReadAllBytes(projectImportsZipFile);
                }

                var build = ReadBuild(stream, projectImportsArchive);
                build.LogFilePath = filePath;
                return build;
            }
        }

        public static Build ReadBuild(Stream stream, byte[] projectImportsArchive = null)
        {
            var eventSource = new BinLogReader();

            Build build = null;

            eventSource.OnBlobRead += (kind, bytes) =>
            {
                if (kind == BinaryLogRecordKind.ProjectImportArchive)
                {
                    projectImportsArchive = bytes;
                }
            };
            eventSource.OnException += ex =>
            {
                if (build != null)
                {
                    build.AddChild(new Error() { Text = "Error when reading the file: " + ex.ToString() });
                }
            };

            StructuredLogger.SaveLogToDisk = false;
            StructuredLogger.CurrentBuild = null;
            var structuredLogger = new StructuredLogger();
            structuredLogger.Parameters = "build.buildlog";
            structuredLogger.Initialize(eventSource);

            eventSource.OnFileFormatVersionRead += fileFormatVersion =>
            {
                if (fileFormatVersion >= 10)
                {
                    // since strings are already deduplicated in the file, no need to do it again
                    // TODO: but search will not work if the string table is empty
                    // structuredLogger.Construction.StringTable.DisableDeduplication = true;
                }
            };

            build = structuredLogger.Construction.Build;

            var sw = Stopwatch.StartNew();
            eventSource.Replay(stream);
            var elapsed = sw.Elapsed;

            structuredLogger.Shutdown();

            build = StructuredLogger.CurrentBuild;
            StructuredLogger.CurrentBuild = null;

            if (build == null)
            {
                build = new Build() { Succeeded = false };
                build.AddChild(new Error() { Text = "Error when opening the log file." });
            }

            if (build.SourceFilesArchive == null && projectImportsArchive != null)
            {
                build.SourceFilesArchive = projectImportsArchive;
            }

            // build.AddChildAtBeginning(new Message { Text = "Elapsed: " + elapsed.ToString() });

            return build;
        }
    }
}

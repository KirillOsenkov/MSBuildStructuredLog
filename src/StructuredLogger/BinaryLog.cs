using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

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

        public static Build ReadBuild(string filePath) => ReadBuild(filePath, progress: null);

        public static Build ReadBuild(string filePath, Progress progress)
        {
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var projectImportsZipFile = Path.ChangeExtension(filePath, ".ProjectImports.zip");
                byte[] projectImportsArchive = null;
                if (File.Exists(projectImportsZipFile))
                {
                    projectImportsArchive = File.ReadAllBytes(projectImportsZipFile);
                }

                var build = ReadBuild(stream, progress, projectImportsArchive);
                build.LogFilePath = filePath;
                return build;
            }
        }

        public static Build ReadBuild(Stream stream, byte[] projectImportsArchive = null)
            => ReadBuild(stream, progress: null, projectImportsArchive: projectImportsArchive);

        internal const bool ReuseBinlogStrings = false;

        public static Build ReadBuild(Stream stream, Progress progress, byte[] projectImportsArchive = null)
        {
            var eventSource = new BinLogReader();

            Build build = null;
            IEnumerable<string> strings = null;

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
            eventSource.OnStringDictionaryComplete += s =>
            {
                strings = s;
            };

            StructuredLogger.SaveLogToDisk = false;
            StructuredLogger.CurrentBuild = null;
            var structuredLogger = new StructuredLogger();
            structuredLogger.Parameters = "build.buildlog";
            structuredLogger.Initialize(eventSource);

            build = structuredLogger.Construction.Build;

            eventSource.OnFileFormatVersionRead += fileFormatVersion =>
            {
                build.FileFormatVersion = fileFormatVersion;

                // strings are deduplicated starting with version 10
                if (fileFormatVersion >= 10)
                {
                    build.StringTable.NormalizeLineEndings = false;
                    build.StringTable.HasDeduplicatedStrings = true;
                    if (ReuseBinlogStrings)
                    {
                        build.StringTable.DisableDeduplication = true;
                    }
                }
            };

            var sw = Stopwatch.StartNew();

            eventSource.Replay(stream, progress);

            var elapsed = sw.Elapsed;

            if (strings != null)
            {
                if (ReuseBinlogStrings)
                {
                    // since strings are already deduplicated in the file, no need to do it again
                    build.StringTable.SetStrings(strings);
                }
                else
                {
                    // intern all strings in one fell swoop here instead of interning multiple times
                    // one by one when processing task parameters
                    build.StringTable.Intern(strings);
                }
            }

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

            // strings = build.StringTable.Instances.OrderBy(s => s).ToArray();

            // Serialization.WriteStringsToFile(@"C:\temp\1.txt", strings.ToArray());

            return build;
        }
    }
}

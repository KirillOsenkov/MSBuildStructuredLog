using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using static Microsoft.Build.Logging.StructuredLogger.BinLogReader;

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
        public static Build ReadBuild(string filePath, Progress progress) => ReadBuild(filePath, progress, readerSettings: null);
        public static Build ReadBuild(string filePath, ReaderSettings readerSettings)
            => ReadBuild(filePath, progress: null, readerSettings);

        public static Build ReadBuild(string filePath, Progress progress, ReaderSettings readerSettings)
        {
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var projectImportsZipFile = Path.ChangeExtension(filePath, ".ProjectImports.zip");
                byte[] projectImportsArchive = null;
                if (File.Exists(projectImportsZipFile))
                {
                    projectImportsArchive = File.ReadAllBytes(projectImportsZipFile);
                }

                var build = ReadBuild(stream, progress, projectImportsArchive, readerSettings);
                build.LogFilePath = filePath;
                return build;
            }
        }

        public static Build ReadBuild(Stream stream, byte[] projectImportsArchive = null)
            => ReadBuild(stream, progress: null, projectImportsArchive: projectImportsArchive);

        public static Build ReadBuild(Stream stream, Progress progress, byte[] projectImportsArchive = null)
            => ReadBuild(stream, progress, projectImportsArchive, readerSettings: null);

        public static Build ReadBuild(
            Stream stream,
            Progress progress,
            byte[] projectImportsArchive = null,
            ReaderSettings readerSettings = null)
        {
            Build build = null;
            IEnumerable<string> strings = null;
            readerSettings ??= ReaderSettings.Default;

            var eventSource = new BinLogReader();

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
            int[] errorByType = new int[Enum.GetValues(typeof(ReaderErrorType)).Length];
            eventSource.RecoverableReadError += eArg =>
            {
                if (readerSettings.UnknownDataBehavior == UnknownDataBehavior.ThrowException)
                {
                    throw new Exception($"Unknown data encountered in the log file ({eArg.ErrorType}-{eArg.RecordKind}): {eArg.GetFormattedMessage()}");
                }

                if (readerSettings.UnknownDataBehavior == UnknownDataBehavior.Ignore)
                {
                    return;
                }

                errorByType[(int)eArg.ErrorType] += 1;
            };

            //build.AddChild(new );

            StructuredLogger.SaveLogToDisk = false;
            StructuredLogger.CurrentBuild = null;
            var structuredLogger = new StructuredLogger();
            structuredLogger.Parameters = "build.buildlog";
            structuredLogger.Initialize(eventSource);

            build = structuredLogger.Construction.Build;

            if (stream is FileStream)
            {
                structuredLogger.Construction.IsLargeBinlog = stream.Length > 100_000_000;
            }

            eventSource.OnFileFormatVersionRead += fileFormatVersion =>
            {
                build.FileFormatVersion = fileFormatVersion;

                // strings are deduplicated starting with version 10
                if (fileFormatVersion >= 10)
                {
                    build.StringTable.NormalizeLineEndings = false;
                    build.StringTable.HasDeduplicatedStrings = true;
                }
            };

            var sw = Stopwatch.StartNew();

            eventSource.Replay(stream, progress);

            var elapsed = sw.Elapsed;

            if (strings != null)
            {
                // intern all strings in one fell swoop here instead of interning multiple times
                // one by one when processing task parameters
                build.StringTable.Intern(strings);
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

            build.WaitForBackgroundTasks();

            if (errorByType.Any(i => i != 0))
            {
                string summary = string.Join(", ", errorByType.Where((count, index) => count > 0).Select((count, index) => $"{((ReaderErrorType)index)}: {count} cases"));
                string message = $"Skipped some data unknown to this version of Viewer. {errorByType.Sum()} case{(errorByType.Sum() > 1 ? "s" : string.Empty)} encountered ({summary}).";

                TreeNode node = readerSettings.UnknownDataBehavior switch
                {
                    UnknownDataBehavior.Error => new Error() { Text = message },
                    UnknownDataBehavior.Warning => new Warning() { Text = message },
                    UnknownDataBehavior.Message => new CriticalBuildMessage() { Text = message },
                    _ => throw new ArgumentOutOfRangeException(nameof(readerSettings.UnknownDataBehavior), readerSettings.UnknownDataBehavior, "Unexpected value")
                };

                build.AddChild(node);
            }

            return build;
        }
    }
}

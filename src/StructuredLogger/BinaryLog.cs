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

        public static Build ReadBuild(string filePath, UnknownDataBehavior unknownDataBehavior = UnknownDataBehavior.Error)
            => ReadBuild(filePath, progress: null, unknownDataBehavior);

        public static Build ReadBuild(string filePath, Progress progress, UnknownDataBehavior unknownDataBehavior)
        {
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var projectImportsZipFile = Path.ChangeExtension(filePath, ".ProjectImports.zip");
                byte[] projectImportsArchive = null;
                if (File.Exists(projectImportsZipFile))
                {
                    projectImportsArchive = File.ReadAllBytes(projectImportsZipFile);
                }

                var build = ReadBuild(stream, progress, projectImportsArchive, unknownDataBehavior);
                build.LogFilePath = filePath;
                return build;
            }
        }

        public static Build ReadBuild(Stream stream, byte[] projectImportsArchive = null)
            => ReadBuild(stream, progress: null, projectImportsArchive: projectImportsArchive);

        //UnknownDataBehavior

        public static Build ReadBuild(
            Stream stream,
            Progress progress,
            byte[] projectImportsArchive = null,
            UnknownDataBehavior unknownDataBehavior = UnknownDataBehavior.Error)
        {
            Build build = null;
            IEnumerable<string> strings = null;

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
                if (unknownDataBehavior == UnknownDataBehavior.ThrowException)
                {
                    throw new Exception($"Unknown data encountered in the log file ({eArg.ErrorType}-{eArg.RecordKind}): {eArg.GetFormattedMessage()}");
                }

                if (unknownDataBehavior == UnknownDataBehavior.Ignore)
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
                string summary = string.Join(", ", errorByType.Where((count, index) => count > 0).Select((count, index) => $"{((ReaderErrorType)index).ToString()}: {count}"));
                string message = $"{errorByType.Sum()} reading errors encountered ({summary}) - unknown data was skipped in current compatibility mode.";

                TreeNode node = unknownDataBehavior switch
                {
                    UnknownDataBehavior.Error => new Error() { Text = message },
                    UnknownDataBehavior.Warning => new Warning() { Text = message },
                    UnknownDataBehavior.Message => new CriticalBuildMessage() { Text = message },
                    _ => throw new ArgumentOutOfRangeException(nameof(unknownDataBehavior), unknownDataBehavior, null)
                };

                build.AddChildAtBeginning(node);
            }

            return build;
        }
    }
}

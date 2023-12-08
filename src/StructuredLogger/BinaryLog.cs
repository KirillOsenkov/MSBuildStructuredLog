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

        public static Build ReadBuild(string filePath, IForwardCompatibilityReadSettings forwardCompatibilitySettings = null)
            => ReadBuild(filePath, progress: null, forwardCompatibilitySettings);

        public static Build ReadBuild(string filePath, Progress progress, IForwardCompatibilityReadSettings forwardCompatibilitySettings)
        {
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var projectImportsZipFile = Path.ChangeExtension(filePath, ".ProjectImports.zip");
                byte[] projectImportsArchive = null;
                if (File.Exists(projectImportsZipFile))
                {
                    projectImportsArchive = File.ReadAllBytes(projectImportsZipFile);
                }

                var build = ReadBuild(stream, progress, projectImportsArchive, forwardCompatibilitySettings);
                build.LogFilePath = filePath;
                return build;
            }
        }

        public static Build ReadBuild(Stream stream, byte[] projectImportsArchive = null)
            => ReadBuild(stream, progress: null, projectImportsArchive: projectImportsArchive);

        public static Build ReadBuild(Stream stream, Progress progress, byte[] projectImportsArchive = null, IForwardCompatibilityReadSettings forwardCompatibilitySettings = null)
        {
            Build build = null;
            IEnumerable<string> strings = null;
            ReaderErrorsRegistry readerErrorsRegistry = null;

            void AddRecoverableReaderError(BinaryLogReaderErrorEventArgs errorEventArgs)
            {
                // Build is read synchronously. If that ever changes - this should change to proper Lazy.
                readerErrorsRegistry ??= new ReaderErrorsRegistry();
                readerErrorsRegistry.Add(errorEventArgs);
            }

            forwardCompatibilitySettings =
                forwardCompatibilitySettings?.WithCustomErrorHandler(AddRecoverableReaderError);
            var eventSource = new BinLogReader()
            {
                ForwardCompatibilitySettings = forwardCompatibilitySettings
            };

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

            build.IsCompatibilityMode = eventSource.IsCompatibilityMode;
            build.RecoverableReadingErrors = readerErrorsRegistry?.GetErrors().ToList() ??
                new List<(ReaderErrorType errorType, BinaryLogRecordKind recordKind, int count)>();

            return build;
        }

        private class ReaderErrorsRegistry
        {
            private readonly Dictionary<int, int>[] _errorsByKind =
                Enum.GetValues(typeof(ReaderErrorType))
                    .Cast<ReaderErrorType>()
                    .Select(_ => new Dictionary<int, int>())
                    .ToArray();

            public void Add(BinaryLogReaderErrorEventArgs errorEventArgs)
            {
                int errorType = (int)errorEventArgs.ErrorType;
                int kind = (int)errorEventArgs.RecordKind;

                _errorsByKind[errorType].TryGetValue(kind, out var currentCount);
                _errorsByKind[errorType][kind] = currentCount + 1;
            }

            public IEnumerable<(ReaderErrorType errorType, BinaryLogRecordKind recordKind, int count)> GetErrors()
            {
                for (int errorType = 0; errorType < _errorsByKind.Length; errorType++)
                {
                    Dictionary<int, int> errors = _errorsByKind[errorType];
                    foreach (KeyValuePair<int, int> kvp in errors)
                    {
                        yield return ((ReaderErrorType)errorType, (BinaryLogRecordKind)kvp.Key, kvp.Value);
                    }
                }
            }
        }
    }
}

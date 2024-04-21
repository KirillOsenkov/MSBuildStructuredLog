using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Logging.StructuredLogger
{
    /// <summary>
    /// Provides a method to read a binary log file (*.binlog) and replay all stored BuildEventArgs
    /// by implementing IEventSource and raising corresponding events.
    /// </summary>
    /// <remarks>The class is public so that we can call it from MSBuild.exe when replaying a log file.</remarks>
    public sealed class BinLogReader : EventArgsDispatcher
    {
        /// <summary>
        /// Raised when the log reader encounters a binary blob embedded in the stream.
        /// The arguments include the blob kind and the byte buffer with the contents.
        /// </summary>
        public event Action<BinaryLogRecordKind, byte[]> OnBlobRead;
        public event Action<string, long> OnStringRead;
        public event Action<IDictionary<string, string>, long> OnNameValueListRead;
        public event Action<int> OnFileFormatVersionRead;
        public event Action<IEnumerable<string>> OnStringDictionaryComplete;

        /// <summary>
        /// Raised when there was an exception reading a record from the file.
        /// </summary>
        public event Action<Exception> OnException;

        /// <summary>
        /// Receives recoverable errors during reading. See <see cref="IBuildEventArgsReaderNotifications.RecoverableReadError"/> for documentation on arguments.
        /// </summary>
        public event Action<BinaryLogReaderErrorEventArgs>? RecoverableReadError;

        /// <summary>
        /// Read the provided binary log file and raise corresponding events for each BuildEventArgs
        /// </summary>
        /// <param name="sourceFilePath">The full file path of the binary log file</param>
        public void Replay(string sourceFilePath) => Replay(sourceFilePath, progress: null);

        /// <summary>
        /// Read the provided binary log file and raise corresponding events for each BuildEventArgs
        /// </summary>
        /// <param name="sourceFilePath">The full file path of the binary log file</param>
        /// <param name="progress">optional callback to receive progress updates</param>
        public void Replay(string sourceFilePath, Progress progress)
        {
            using (var stream = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Replay(stream, progress);
            }
        }

        public void Replay(Stream stream)
        {
            Replay(stream, progress: null);
        }

        public void Replay(Stream stream, Progress progress)
        {
            var gzipStream = new GZipStream(stream, CompressionMode.Decompress, leaveOpen: true);
            var bufferedStream = new BufferedStream(gzipStream, 32768);
            var binaryReader = new BinaryReader(bufferedStream);

            using var reader = OpenReader(binaryReader);

            if (PlatformUtilities.HasThreads)
            {
                var queue = new BatchBlockingCollection<BuildEventArgs>(boundedCapacity: 10);
                queue.ProcessItem += Dispatch;

                int recordsRead = 0;

                reader.OnBlobRead += OnBlobRead;

                Stopwatch stopwatch = Stopwatch.StartNew();

                var streamLength = stream.Length;

                while (true)
                {
                    BuildEventArgs instance = null;

                    try
                    {
                        instance = reader.Read();
                    }
                    catch (Exception ex)
                    {
                        OnException?.Invoke(ex);
                    }

                    recordsRead++;
                    if (instance == null)
                    {
                        queue.CompleteAdding();
                        break;
                    }

                    queue.Add(instance);

                    // only check the stopwatch every 1000 records, otherwise Stopwatch is showing up in profiles
                    if (progress != null && (recordsRead % 1000) == 0 && stopwatch.ElapsedMilliseconds > 200)
                    {
                        stopwatch.Restart();
                        var streamPosition = stream.Position;
                        double ratio = (double)streamPosition / streamLength;
                        progress.Report(new ProgressUpdate { Ratio = ratio, BufferLength = queue.Count });
                    }
                }

                queue.Completion.Wait();

                if (reader.FileFormatVersion >= 10)
                {
                    var strings = reader.GetStrings();
                    if (strings != null && strings.Any())
                    {
                        OnStringDictionaryComplete?.Invoke(strings);
                    }
                }
            }
            else
            {
                var queue = new List<BuildEventArgs>();

                int recordsRead = 0;

                reader.OnBlobRead += OnBlobRead;

                Stopwatch stopwatch = Stopwatch.StartNew();

                var streamLength = stream.Length;

                while (true)
                {
                    BuildEventArgs instance = null;

                    try
                    {
                        instance = reader.Read();
                    }
                    catch (Exception ex)
                    {
                        OnException?.Invoke(ex);
                    }

                    recordsRead++;
                    if (instance == null)
                    {
                        break;
                    }

                    queue.Add(instance);

                    if (progress != null && stopwatch.ElapsedMilliseconds > 200)
                    {
                        stopwatch.Restart();
                        var streamPosition = stream.Position;
                        double ratio = (double)streamPosition / streamLength;
                        progress.Report(new ProgressUpdate { Ratio = ratio, BufferLength = queue.Count });
                    }
                }

                foreach (var args in queue)
                {
                    Dispatch(args);
                }
                if (reader.FileFormatVersion >= 10)
                {
                    var strings = reader.GetStrings();
                    if (strings != null && strings.Any())
                    {
                        OnStringDictionaryComplete?.Invoke(strings);
                    }
                }
            }

            if (progress != null)
            {
                progress.Report(new ProgressUpdate { Ratio = 1.0, BufferLength = 0 });
            }
        }

        private BuildEventArgsReader OpenReader(BinaryReader binaryReader)
        {
            int fileFormatVersion = binaryReader.ReadInt32();
            // Is this the new log format that contains the minimum reader version?
            bool hasEventOffsets = fileFormatVersion >= BinaryLogger.ForwardCompatibilityMinimalVersion;
            int minimumReaderVersion = hasEventOffsets
                ? binaryReader.ReadInt32()
                : fileFormatVersion;

            OnFileFormatVersionRead?.Invoke(fileFormatVersion);

            EnsureFileFormatVersionKnown(fileFormatVersion, minimumReaderVersion);

            var reader = new BuildEventArgsReader(binaryReader, fileFormatVersion);

            reader.SkipUnknownEventParts = hasEventOffsets;
            reader.SkipUnknownEvents = hasEventOffsets;

            // ensure some handler is subscribed, even if we are not interested in the events
            reader.RecoverableReadError += RecoverableReadError ?? (_ => { });

            return reader;
        }

        private void EnsureFileFormatVersionKnown(int fileFormatVersion, int minimumReaderVersion)
        {
            if (fileFormatVersion > BinaryLogger.FileFormatVersion)
            {
                // prefer the update to newer version to forward compatibility mode
                if (minimumReaderVersion <= BinaryLogger.FileFormatVersion && !BinaryLogger.IsNewerVersionAvailable)
                {
                    return;
                }

                var text = $"Unsupported log file format. Latest supported version is {BinaryLogger.FileFormatVersion}, the log file has version {fileFormatVersion} (with minimum required reader version {minimumReaderVersion}).";
                // prefer the update to newer version to forward compatibility mode
                if (BinaryLogger.IsNewerVersionAvailable)
                {
                    text += " Update available - restart this instance to automatically use newer version.";
                }
                throw new NotSupportedException(text);
            }
        }

        private class DisposableEnumerable<T> : IEnumerable<T>, IDisposable
        {
            private IEnumerable<T> enumerable;
            private Action dispose;

            public static IEnumerable<T> Create(IEnumerable<T> enumerable, Action dispose)
            {
                return new DisposableEnumerable<T>(enumerable, dispose);
            }

            public DisposableEnumerable(IEnumerable<T> enumerable, Action dispose)
            {
                this.enumerable = enumerable;
                this.dispose = dispose;
            }

            public void Dispose()
            {
                if (dispose != null)
                {
                    dispose();
                    dispose = null;
                }
            }

            public IEnumerator<T> GetEnumerator() => enumerable.GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => enumerable.GetEnumerator();
        }

        /// <summary>
        /// Enumerate over all records in the file. For each record store the bytes,
        /// the start position in the stream, length in bytes and the deserialized object.
        /// </summary>
        /// <remarks>Useful for debugging and analyzing binary logs</remarks>
        public IEnumerable<Record> ReadRecords(string logFilePath)
        {
            var stream = new FileStream(logFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return DisposableEnumerable<Record>.Create(ReadRecords(stream), () => stream.Dispose());
        }

        /// <summary>
        /// Enumerate over all records in the file. For each record reports
        /// the start position in the stream, length in bytes and the type of record.
        /// </summary>
        /// <remarks>Useful for debugging and analyzing binary logs</remarks>
        public IEnumerable<RecordInfo> ChunkBinlog(string logFilePath)
        {
            var stream = new FileStream(logFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return DisposableEnumerable<RecordInfo>.Create(
                ChunkBinlogFromDecompressedStream(GetDecompressedStream(stream)), () => stream.Dispose());
        }

        public IEnumerable<Record> ReadRecords(byte[] bytes)
        {
            var stream = new MemoryStream(bytes);
            return ReadRecords(stream);
        }

        /// <summary>
        /// Enumerate over all records in the binary log stream. For each record store the bytes,
        /// the start position in the stream, length in bytes and the deserialized object.
        /// </summary>
        /// <remarks>Useful for debugging and analyzing binary logs</remarks>
        public IEnumerable<Record> ReadRecords(Stream binaryLogStream)
            => ReadRecordsFromDecompressedStream(GetDecompressedStream(binaryLogStream));

        private static Stream GetDecompressedStream(Stream binaryLogStream)
        {
            var gzipStream = new GZipStream(binaryLogStream, CompressionMode.Decompress, leaveOpen: true);
            var bufferedStream = new BufferedStream(gzipStream, 32768);
            return bufferedStream;
        }


        private IEnumerable<RecordInfo> ChunkBinlogFromDecompressedStream(Stream decompressedStream)
        {
            var binaryReader = new BinaryReader(decompressedStream);
            using BuildEventArgsReader reader = OpenReader(binaryReader);
            var hasOffsets = reader.FileFormatVersion >= BinaryLogger.ForwardCompatibilityMinimalVersion;

            if (hasOffsets)
            {
                return ChunkBinlogWithOffsets(reader);
            }
            else
            {
                return ReadRecordsFromDecompressedStream(reader, true)
                    .Select(r => new RecordInfo(r.Args != null ? ToBinaryLogRecordKind(r.Args) : r.Kind, r.Start, r.Length));
            }
        }

        private IEnumerable<RecordInfo> ChunkBinlogWithOffsets(
            BuildEventArgsReader reader)
        {
            long start = 0;
            BuildEventArgsReader.RawRecord chunk;
            do
            {
                chunk = reader.ReadRaw(false);
                yield return new RecordInfo(chunk.RecordKind, start, chunk.Stream.Length);
                start += chunk.Stream.Length;
            } while (chunk.RecordKind != BinaryLogRecordKind.EndOfFile);
        }

        public static BinaryLogRecordKind ToBinaryLogRecordKind(BuildEventArgs args)
            => args switch
            {
                BuildStartedEventArgs _ => BinaryLogRecordKind.BuildStarted,
                BuildFinishedEventArgs _ => BinaryLogRecordKind.BuildFinished,
                ProjectStartedEventArgs _ => BinaryLogRecordKind.ProjectStarted,
                ProjectFinishedEventArgs _ => BinaryLogRecordKind.ProjectFinished,
                TargetStartedEventArgs _ => BinaryLogRecordKind.TargetStarted,
                TargetFinishedEventArgs _ => BinaryLogRecordKind.TargetFinished,
                TaskStartedEventArgs _ => BinaryLogRecordKind.TaskStarted,
                TaskFinishedEventArgs _ => BinaryLogRecordKind.TaskFinished,
                BuildErrorEventArgs _ => BinaryLogRecordKind.Error,
                BuildWarningEventArgs _ => BinaryLogRecordKind.Warning,
                CriticalBuildMessageEventArgs _ => BinaryLogRecordKind.CriticalBuildMessage,
                TaskCommandLineEventArgs _ => BinaryLogRecordKind.TaskCommandLine,
                TaskParameterEventArgs _ => BinaryLogRecordKind.TaskParameter,
                ProjectEvaluationStartedEventArgs _ => BinaryLogRecordKind.ProjectEvaluationStarted,
                ProjectEvaluationFinishedEventArgs _ => BinaryLogRecordKind.ProjectEvaluationFinished,
                ProjectImportedEventArgs _ => BinaryLogRecordKind.ProjectImported,
                TargetSkippedEventArgs _ => BinaryLogRecordKind.TargetSkipped,
                EnvironmentVariableReadEventArgs _ => BinaryLogRecordKind.EnvironmentVariableRead,
                FileUsedEventArgs _ => BinaryLogRecordKind.FileUsed,
                PropertyReassignmentEventArgs _ => BinaryLogRecordKind.PropertyReassignment,
                UninitializedPropertyReadEventArgs _ => BinaryLogRecordKind.UninitializedPropertyRead,
                PropertyInitialValueSetEventArgs _ => BinaryLogRecordKind.PropertyInitialValueSet,
                AssemblyLoadBuildEventArgs _ => BinaryLogRecordKind.AssemblyLoad,
                BuildMessageEventArgs _ => BinaryLogRecordKind.Message,
                _ => throw new NotImplementedException(),
            };

        public IEnumerable<Record> ReadRecordsFromDecompressedStream(Stream decompressedStream)
            => ReadRecordsFromDecompressedStream(decompressedStream, includeAuxiliaryRecords: false);

        public IEnumerable<Record> ReadRecordsFromDecompressedStream(Stream decompressedStream,
            bool includeAuxiliaryRecords)
        {
            var binaryReader = new BinaryReader(decompressedStream);
            using var reader = OpenReader(binaryReader);
            return ReadRecordsFromDecompressedStream(reader, includeAuxiliaryRecords);
        }

        internal IEnumerable<Record> ReadRecordsFromDecompressedStream(BuildEventArgsReader reader, bool includeAuxiliaryRecords)
        {
            List<Record> blobs = new List<Record>();

            Strings.Initialize();

            // forward the events from the reader to the subscribers of this class
            reader.OnBlobRead += OnBlobRead;

            long start = 0;

            List<Record>? auxiliaryRecords = includeAuxiliaryRecords ? [] : null;

            reader.OnBlobRead += (kind, blob) =>
            {
                start = reader.Position;

                var record = new Record
                {
                    Bytes = blob,
                    Kind = BinaryLogRecordKind.ProjectImportArchive,
                    Args = null,
                    Start = start - blob.Length,
                    Length = blob.Length
                };

                if (auxiliaryRecords != null)
                {
                    auxiliaryRecords.Add(record);
                }
                else
                {
                    blobs.Add(record);
                }
            };

            reader.OnStringRead += text =>
            {
                long length = reader.Position - start;
                auxiliaryRecords?.Add(new Record { Kind = BinaryLogRecordKind.String, Start = start, Length = length });

                // re-read the current position as we're just about to start reading
                // the actual BuildEventArgs record
                start = reader.Position;

                OnStringRead?.Invoke(text, length);
            };

            reader.OnNameValueListRead += list =>
            {
                long length = reader.Position - start;
                auxiliaryRecords?.Add(new Record { Kind = BinaryLogRecordKind.NameValueList, Start = start, Length = length });
                start = reader.Position;
                OnNameValueListRead?.Invoke(list, length);
            };

            while (true)
            {
                BuildEventArgs instance = null;

                start = reader.Position;

                instance = reader.Read();
                if (instance == null)
                {
                    auxiliaryRecords?.Add(new Record { Kind = BinaryLogRecordKind.EndOfFile, Start = start, Length = reader.Position - start });
                }
                foreach (Record auxiliaryRecord in auxiliaryRecords ?? Enumerable.Empty<Record>())
                {
                    yield return auxiliaryRecord;
                }
                auxiliaryRecords?.Clear();
                if (instance == null)
                {
                    break;
                }

                var record = new Record
                {
                    Bytes = null, // probably can reconstruct this from the Args if necessary
                    Args = instance,
                    Start = start,
                    Length = reader.Position - start
                };

                yield return record;
            }

            foreach (var blob in blobs)
            {
                yield return blob;
            }
        }
    }
}

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
        /// If set - controls the behavior of forward compatibility reading.
        /// </summary>
        public IForwardCompatibilityReadSettings ForwardCompatibilitySettings { private get; set; }

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

            bool isLogOfNewerVersion = fileFormatVersion > BinaryLogger.FileFormatVersion;

            var reader = new BuildEventArgsReader(binaryReader, fileFormatVersion);

            reader.SkipUnknownEventParts = hasEventOffsets;
            reader.SkipUnknownEvents = hasEventOffsets;
            if (hasEventOffsets && !isLogOfNewerVersion)
            {
                reader.RecoverableReadError += HandleReadingErrorOnKnownVersion;
            }

            // ensure some handler is subscribed, even if we are not interested in the events
            reader.RecoverableReadError += ForwardCompatibilitySettings?.ErrorHandler ?? (_ => { });

            return reader;

            void HandleReadingErrorOnKnownVersion(BinaryLogReaderErrorEventArgs arg)
            {
                string text =
                    $"Log is of a known format ({fileFormatVersion}, latest known version is {BinaryLogger.FileFormatVersion}), but reader encountered a recoverable reading error ({arg.GetFormattedMessage()}), which probably means the format version was forgotten to be incremented. The log can be read with current reader, the newer events and data will however be skipped.";
                if (!IsForwardCompatibilityModeAllowed(text))
                {
                    throw new NotSupportedException(text);
                }

                // We want this to be only one time event handler
                reader.RecoverableReadError -= HandleReadingErrorOnKnownVersion;
            }
        }

        public bool IsCompatibilityMode { get; private set; }

        // Store the user's decision about forward compatibility mode
        //  for the future calls, that won't pass the user handler.
        // This is needed to avoid asking the user for each action (e.g.
        //  once the log is opened, we do not want to ask again when
        //  calculating stats, redacting, etc.). But at the same time we
        //  *do* want to ask the user again if the log (same or different)
        //  is being opened again within same app run.
        private static bool? _allowForwardCompatibility = null;
        private bool IsForwardCompatibilityModeAllowed(string text)
        {
            IsCompatibilityMode = true;
            if (ForwardCompatibilitySettings != null)
            {
                _allowForwardCompatibility = ForwardCompatibilitySettings.AllowForwardCompatibility(text);
            }

            return _allowForwardCompatibility ?? false;
        }

        private void EnsureFileFormatVersionKnown(int fileFormatVersion, int minimumReaderVersion)
        {
            if (fileFormatVersion > BinaryLogger.FileFormatVersion)
            {
                bool forwardCompatibilityModeAllowed = false;
                // prefer the update to newer version to forward compatibility mode
                if (minimumReaderVersion <= BinaryLogger.FileFormatVersion && !BinaryLogger.IsNewerVersionAvailable)
                {
                    var text =
                        $"Newer log file format. Latest known version is {BinaryLogger.FileFormatVersion}, the log file has version {fileFormatVersion}. The log has minimum required reader version {minimumReaderVersion} - so it can be read with current reader, the newer events and data will however be skipped.";
                    forwardCompatibilityModeAllowed = IsForwardCompatibilityModeAllowed(text);
                }

                // prefer the update to newer version to forward compatibility mode
                if (!forwardCompatibilityModeAllowed || BinaryLogger.IsNewerVersionAvailable)
                {
                    var text = $"Unsupported log file format. Latest supported version is {BinaryLogger.FileFormatVersion}, the log file has version {fileFormatVersion} (with minimum required reader version {minimumReaderVersion}).";
                    if (BinaryLogger.IsNewerVersionAvailable)
                    {
                        text += " Update available - restart this instance to automatically use newer version.";
                    }

                    throw new NotSupportedException(text);
                }
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
        {
            var gzipStream = new GZipStream(binaryLogStream, CompressionMode.Decompress, leaveOpen: true);
            var bufferedStream = new BufferedStream(gzipStream, 32768);
            return ReadRecordsFromDecompressedStream(bufferedStream);
        }

        public IEnumerable<Record> ReadRecordsFromDecompressedStream(Stream decompressedStream)
        {
            var wrapper = new WrapperStream(decompressedStream);

            var binaryReader = new BinaryReader(wrapper);

            long lengthOfBlobsAddedLastTime = 0;

            List<Record> blobs = new List<Record>();

            using var reader = OpenReader(binaryReader);

            // forward the events from the reader to the subscribers of this class
            reader.OnBlobRead += OnBlobRead;

            long start = 0;

            reader.OnBlobRead += (kind, blob) =>
            {
                start = wrapper.Position;

                var record = new Record
                {
                    Bytes = blob,
                    Args = null,
                    Start = start - blob.Length, // TODO: check if this is accurate
                    Length = blob.Length
                };

                blobs.Add(record);
                lengthOfBlobsAddedLastTime += blob.Length;
            };

            reader.OnStringRead += text =>
            {
                long length = wrapper.Position - start;

                // re-read the current position as we're just about to start reading
                // the actual BuildEventArgs record
                start = wrapper.Position;

                OnStringRead?.Invoke(text, length);
            };

            reader.OnNameValueListRead += list =>
            {
                long length = wrapper.Position - start;
                start = wrapper.Position;
                OnNameValueListRead?.Invoke(list, length);
            };

            while (true)
            {
                BuildEventArgs instance = null;

                start = wrapper.Position;

                instance = reader.Read();
                if (instance == null)
                {
                    break;
                }

                var record = new Record
                {
                    Bytes = null, // probably can reconstruct this from the Args if necessary
                    Args = instance,
                    Start = start,
                    Length = wrapper.Position - start
                };

                yield return record;

                lengthOfBlobsAddedLastTime = 0;
            }

            foreach (var blob in blobs)
            {
                yield return blob;
            }
        }
    }

    public class WrapperStream : Stream
    {
        private readonly Stream stream;

        public WrapperStream(Stream stream)
        {
            this.stream = stream;
        }

        public override bool CanRead => stream.CanRead;

        public override bool CanSeek => stream.CanSeek;

        public override bool CanWrite => stream.CanWrite;

        public override long Length => stream.Length;

        private long position;
        public override long Position
        {
            get => position;
            set => throw new NotImplementedException();
        }

        public override void Flush()
        {
            stream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var result = stream.Read(buffer, offset, count);
            position += result;
            return result;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return stream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            stream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            stream.Write(buffer, offset, count);
        }
    }
}

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

            int fileFormatVersion = binaryReader.ReadInt32();

            OnFileFormatVersionRead?.Invoke(fileFormatVersion);

            EnsureFileFormatVersionKnown(fileFormatVersion);

            if (PlatformUtilities.HasThreads)
            {
                // Use a producer-consumer queue so that IO can happen on one thread
                // while processing can happen on another thread decoupled. The speed
                // up is from 4.65 to 4.15 seconds.

                // see issue https://github.com/KirillOsenkov/MSBuildStructuredLog/issues/684
                var maxBoundedCapacity = 50000;

                var queue = new BlockingCollection<BuildEventArgs>(boundedCapacity: maxBoundedCapacity);
                var processingTask = System.Threading.Tasks.Task.Run(() =>
                {
                    foreach (var args in queue.GetConsumingEnumerable())
                    {
                        Dispatch(args);
                    }
                });

                int recordsRead = 0;

                using var reader = new BuildEventArgsReader(binaryReader, fileFormatVersion);
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

                    if (progress != null && stopwatch.ElapsedMilliseconds > 200)
                    {
                        stopwatch.Restart();
                        var streamPosition = stream.Position;
                        double ratio = (double)streamPosition / streamLength;
                        progress.Report(ratio);
                    }
                }

                processingTask.Wait();
                if (fileFormatVersion >= 10)
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

                using var reader = new BuildEventArgsReader(binaryReader, fileFormatVersion);
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
                        progress.Report(ratio);
                    }
                }

                foreach (var args in queue)
                {
                    Dispatch(args);
                }
                if (fileFormatVersion >= 10)
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
                progress.Report(1.0);
            }
        }

        private void EnsureFileFormatVersionKnown(int fileFormatVersion)
        {
            // the log file is written using a newer version of file format
            // that we don't know how to read
            if (fileFormatVersion > BinaryLogger.FileFormatVersion)
            {
                var text = $"Unsupported log file format. Latest supported version is {BinaryLogger.FileFormatVersion}, the log file has version {fileFormatVersion}.";
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

            int fileFormatVersion = binaryReader.ReadInt32();

            EnsureFileFormatVersionKnown(fileFormatVersion);

            long lengthOfBlobsAddedLastTime = 0;

            List<Record> blobs = new List<Record>();

            using var reader = new BuildEventArgsReader(binaryReader, fileFormatVersion);

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

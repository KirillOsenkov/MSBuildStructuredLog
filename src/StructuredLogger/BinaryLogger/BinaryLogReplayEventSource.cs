using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Logging
{
    /// <summary>
    /// Provides a method to read a binary log file (*.binlog) and replay all stored BuildEventArgs
    /// by implementing IEventSource and raising corresponding events.
    /// </summary>
    /// <remarks>The class is public so that we can call it from MSBuild.exe when replaying a log file.</remarks>
    public sealed class BinaryLogReplayEventSource : EventArgsDispatcher
    {
        /// <summary>
        /// Raised when the log reader encounters a binary blob embedded in the stream.
        /// The arguments include the blob kind and the byte buffer with the contents.
        /// </summary>
        public event Action<BinaryLogRecordKind, byte[]> OnBlobRead;

        /// <summary>
        /// Read the provided binary log file and raise corresponding events for each BuildEventArgs
        /// </summary>
        /// <param name="sourceFilePath">The full file path of the binary log file</param>
        public void Replay(string sourceFilePath)
        {
            using (var stream = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var gzipStream = new GZipStream(stream, CompressionMode.Decompress, leaveOpen: true);
                var binaryReader = new BinaryReader(gzipStream);

                int fileFormatVersion = binaryReader.ReadInt32();

                // the log file is written using a newer version of file format
                // that we don't know how to read
                if (fileFormatVersion > BinaryLogger.FileFormatVersion)
                {
                    var text = $"Unsupported log file format. Latest supported version is {BinaryLogger.FileFormatVersion}, the log file has version {fileFormatVersion}.";
                    throw new NotSupportedException(text);
                }

                // Use a producer-consumer queue so that IO can happen on one thread
                // while processing can happen on another thread decoupled. The speed
                // up is from 4.65 to 4.15 seconds.
                var queue = new BlockingCollection<BuildEventArgs>();
                var processingTask = System.Threading.Tasks.Task.Run(() =>
                {
                    foreach (var args in queue.GetConsumingEnumerable())
                    {
                        Dispatch(args);
                    }
                });

                var reader = new BuildEventArgsReader(binaryReader, fileFormatVersion);
                reader.OnBlobRead += OnBlobRead;
                while (true)
                {
                    BuildEventArgs instance = null;

                    instance = reader.Read();
                    if (instance == null)
                    {
                        queue.CompleteAdding();
                        break;
                    }

                    queue.Add(instance);
                }

                processingTask.Wait();
            }
        }

        /// <summary>
        /// Enumerate over all records in the file. For each record store the bytes,
        /// the start position in the stream, length in bytes and the deserialized object.
        /// </summary>
        /// <remarks>Useful for debugging and analyzing binary logs</remarks>
        public IEnumerable<Record> ReadRecords(string sourceFilePath)
        {
            using (var stream = new FileStream(sourceFilePath, FileMode.Open))
            {
                var gzipStream = new GZipStream(stream, CompressionMode.Decompress, leaveOpen: true);
                var memoryStream = new MemoryStream();
                gzipStream.CopyTo(memoryStream);
                memoryStream.Position = 0;

                var binaryReader = new BinaryReader(memoryStream);
                var bytes = memoryStream.ToArray();

                int fileFormatVersion = binaryReader.ReadInt32();

                // the log file is written using a newer version of file format
                // that we don't know how to read
                if (fileFormatVersion > BinaryLogger.FileFormatVersion)
                {
                    var text = $"Unsupported log file format. Latest supported version is {BinaryLogger.FileFormatVersion}, the log file has version {fileFormatVersion}.";
                    throw new NotSupportedException(text);
                }

                long index = memoryStream.Position;
                long lengthOfBlobsAddedLastTime = 0;

                List<Record> blobs = new List<Record>();

                var reader = new BuildEventArgsReader(binaryReader, fileFormatVersion);
                reader.OnBlobRead += (kind, blob) =>
                {
                    var record = new Record
                    {
                        Bytes = blob,
                        Args = null,
                        Start = index,
                        Length = blob.Length
                    };

                    blobs.Add(record);
                    lengthOfBlobsAddedLastTime += blob.Length;
                };

                while (true)
                {
                    BuildEventArgs instance = null;

                    instance = reader.Read();
                    if (instance == null)
                    {
                        break;
                    }

                    var position = memoryStream.Position;
                    var length = position - index - lengthOfBlobsAddedLastTime;

                    var chunk = new byte[length];
                    Array.Copy(bytes, (int)(index + lengthOfBlobsAddedLastTime), chunk, 0, (int)length);
                    var record = new Record
                    {
                        Bytes = chunk,
                        Args = instance,
                        Start = index,
                        Length = length
                    };

                    yield return record;

                    index = position;
                    lengthOfBlobsAddedLastTime = 0;
                }

                foreach (var blob in blobs)
                {
                    yield return blob;
                }
            }
        }
    }
}

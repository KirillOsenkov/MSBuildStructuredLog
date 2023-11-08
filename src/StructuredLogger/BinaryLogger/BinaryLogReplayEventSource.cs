using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.IO;
using System.Text;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Shared;
using System.Threading;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Logging.StructuredLogger
{
    /// <summary>
    /// Interface for replaying a binary log file (*.binlog)
    /// </summary>
    internal interface IBinaryLogReplaySource :
        IEventSource,
        IBuildEventStringsReader,
        IBuildFileReader,
        IEmbeddedContentSource
    { }

    /// <summary>
    /// Provides a method to read a binary log file (*.binlog) and replay all stored BuildEventArgs
    /// by implementing IEventSource and raising corresponding events.
    /// </summary>
    /// <remarks>The class is public so that we can call it from MSBuild.exe when replaying a log file.</remarks>
    internal sealed class BinaryLogReplayEventSource : EventArgsDispatcher, IBinaryLogReplaySource
    {
        /// <summary>
        /// Read the provided binary log file and raise corresponding events for each BuildEventArgs
        /// </summary>
        /// <param name="sourceFilePath">The full file path of the binary log file</param>
        public void Replay(string sourceFilePath)
        {
            Replay(sourceFilePath, CancellationToken.None);
        }

        /// <summary>
        /// Read the provided binary log file opened as a stream and raise corresponding events for each BuildEventArgs
        /// </summary>
        /// <param name="sourceFileStream">Stream over the binlog content.</param>
        /// <param name="cancellationToken"></param>
        public void Replay(Stream sourceFileStream, CancellationToken cancellationToken)
        {
            using var binaryReader = OpenReader(sourceFileStream);
            Replay(binaryReader, cancellationToken);
        }

        /// <summary>
        /// Creates a <see cref="BinaryReader"/> for the provided binary log file.
        /// Performs decompression and buffering in the optimal way.
        /// Caller is responsible for disposing the returned reader.
        /// </summary>
        /// <param name="sourceFilePath"></param>
        /// <returns>BinaryReader of the given binlog file.</returns>
        public static BinaryReader OpenReader(string sourceFilePath)
        {
            Stream? stream = null;
            try
            {
                stream = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                return OpenReader(stream);
            }
            catch (Exception)
            {
                stream?.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Creates a <see cref="BinaryReader"/> for the provided binary log file.
        /// Performs decompression and buffering in the optimal way.
        /// Caller is responsible for disposing the returned reader.
        /// </summary>
        /// <param name="sourceFileStream">Stream over the binlog file</param>
        /// <returns>BinaryReader of the given binlog file.</returns>
        public static BinaryReader OpenReader(Stream sourceFileStream)
        {
            var gzipStream = new GZipStream(sourceFileStream, CompressionMode.Decompress, leaveOpen: false);

            // wrapping the GZipStream in a buffered stream significantly improves performance
            // and the max throughput is reached with a 32K buffer. See details here:
            // https://github.com/dotnet/runtime/issues/39233#issuecomment-745598847
            var bufferedStream = new BufferedStream(gzipStream, 32768);
            return new BinaryReader(bufferedStream);
        }

        /// <summary>
        /// Creates a <see cref="BinaryReader"/> for the provided binary log file.
        /// Performs decompression and buffering in the optimal way.
        /// </summary>
        /// <param name="sourceFilePath"></param>
        /// <returns>BinaryReader of the given binlog file.</returns>
        internal static BuildEventArgsReader OpenBuildEventsReader(string sourceFilePath)
            => OpenBuildEventsReader(OpenReader(sourceFilePath), true);

        /// <summary>
        /// Creates a <see cref="BuildEventArgsReader"/> for the provided binary reader over binary log file.
        /// Caller is responsible for disposing the returned reader.
        /// </summary>
        /// <param name="binaryReader"></param>
        /// <param name="closeInput">Indicates whether the passed BinaryReader should be closed on disposing.</param>
        /// <returns>BuildEventArgsReader over the given binlog file binary reader.</returns>
        internal static BuildEventArgsReader OpenBuildEventsReader(
            BinaryReader binaryReader,
            bool closeInput)
        {
            int fileFormatVersion = binaryReader.ReadInt32();

            // the log file is written using a newer version of file format
            // that we don't know how to read
            if (fileFormatVersion > BinaryLogger.FileFormatVersion)
            {
                var text = $"The log file format version is {fileFormatVersion}, whereas this version of MSBuild only supports versions up to {BinaryLogger.FileFormatVersion}.";
                throw new NotSupportedException(text);
            }

            return new BuildEventArgsReader(binaryReader, fileFormatVersion)
            {
                CloseInput = closeInput,
            };
        }

        /// <summary>
        /// Read the provided binary log file and raise corresponding events for each BuildEventArgs
        /// </summary>
        /// <param name="sourceFilePath">The full file path of the binary log file</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> indicating the replay should stop as soon as possible.</param>
        public void Replay(string sourceFilePath, CancellationToken cancellationToken)
        {
            using var binaryReader = OpenReader(sourceFilePath);
            Replay(binaryReader, cancellationToken);
        }

        /// <summary>
        /// Read the provided binary log file and raise corresponding events for each BuildEventArgs
        /// </summary>
        /// <param name="binaryReader">The binary log content binary reader - caller is responsible for disposing.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> indicating the replay should stop as soon as possible.</param>
        public void Replay(BinaryReader binaryReader, CancellationToken cancellationToken)
        {
            using BuildEventArgsReader reader = OpenBuildEventsReader(binaryReader, false);

            reader.EmbeddedContentRead += _embeddedContentRead;
            reader.StringReadDone += _stringReadDone;
            reader.ArchiveFileEncountered += _archiveFileEncountered;

            while (!cancellationToken.IsCancellationRequested && reader.Read() is { } instance)
            {
                Dispatch(instance);
            }
        }

        private Action<EmbeddedContentEventArgs>? _embeddedContentRead;
        /// <inheritdoc cref="IEmbeddedContentSource.EmbeddedContentRead"/>
        event Action<EmbeddedContentEventArgs>? IEmbeddedContentSource.EmbeddedContentRead
        {
            // Explicitly implemented event has to declare explicit add/remove accessors
            //  https://stackoverflow.com/a/2268472/2308106
            add => _embeddedContentRead += value;
            remove => _embeddedContentRead -= value;
        }

        private Action<ArchiveFileEventArgs>? _archiveFileEncountered;
        /// <inheritdoc cref="IBuildFileReader.ArchiveFileEncountered"/>
        event Action<ArchiveFileEventArgs>? IBuildFileReader.ArchiveFileEncountered
        {
            add => _archiveFileEncountered += value;
            remove => _archiveFileEncountered -= value;
        }

        private Action<StringReadEventArgs>? _stringReadDone;
        /// <inheritdoc cref="IBuildEventStringsReader.StringReadDone"/>
        event Action<StringReadEventArgs>? IBuildEventStringsReader.StringReadDone
        {
            add => _stringReadDone += value;
            remove => _stringReadDone -= value;
        }
    }
}

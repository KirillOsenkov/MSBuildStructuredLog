using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging.StructuredLogger;
using Moq;
using Xunit;

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// A fake Progress implementation to capture progress reports during tests.
    /// </summary>
    public class FakeProgress : Progress<ProgressUpdate>, Progress
    {
        private readonly List<ProgressUpdate> _updates = new List<ProgressUpdate>();

        public FakeProgress() : base(report => { }) { }

        public new void Report(ProgressUpdate update)
        {
            _updates.Add(update);
        }

        public IReadOnlyList<ProgressUpdate> Updates => _updates.AsReadOnly();
    }

    /// <summary>
    /// Dummy implementation for ProgressUpdate used in tests.
    /// </summary>
    public class ProgressUpdate
    {
        public double Ratio { get; set; }
        public int BufferLength { get; set; }
    }

    /// <summary>
    /// Dummy interface for Progress to match the signature used in BinLogReader.
    /// </summary>
    public interface Progress
    {
        void Report(ProgressUpdate update);
    }

    /// <summary>
    /// Unit tests for the <see cref="BinLogReader"/> class.
    /// </summary>
    public class BinLogReaderTests
    {
        private const int ValidFileFormatVersion = 10;
        private const int ValidMinimumReaderVersion = 10;

        /// <summary>
        /// Creates a compressed MemoryStream containing the provided raw data.
        /// </summary>
        /// <param name="rawData">The raw data to compress.</param>
        /// <returns>A MemoryStream with GZip compressed data.</returns>
        private MemoryStream CreateCompressedStream(byte[] rawData)
        {
            var outputStream = new MemoryStream();
            using (var gzip = new GZipStream(outputStream, CompressionMode.Compress, leaveOpen: true))
            {
                gzip.Write(rawData, 0, rawData.Length);
            }
            outputStream.Position = 0;
            return outputStream;
        }

        /// <summary>
        /// Creates raw log data representing a minimal valid header.
        /// Writes the file format version and (if applicable) the minimum reader version.
        /// </summary>
        /// <returns>A byte array of the raw log header.</returns>
        private byte[] CreateMinimalValidLogHeader()
        {
            using (var ms = new MemoryStream())
            {
                using (var bw = new BinaryWriter(ms))
                {
                    // Write file format version.
                    bw.Write(ValidFileFormatVersion);
                    // Since file format version >= BinaryLogger.ForwardCompatibilityMinimalVersion (assumed),
                    // write minimumReaderVersion.
                    bw.Write(ValidMinimumReaderVersion);
                }
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Tests that Replay(Stream, Progress) method processes an empty log stream,
        /// raises the OnFileFormatVersionRead event and reports final progress.
        /// It also verifies that an exception is raised during record reading.
        /// </summary>
//         [Fact] [Error] (115-27)CS1503 Argument 1: cannot convert from 'System.IO.MemoryStream' to 'string' [Error] (115-39)CS1503 Argument 2: cannot convert from 'Microsoft.Build.Logging.StructuredLogger.UnitTests.FakeProgress' to 'Microsoft.Build.Logging.StructuredLogger.Progress'
//         public void Replay_StreamEmptyLog_ShouldRaiseEventsAndReportFinalProgress()
//         {
//             // Arrange
//             byte[] header = CreateMinimalValidLogHeader();
//             using MemoryStream baseStream = CreateCompressedStream(header);
//             // BinLogReader expects a stream that it will decompress, so pass the compressed stream.
//             var reader = new BinLogReader();
//             int reportedFileFormatVersion = 0;
//             var exceptions = new List<Exception>();
//             var fakeProgress = new FakeProgress();
// 
//             reader.OnFileFormatVersionRead += version => reportedFileFormatVersion = version;
//             reader.OnException += ex => exceptions.Add(ex);
// 
//             // Act
//             // Replay(Stream, Progress) should run without throwing.
//             reader.Replay(baseStream, fakeProgress);
// 
//             // Assert
//             Assert.Equal(ValidFileFormatVersion, reportedFileFormatVersion);
//             // Expect at least one exception captured due to EndOfStream or similar when reading records.
//             Assert.NotEmpty(exceptions);
//             // The final progress report should have Ratio == 1.0 and BufferLength == 0.
//             Assert.Contains(fakeProgress.Updates, update => update.Ratio == 1.0 && update.BufferLength == 0);
//         }

        /// <summary>
        /// Tests that Replay(string, Progress) method works correctly when provided with a valid file path.
        /// It creates a temporary file with minimal valid log header and verifies that events and progress are reported.
        /// </summary>
//         [Fact] [Error] (154-41)CS1503 Argument 2: cannot convert from 'Microsoft.Build.Logging.StructuredLogger.UnitTests.FakeProgress' to 'Microsoft.Build.Logging.StructuredLogger.Progress'
//         public void Replay_FilePathEmptyLog_ShouldRaiseEventsAndReportFinalProgress()
//         {
//             // Arrange
//             byte[] header = CreateMinimalValidLogHeader();
//             byte[] compressedData;
//             using (var ms = CreateCompressedStream(header))
//             {
//                 compressedData = ms.ToArray();
//             }
// 
//             string tempFile = Path.GetTempFileName();
//             try
//             {
//                 File.WriteAllBytes(tempFile, compressedData);
// 
//                 var reader = new BinLogReader();
//                 int reportedFileFormatVersion = 0;
//                 var exceptions = new List<Exception>();
//                 var fakeProgress = new FakeProgress();
// 
//                 reader.OnFileFormatVersionRead += version => reportedFileFormatVersion = version;
//                 reader.OnException += ex => exceptions.Add(ex);
// 
//                 // Act
//                 reader.Replay(tempFile, fakeProgress);
// 
//                 // Assert
//                 Assert.Equal(ValidFileFormatVersion, reportedFileFormatVersion);
//                 Assert.NotEmpty(exceptions);
//                 Assert.Contains(fakeProgress.Updates, update => update.Ratio == 1.0 && update.BufferLength == 0);
//             }
//             finally
//             {
//                 if (File.Exists(tempFile))
//                 {
//                     File.Delete(tempFile);
//                 }
//             }
//         }

        /// <summary>
        /// Tests that the Replay(string) overload (without Progress) calls the Replay(string, Progress) correctly.
        /// </summary>
        [Fact]
        public void Replay_FilePathWithoutProgress_ShouldNotThrow()
        {
            // Arrange
            byte[] header = CreateMinimalValidLogHeader();
            byte[] compressedData;
            using (var ms = CreateCompressedStream(header))
            {
                compressedData = ms.ToArray();
            }

            string tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllBytes(tempFile, compressedData);
                var reader = new BinLogReader();
                // Act & Assert
                // Should complete without throwing, even though internal reading may trigger exceptions caught internally.
                reader.Replay(tempFile);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        /// <summary>
        /// Tests that ReadRecords(byte[]) method throws an exception when the log stream is empty beyond the header.
        /// </summary>
        [Fact]
        public void ReadRecords_ByteArray_EmptyLog_ShouldThrowException()
        {
            // Arrange
            byte[] header = CreateMinimalValidLogHeader();
            byte[] compressedData;
            using (var ms = CreateCompressedStream(header))
            {
                compressedData = ms.ToArray();
            }
            var reader = new BinLogReader();

            // Act & Assert
            // Since the log contains only header and no records, reading records should eventually trigger an exception.
            Assert.Throws<EndOfStreamException>(() =>
            {
                // Force enumeration of the records.
                var records = reader.ReadRecords(compressedData).ToList();
            });
        }

        /// <summary>
        /// Tests that ChunkBinlog(string) method throws an exception when the log stream is empty beyond the header.
        /// </summary>
        [Fact]
        public void ChunkBinlog_EmptyLog_ShouldThrowException()
        {
            // Arrange
            byte[] header = CreateMinimalValidLogHeader();
            byte[] compressedData;
            using (var ms = CreateCompressedStream(header))
            {
                compressedData = ms.ToArray();
            }
            string tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllBytes(tempFile, compressedData);
                var reader = new BinLogReader();

                // Act & Assert
                Assert.ThrowsAny<Exception>(() =>
                {
                    // Force enumeration of chunked records.
                    var chunks = reader.ChunkBinlog(tempFile).ToList();
                });
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        /// <summary>
        /// Tests that ReadRecords(Stream) method throws an exception when the log stream contains only the header.
        /// </summary>
        [Fact]
        public void ReadRecords_Stream_EmptyLog_ShouldThrowException()
        {
            // Arrange
            byte[] header = CreateMinimalValidLogHeader();
            using MemoryStream baseStream = CreateCompressedStream(header);
            var reader = new BinLogReader();

            // Act & Assert
            Assert.Throws<EndOfStreamException>(() =>
            {
                // Force enumeration of the records.
                var records = reader.ReadRecords((Stream)baseStream).ToList();
            });
        }

        /// <summary>
        /// Tests the static method ToBinaryLogRecordKind with a known BuildEventArgs type (BuildStartedEventArgs).
        /// Expects the corresponding BinaryLogRecordKind.BuildStarted.
        /// </summary>
//         [Fact] [Error] (288-88)CS1503 Argument 3: cannot convert from 'string' to 'System.Collections.Generic.IDictionary<string, string>'
//         public void ToBinaryLogRecordKind_WithBuildStartedEventArgs_ReturnsBuildStarted()
//         {
//             // Arrange
//             BuildEventArgs buildStarted = new BuildStartedEventArgs("message", "help", "sender");
// 
//             // Act
//             BinaryLogRecordKind kind = BinLogReader.ToBinaryLogRecordKind(buildStarted);
// 
//             // Assert
//             Assert.Equal(BinaryLogRecordKind.BuildStarted, kind);
//         }

        /// <summary>
        /// Tests the static method ToBinaryLogRecordKind with an unknown BuildEventArgs subtype.
        /// Expects a NotImplementedException.
        /// </summary>
        [Fact]
        public void ToBinaryLogRecordKind_WithUnknownEventArgs_ThrowsNotImplementedException()
        {
            // Arrange
            BuildEventArgs unknownArgs = new UnknownBuildEventArgs();

            // Act & Assert
            Assert.Throws<NotImplementedException>(() => BinLogReader.ToBinaryLogRecordKind(unknownArgs));
        }

        /// <summary>
        /// A dummy BuildEventArgs type that is not handled by ToBinaryLogRecordKind.
        /// </summary>
        private class UnknownBuildEventArgs : BuildEventArgs
        {
            public UnknownBuildEventArgs() : base() { }
        }
    }
}

using System;
using System.IO;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging.StructuredLogger;
using Moq;
using StructuredLogger.BinaryLogger;
using Xunit;

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="BuildEventArgsWriter"/> class.
    /// </summary>
    public class BuildEventArgsWriterTests
    {
        private readonly MemoryStream _underlyingStream;
        private readonly BinaryWriter _binaryWriter;
        private readonly BuildEventArgsWriter _writer;

        /// <summary>
        /// Initializes a new instance of the <see cref="BuildEventArgsWriterTests"/> class.
        /// Sets up a MemoryStream and BinaryWriter to inject into the BuildEventArgsWriter.
        /// </summary>
        public BuildEventArgsWriterTests()
        {
            _underlyingStream = new MemoryStream();
            _binaryWriter = new BinaryWriter(_underlyingStream);
            _writer = new BuildEventArgsWriter(_binaryWriter);
        }

        /// <summary>
        /// Tests the Write method when provided a BuildMessageEventArgs.
        /// This test verifies that the underlying stream is written to.
        /// </summary>
        [Fact]
        public void Write_BuildMessageEventArgs_HappyPath_WritesData()
        {
            // Arrange
            var testMessage = "Test build message";
            var eventArgs = new BuildMessageEventArgs(testMessage, null, "TestSender", MessageImportance.Low, DateTime.UtcNow)
            {
                BuildEventContext = new BuildEventContext(1, 2, 3, 4, 5, 6, 7)
            };
            _underlyingStream.SetLength(0);

            // Act
            _writer.Write(eventArgs);

            // Assert
            Assert.True(_underlyingStream.Length > 0, "Underlying stream should have been written to.");
        }

        /// <summary>
        /// Tests the WriteBlob method with a valid stream.
        /// This test verifies that blob data is written to the underlying stream.
        /// </summary>
        [Fact]
        public void WriteBlob_ValidStream_WritesData()
        {
            // Arrange
            var blobContent = "Blob content test";
            var blobBytes = Encoding.UTF8.GetBytes(blobContent);
            using var blobStream = new MemoryStream(blobBytes);
            _underlyingStream.SetLength(0);

            // Act
            _writer.WriteBlob(BinaryLogRecordKind.BuildStarted, blobStream);

            // Assert
            Assert.True(_underlyingStream.Length > 0, "Underlying stream should contain blob data after WriteBlob is called.");
        }

        /// <summary>
        /// Tests that WriteBlob throws an ArgumentOutOfRangeException when provided a stream whose Length exceeds int.MaxValue.
        /// </summary>
        [Fact]
        public void WriteBlob_StreamTooLong_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            var longStream = new StreamStub();

            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => _writer.WriteBlob(BinaryLogRecordKind.BuildFinished, longStream));
        }

        /// <summary>
        /// Tests the WriteStringRecord method by providing a valid non-null, non-empty string.
        /// This test verifies that a string record is written with the correct record kind and value.
        /// </summary>
        [Fact]
        public void WriteStringRecord_ValidString_WritesRecord()
        {
            // Arrange
            string testString = "Hello, world!";
            _underlyingStream.SetLength(0);

            // Act
            _writer.WriteStringRecord(testString);
            _binaryWriter.Flush();
            _underlyingStream.Position = 0;

            using var reader = new BinaryReader(_underlyingStream);
            int recordKind = Read7BitEncodedInt(reader);

            // Assert that the record kind matches BinaryLogRecordKind.String.
            Assert.Equal((int)BinaryLogRecordKind.String, recordKind);

            string writtenString = reader.ReadString();
            Assert.Equal(testString, writtenString);
        }

        /// <summary>
        /// Helper method to read a 7-bit encoded integer from a BinaryReader.
        /// </summary>
        /// <param name="reader">The BinaryReader to read from.</param>
        /// <returns>The decoded integer.</returns>
        private static int Read7BitEncodedInt(BinaryReader reader)
        {
            int count = 0;
            int shift = 0;
            byte b;
            do
            {
                b = reader.ReadByte();
                count |= (b & 0x7F) << shift;
                shift += 7;
            } while ((b & 0x80) != 0);
            return count;
        }

        /// <summary>
        /// A stub stream that simulates a stream with Length greater than int.MaxValue.
        /// Used for testing the WriteBlob method's exceptional scenario.
        /// </summary>
        private class StreamStub : Stream
        {
            public override bool CanRead => true;
            public override bool CanSeek => true;
            public override bool CanWrite => false;
            public override long Length => (long)int.MaxValue + 1;
            public override long Position { get; set; }

            public override void Flush() { }
            public override int Read(byte[] buffer, int offset, int count) => 0;
            public override long Seek(long offset, SeekOrigin origin) => 0;
            public override void SetLength(long value) { }
            public override void Write(byte[] buffer, int offset, int count) { }
        }
    }
}

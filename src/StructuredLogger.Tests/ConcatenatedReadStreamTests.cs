using DotUtils.StreamUtils;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DotUtils.StreamUtils.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="ConcatenatedReadStream"/> class.
    /// </summary>
    public class ConcatenatedReadStreamTests
    {
        /// <summary>
        /// A helper stream that simulates a non-readable stream.
        /// </summary>
        private class NonReadableStream : Stream
        {
            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => 0;
            public override long Position { get => 0; set { } }
            public override void Flush() { }
            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }

        /// <summary>
        /// Tests that the constructor accepts valid readable streams and concatenates them correctly.
        /// This test verifies that data from multiple streams is read in sequence.
        /// </summary>
        [Fact]
        public void Constructor_WithValidStreams_ReadsConcatenatedData()
        {
            // Arrange
            byte[] data1 = { 1, 2, 3 };
            byte[] data2 = { 4, 5, 6 };
            using var stream1 = new MemoryStream(data1);
            using var stream2 = new MemoryStream(data2);

            // Act
            using var concatenatedStream = new ConcatenatedReadStream(new[] { stream1, stream2 });
            byte[] buffer = new byte[6];
            int bytesRead = concatenatedStream.Read(buffer, 0, buffer.Length);

            // Assert
            Assert.Equal(6, bytesRead);
            Assert.Equal(data1.Concat(data2).ToArray(), buffer);
        }

        /// <summary>
        /// Tests that the constructor throws an ArgumentException when a non-readable stream is provided.
        /// </summary>
        [Fact]
        public void Constructor_WithNonReadableStream_ThrowsArgumentException()
        {
            // Arrange
            using var validStream = new MemoryStream(new byte[] { 1, 2 });
            var nonReadable = new NonReadableStream();

            // Act & Assert
            Assert.Throws<ArgumentException>(() => new ConcatenatedReadStream(validStream, nonReadable));
        }

        /// <summary>
        /// Tests that the constructor expands nested ConcatenatedReadStream instances correctly,
        /// ensuring that sub-streams from a nested instance are concatenated seamlessly.
        /// </summary>
        [Fact]
        public void Constructor_WithNestedConcatenatedReadStream_ExpandsStreams()
        {
            // Arrange
            byte[] data1 = { 7, 8 };
            byte[] data2 = { 9 };
            using var stream1 = new MemoryStream(data1);
            using var stream2 = new MemoryStream(data2);
            using var nestedStream = new ConcatenatedReadStream(stream1, stream2);
            byte[] data3 = { 10 };
            using var stream3 = new MemoryStream(data3);

            // Act
            using var concatenatedStream = new ConcatenatedReadStream(nestedStream, stream3);
            byte[] buffer = new byte[3];
            int bytesRead = concatenatedStream.Read(buffer, 0, buffer.Length);

            // Assert
            Assert.Equal(3, bytesRead);
            Assert.Equal(new byte[] { 7, 8, 9 }, buffer.Take(3).ToArray());
        }

        /// <summary>
        /// Tests that the Flush method throws a NotSupportedException as the stream is read-only.
        /// </summary>
        [Fact]
        public void Flush_ThrowsNotSupportedException()
        {
            // Arrange
            using var stream = new MemoryStream(new byte[] { 1 });
            using var concatenatedStream = new ConcatenatedReadStream(stream);

            // Act & Assert
            Assert.Throws<NotSupportedException>(() => concatenatedStream.Flush());
        }

        /// <summary>
        /// Tests that the Read method reads data sequentially across multiple streams.
        /// </summary>
        [Fact]
        public void Read_WithMultipleStreams_ReadsDataAcrossBoundaries()
        {
            // Arrange
            byte[] data1 = { 11, 12 };
            byte[] data2 = { 13, 14, 15 };
            using var stream1 = new MemoryStream(data1);
            using var stream2 = new MemoryStream(data2);
            using var concatenatedStream = new ConcatenatedReadStream(stream1, stream2);
            byte[] buffer = new byte[5];

            // Act
            int bytesRead = concatenatedStream.Read(buffer, 0, buffer.Length);

            // Assert
            Assert.Equal(5, bytesRead);
            Assert.Equal(data1.Concat(data2).ToArray(), buffer);
        }

        /// <summary>
        /// Tests that the Read method returns only the available bytes if the requested count exceeds the total data.
        /// </summary>
        [Fact]
        public void Read_WhenRequestExceedsAvailableData_ReturnsOnlyAvailableData()
        {
            // Arrange
            byte[] data = { 16, 17 };
            using var stream = new MemoryStream(data);
            using var concatenatedStream = new ConcatenatedReadStream(stream);
            byte[] buffer = new byte[10];

            // Act
            int bytesRead = concatenatedStream.Read(buffer, 0, buffer.Length);

            // Assert
            Assert.Equal(2, bytesRead);
            Assert.Equal(data, buffer.Take(2).ToArray());
        }

        /// <summary>
        /// Tests that the Position property is updated correctly after reading data.
        /// </summary>
        [Fact]
        public void Read_UpdatesPositionProperty()
        {
            // Arrange
            byte[] data = { 18, 19, 20 };
            using var stream = new MemoryStream(data);
            using var concatenatedStream = new ConcatenatedReadStream(stream);
            byte[] buffer = new byte[3];

            // Act
            int bytesRead = concatenatedStream.Read(buffer, 0, buffer.Length);
            long position = concatenatedStream.Position;

            // Assert
            Assert.Equal(3, bytesRead);
            Assert.Equal(3, position);
        }

        /// <summary>
        /// Tests that the ReadByte method reads one byte at a time and returns -1 after all data is read.
        /// </summary>
        [Fact]
        public void ReadByte_WithMultipleStreams_ReturnsCorrectByteAndMinusOneOnEnd()
        {
            // Arrange
            byte[] data1 = { 21 };
            byte[] data2 = { 22 };
            using var stream1 = new MemoryStream(data1);
            using var stream2 = new MemoryStream(data2);
            using var concatenatedStream = new ConcatenatedReadStream(stream1, stream2);

            // Act & Assert
            Assert.Equal(21, concatenatedStream.ReadByte());
            Assert.Equal(22, concatenatedStream.ReadByte());
            Assert.Equal(-1, concatenatedStream.ReadByte());
        }

        /// <summary>
        /// Tests that the ReadAsync method reads data asynchronously across multiple streams.
        /// </summary>
        [Fact]
        public async Task ReadAsync_WithMultipleStreams_ReadsDataAcrossBoundaries()
        {
            // Arrange
            byte[] data1 = { 23, 24 };
            byte[] data2 = { 25 };
            using var stream1 = new MemoryStream(data1);
            using var stream2 = new MemoryStream(data2);
            using var concatenatedStream = new ConcatenatedReadStream(stream1, stream2);
            byte[] buffer = new byte[3];

            // Act
            int bytesRead = await concatenatedStream.ReadAsync(buffer, 0, buffer.Length, CancellationToken.None);

            // Assert
            Assert.Equal(3, bytesRead);
            Assert.Equal(data1.Concat(data2).ToArray(), buffer);
        }

#if NET
        /// <summary>
        /// Tests that the Read(Span<byte>) method reads data correctly across multiple streams.
        /// </summary>
        [Fact]
        public void Read_Span_WithMultipleStreams_ReadsDataCorrectly()
        {
            // Arrange
            byte[] data1 = { 26, 27 };
            byte[] data2 = { 28, 29 };
            using var stream1 = new MemoryStream(data1);
            using var stream2 = new MemoryStream(data2);
            using var concatenatedStream = new ConcatenatedReadStream(stream1, stream2);
            byte[] buffer = new byte[4];

            // Act
            int bytesRead = concatenatedStream.Read(buffer);

            // Assert
            Assert.Equal(4, bytesRead);
            Assert.Equal(data1.Concat(data2).ToArray(), buffer);
        }

        /// <summary>
        /// Tests that the ReadAsync(Memory<byte>, CancellationToken) method reads data asynchronously across streams.
        /// </summary>
        [Fact]
        public async Task ReadAsync_Memory_WithMultipleStreams_ReadsDataCorrectly()
        {
            // Arrange
            byte[] data1 = { 30 };
            byte[] data2 = { 31, 32 };
            using var stream1 = new MemoryStream(data1);
            using var stream2 = new MemoryStream(data2);
            using var concatenatedStream = new ConcatenatedReadStream(stream1, stream2);
            byte[] buffer = new byte[3];
            Memory<byte> memoryBuffer = buffer;

            // Act
            int bytesRead = await concatenatedStream.ReadAsync(memoryBuffer, CancellationToken.None);

            // Assert
            Assert.Equal(3, bytesRead);
            Assert.Equal(data1.Concat(data2).ToArray(), buffer);
        }
#endif

        /// <summary>
        /// Tests that the Seek method throws a NotSupportedException.
        /// </summary>
        [Fact]
        public void Seek_ThrowsNotSupportedException()
        {
            // Arrange
            using var stream = new MemoryStream(new byte[] { 33 });
            using var concatenatedStream = new ConcatenatedReadStream(stream);

            // Act & Assert
            Assert.Throws<NotSupportedException>(() => concatenatedStream.Seek(0, SeekOrigin.Begin));
        }

        /// <summary>
        /// Tests that the SetLength method throws a NotSupportedException.
        /// </summary>
        [Fact]
        public void SetLength_ThrowsNotSupportedException()
        {
            // Arrange
            using var stream = new MemoryStream(new byte[] { 34 });
            using var concatenatedStream = new ConcatenatedReadStream(stream);

            // Act & Assert
            Assert.Throws<NotSupportedException>(() => concatenatedStream.SetLength(100));
        }

        /// <summary>
        /// Tests that the Write method throws a NotSupportedException.
        /// </summary>
        [Fact]
        public void Write_ThrowsNotSupportedException()
        {
            // Arrange
            using var stream = new MemoryStream(new byte[] { 35 });
            using var concatenatedStream = new ConcatenatedReadStream(stream);
            byte[] dummyData = { 36 };

            // Act & Assert
            Assert.Throws<NotSupportedException>(() => concatenatedStream.Write(dummyData, 0, dummyData.Length));
        }

        /// <summary>
        /// Tests that the stream properties return correct values.
        /// </summary>
        [Fact]
        public void Properties_ReturnCorrectValues()
        {
            // Arrange
            byte[] data = { 37, 38, 39 };
            using var stream = new MemoryStream(data);
            using var concatenatedStream = new ConcatenatedReadStream(stream);

            // Act & Assert
            Assert.True(concatenatedStream.CanRead);
            Assert.False(concatenatedStream.CanSeek);
            Assert.False(concatenatedStream.CanWrite);
            Assert.Equal(stream.Length, concatenatedStream.Length);
        }

        /// <summary>
        /// Tests that setting the Position property throws a NotSupportedException.
        /// </summary>
        [Fact]
        public void SetPosition_ThrowsNotSupportedException()
        {
            // Arrange
            using var stream = new MemoryStream(new byte[] { 40 });
            using var concatenatedStream = new ConcatenatedReadStream(stream);

            // Act & Assert
            Assert.Throws<NotSupportedException>(() => concatenatedStream.Position = 10);
        }
    }
}

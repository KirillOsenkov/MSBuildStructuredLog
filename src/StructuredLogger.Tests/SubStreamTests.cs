using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DotUtils.StreamUtils.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="DotUtils.StreamUtils.SubStream"/> class.
    /// </summary>
    public class SubStreamTests
    {
        // A custom stream that simulates a non-readable stream.
        private class NonReadableStream : Stream
        {
            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => 0;
            public override long Position { get => 0; set { } }

            public override void Flush()
            {
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }
        }

        // A custom stream to track Flush and FlushAsync calls.
        private class FlushTrackingStream : MemoryStream
        {
            public int FlushCallCount { get; private set; }
            public int FlushAsyncCallCount { get; private set; }

            public override void Flush()
            {
                FlushCallCount++;
                base.Flush();
            }

            public override Task FlushAsync(CancellationToken cancellationToken)
            {
                FlushAsyncCallCount++;
                return base.FlushAsync(cancellationToken);
            }
        }

        /// <summary>
        /// Tests that the constructor throws an <see cref="InvalidOperationException"/> when the underlying stream is not readable.
        /// </summary>
        [Fact]
        public void Constructor_NonReadableStream_ThrowsInvalidOperationException()
        {
            // Arrange
            var nonReadableStream = new NonReadableStream();

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => new DotUtils.StreamUtils.SubStream(nonReadableStream, 10));
        }

        /// <summary>
        /// Tests that the properties CanRead, CanSeek, CanWrite and Length return the expected values.
        /// </summary>
        [Fact]
        public void Properties_ReturnExpectedValues()
        {
            // Arrange
            byte[] data = new byte[] { 1, 2, 3, 4 };
            using var baseStream = new MemoryStream(data);
            var subStream = new DotUtils.StreamUtils.SubStream(baseStream, data.Length);

            // Act & Assert
            Assert.True(subStream.CanRead);
            Assert.False(subStream.CanSeek);
            Assert.False(subStream.CanWrite);
            Assert.Equal(data.Length, subStream.Length);
        }

        /// <summary>
        /// Tests that setting the Position property throws a <see cref="NotImplementedException"/>.
        /// </summary>
        [Fact]
        public void PositionSetter_ThrowsNotImplementedException()
        {
            // Arrange
            byte[] data = new byte[] { 10, 20, 30 };
            using var baseStream = new MemoryStream(data);
            var subStream = new DotUtils.StreamUtils.SubStream(baseStream, data.Length);

            // Act & Assert
            Assert.Throws<NotImplementedException>(() => subStream.Position = 1);
        }

        /// <summary>
        /// Tests that Flush calls the underlying stream's Flush method.
        /// </summary>
        [Fact]
        public void Flush_CallsUnderlyingStreamFlush()
        {
            // Arrange
            byte[] data = new byte[] { 5, 6, 7 };
            using var flushTrackingStream = new FlushTrackingStream();
            flushTrackingStream.Write(data, 0, data.Length);
            flushTrackingStream.Position = 0;
            var subStream = new DotUtils.StreamUtils.SubStream(flushTrackingStream, data.Length);

            // Act
            subStream.Flush();

            // Assert
            Assert.Equal(1, flushTrackingStream.FlushCallCount);
        }

        /// <summary>
        /// Tests that FlushAsync calls the underlying stream's FlushAsync method.
        /// </summary>
        [Fact]
        public async Task FlushAsync_CallsUnderlyingStreamFlushAsync()
        {
            // Arrange
            byte[] data = new byte[] { 8, 9, 10 };
            using var flushTrackingStream = new FlushTrackingStream();
            flushTrackingStream.Write(data, 0, data.Length);
            flushTrackingStream.Position = 0;
            var subStream = new DotUtils.StreamUtils.SubStream(flushTrackingStream, data.Length);
            var cancellationToken = CancellationToken.None;

            // Act
            await subStream.FlushAsync(cancellationToken);

            // Assert
            Assert.Equal(1, flushTrackingStream.FlushAsyncCallCount);
        }

        /// <summary>
        /// Tests that the Read method returns the correct number of bytes and advances the internal position.
        /// </summary>
        [Fact]
        public void Read_WhenCalled_ReturnsExpectedBytesAndAdvancesPosition()
        {
            // Arrange
            byte[] data = new byte[] { 11, 12, 13, 14, 15 };
            using var baseStream = new MemoryStream(data);
            // Limit the substream length to 3 bytes.
            var subStream = new DotUtils.StreamUtils.SubStream(baseStream, 3);
            byte[] buffer = new byte[10];

            // Act
            int bytesRead = subStream.Read(buffer, 0, buffer.Length);

            // Assert
            Assert.Equal(3, bytesRead);
            Assert.Equal(11, buffer[0]);
            Assert.Equal(12, buffer[1]);
            Assert.Equal(13, buffer[2]);
            Assert.True(subStream.IsAtEnd);
        }

        /// <summary>
        /// Tests that the Read method does not read beyond its defined length even if count is larger.
        /// </summary>
        [Fact]
        public void Read_WhenCountExceedsLimit_ReturnsOnlyAvailableBytes()
        {
            // Arrange
            byte[] data = new byte[] { 21, 22, 23, 24 };
            using var baseStream = new MemoryStream(data);
            // Limit the substream length to 2 bytes.
            var subStream = new DotUtils.StreamUtils.SubStream(baseStream, 2);
            byte[] buffer = new byte[10];

            // Act
            int bytesRead = subStream.Read(buffer, 0, buffer.Length);

            // Assert
            Assert.Equal(2, bytesRead);
            Assert.Equal(21, buffer[0]);
            Assert.Equal(22, buffer[1]);
            Assert.True(subStream.IsAtEnd);
        }

        /// <summary>
        /// Tests that the ReadByte method returns a byte correctly and returns -1 when no more data is available.
        /// </summary>
        [Fact]
        public void ReadByte_WhenCalled_ReturnsByteAndAdvancesPosition_AndMinusOneAtEnd()
        {
            // Arrange
            byte[] data = new byte[] { 31, 32 };
            using var baseStream = new MemoryStream(data);
            var subStream = new DotUtils.StreamUtils.SubStream(baseStream, data.Length);

            // Act & Assert
            int firstByte = subStream.ReadByte();
            Assert.Equal(31, firstByte);

            int secondByte = subStream.ReadByte();
            Assert.Equal(32, secondByte);

            int endByte = subStream.ReadByte();
            Assert.Equal(-1, endByte);
            Assert.True(subStream.IsAtEnd);
        }

        /// <summary>
        /// Tests that the ReadAsync method returns the correct number of bytes and advances the internal position.
        /// </summary>
        [Fact]
        public async Task ReadAsync_WhenCalled_ReturnsExpectedBytesAndAdvancesPosition()
        {
            // Arrange
            byte[] data = new byte[] { 41, 42, 43, 44 };
            using var baseStream = new MemoryStream(data);
            // Limit the substream length to 3 bytes.
            var subStream = new DotUtils.StreamUtils.SubStream(baseStream, 3);
            byte[] buffer = new byte[10];
            var cancellationToken = CancellationToken.None;

            // Act
            int bytesRead = await subStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);

            // Assert
            Assert.Equal(3, bytesRead);
            Assert.Equal(41, buffer[0]);
            Assert.Equal(42, buffer[1]);
            Assert.Equal(43, buffer[2]);
            Assert.True(subStream.IsAtEnd);
        }

#if NET
        /// <summary>
        /// Tests that the Read(Span{byte}) method returns the correct number of bytes and advances the internal position.
        /// </summary>
        [Fact]
        public void ReadSpan_WhenCalled_ReturnsExpectedBytesAndAdvancesPosition()
        {
            // Arrange
            byte[] data = new byte[] { 51, 52, 53, 54 };
            using var baseStream = new MemoryStream(data);
            // Limit the substream length to 3 bytes.
            var subStream = new DotUtils.StreamUtils.SubStream(baseStream, 3);
            Span<byte> buffer = new byte[10];

            // Act
            int bytesRead = subStream.Read(buffer);

            // Assert
            Assert.Equal(3, bytesRead);
            Assert.Equal(51, buffer[0]);
            Assert.Equal(52, buffer[1]);
            Assert.Equal(53, buffer[2]);
            Assert.True(subStream.IsAtEnd);
        }

        /// <summary>
        /// Tests that the ReadAsync(Memory{byte}) method returns the correct number of bytes and advances the internal position.
        /// </summary>
        [Fact]
        public async Task ReadMemoryAsync_WhenCalled_ReturnsExpectedBytesAndAdvancesPosition()
        {
            // Arrange
            byte[] data = new byte[] { 61, 62, 63, 64 };
            using var baseStream = new MemoryStream(data);
            // Limit the substream length to 2 bytes.
            var subStream = new DotUtils.StreamUtils.SubStream(baseStream, 2);
            Memory<byte> buffer = new byte[10];
            var cancellationToken = CancellationToken.None;

            // Act
            int bytesRead = await subStream.ReadAsync(buffer, cancellationToken);

            // Assert
            Assert.Equal(2, bytesRead);
            Assert.Equal(61, buffer.Span[0]);
            Assert.Equal(62, buffer.Span[1]);
            Assert.True(subStream.IsAtEnd);
        }
#endif

        /// <summary>
        /// Tests that the Seek method throws a <see cref="NotImplementedException"/>.
        /// </summary>
        [Fact]
        public void Seek_ThrowsNotImplementedException()
        {
            // Arrange
            byte[] data = new byte[] { 71, 72, 73 };
            using var baseStream = new MemoryStream(data);
            var subStream = new DotUtils.StreamUtils.SubStream(baseStream, data.Length);

            // Act & Assert
            Assert.Throws<NotImplementedException>(() => subStream.Seek(0, SeekOrigin.Begin));
        }

        /// <summary>
        /// Tests that the SetLength method throws a <see cref="NotImplementedException"/>.
        /// </summary>
        [Fact]
        public void SetLength_ThrowsNotImplementedException()
        {
            // Arrange
            byte[] data = new byte[] { 81, 82, 83 };
            using var baseStream = new MemoryStream(data);
            var subStream = new DotUtils.StreamUtils.SubStream(baseStream, data.Length);

            // Act & Assert
            Assert.Throws<NotImplementedException>(() => subStream.SetLength(10));
        }

        /// <summary>
        /// Tests that the Write method throws a <see cref="NotImplementedException"/>.
        /// </summary>
        [Fact]
        public void Write_ThrowsNotImplementedException()
        {
            // Arrange
            byte[] data = new byte[] { 91, 92, 93 };
            using var baseStream = new MemoryStream(data);
            var subStream = new DotUtils.StreamUtils.SubStream(baseStream, data.Length);
            byte[] writeBuffer = new byte[5];

            // Act & Assert
            Assert.Throws<NotImplementedException>(() => subStream.Write(writeBuffer, 0, writeBuffer.Length));
        }
    }
}

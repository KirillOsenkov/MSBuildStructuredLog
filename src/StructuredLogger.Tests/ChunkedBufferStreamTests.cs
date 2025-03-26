using Moq;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DotUtils.StreamUtils.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="ChunkedBufferStream"/> class.
    /// </summary>
    public class ChunkedBufferStreamTests
    {
        private const int DefaultBufferSize = 4;

        /// <summary>
        /// A helper stream to determine if Close was called.
        /// </summary>
        private class TestStream : MemoryStream
        {
            public bool IsClosed { get; private set; } = false;

            public override void Close()
            {
                IsClosed = true;
                base.Close();
            }
        }

        /// <summary>
        /// Tests that Flush writes any buffered data to the underlying stream.
        /// </summary>
        [Fact]
        public void Flush_WithBufferedData_WritesBufferedDataToUnderlyingStream()
        {
            // Arrange
            var underlyingStream = new MemoryStream();
            var chunkStream = new ChunkedBufferStream(underlyingStream, DefaultBufferSize);
            byte[] inputData = new byte[] { 1, 2 }; // less than buffer capacity
            chunkStream.Write(inputData, 0, inputData.Length);

            // Act
            chunkStream.Flush();

            // Assert
            byte[] result = underlyingStream.ToArray();
            Assert.Equal(inputData, result);
        }

        /// <summary>
        /// Tests that Write does not automatically flush if the buffer is not full.
        /// </summary>
        [Fact]
        public void Write_PartialBuffer_DoesNotFlushUntilFlushCall()
        {
            // Arrange
            var underlyingStream = new MemoryStream();
            var chunkStream = new ChunkedBufferStream(underlyingStream, DefaultBufferSize);
            byte[] inputData = new byte[] { 10, 20 }; // less than DefaultBufferSize

            // Act
            chunkStream.Write(inputData, 0, inputData.Length);

            // Assert: Underlying stream should remain unchanged since Flush was not called and buffer is not full.
            Assert.Empty(underlyingStream.ToArray());

            // Act: Now flush and verify
            chunkStream.Flush();
            Assert.Equal(inputData, underlyingStream.ToArray());
        }

        /// <summary>
        /// Tests that Write automatically flushes when the buffer gets exactly full.
        /// </summary>
        [Fact]
        public void Write_WithExactBufferFill_AutomaticallyFlushesBuffer()
        {
            // Arrange
            var underlyingStream = new MemoryStream();
            var chunkStream = new ChunkedBufferStream(underlyingStream, DefaultBufferSize);
            byte[] inputData = new byte[] { 1, 2, 3, 4 }; // exactly DefaultBufferSize

            // Act
            chunkStream.Write(inputData, 0, inputData.Length);

            // Assert: Underlying stream should contain the data immediately.
            Assert.Equal(inputData, underlyingStream.ToArray());
        }

        /// <summary>
        /// Tests that Write correctly handles multiple chunks when input data spans over several buffer fills.
        /// </summary>
        [Fact]
        public void Write_MultipleChunks_WorksCorrectly()
        {
            // Arrange
            var underlyingStream = new MemoryStream();
            int bufferSize = 4;
            var chunkStream = new ChunkedBufferStream(underlyingStream, bufferSize);
            byte[] inputData = new byte[10];
            for (int i = 0; i < inputData.Length; i++)
            {
                inputData[i] = (byte)(i + 1);
            }

            // Act
            chunkStream.Write(inputData, 0, inputData.Length);
            // Flush remaining data in buffer.
            chunkStream.Flush();

            // Assert: Underlying stream should have all the data in order.
            Assert.Equal(inputData, underlyingStream.ToArray());
        }

        /// <summary>
        /// Tests that WriteByte buffers data and automatically flushes when the buffer is full.
        /// </summary>
        [Fact]
        public void WriteByte_WithBufferFill_AutomaticallyFlushesBuffer()
        {
            // Arrange
            var underlyingStream = new MemoryStream();
            var chunkStream = new ChunkedBufferStream(underlyingStream, DefaultBufferSize);

            // Act
            for (int i = 1; i <= DefaultBufferSize; i++)
            {
                chunkStream.WriteByte((byte)i);
            }

            // Assert: The underlying stream should have been flushed automatically.
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, underlyingStream.ToArray());
        }

        /// <summary>
        /// Tests that WriteByte does not flush if the buffer is not full.
        /// </summary>
        [Fact]
        public void WriteByte_WithPartialBuffer_DoesNotFlushUntilFlushCall()
        {
            // Arrange
            var underlyingStream = new MemoryStream();
            var chunkStream = new ChunkedBufferStream(underlyingStream, DefaultBufferSize);

            // Act
            chunkStream.WriteByte(100);
            
            // Assert: Underlying stream should still be empty.
            Assert.Empty(underlyingStream.ToArray());

            // Act: Flush and verify.
            chunkStream.Flush();
            Assert.Equal(new byte[] { 100 }, underlyingStream.ToArray());
        }

        /// <summary>
        /// Tests that WriteAsync automatically flushes the buffer when it becomes full.
        /// </summary>
        [Fact]
        public async Task WriteAsync_WithExactBufferFill_AutomaticallyFlushesBuffer()
        {
            // Arrange
            var underlyingStream = new MemoryStream();
            var chunkStream = new ChunkedBufferStream(underlyingStream, DefaultBufferSize);
            byte[] inputData = new byte[] { 5, 6, 7, 8 };

            // Act
            await chunkStream.WriteAsync(inputData, 0, inputData.Length, CancellationToken.None);

            // Assert: Data should have been written to underlying stream.
            Assert.Equal(inputData, underlyingStream.ToArray());
        }

        /// <summary>
        /// Tests that WriteAsync does not flush if the buffer is not full.
        /// </summary>
        [Fact]
        public async Task WriteAsync_WithPartialBuffer_DoesNotFlushUntilFlushCall()
        {
            // Arrange
            var underlyingStream = new MemoryStream();
            var chunkStream = new ChunkedBufferStream(underlyingStream, DefaultBufferSize);
            byte[] inputData = new byte[] { 9, 10 };

            // Act
            await chunkStream.WriteAsync(inputData, 0, inputData.Length, CancellationToken.None);

            // Assert: Underlying stream should still be empty since no full buffer flush occurred.
            Assert.Empty(underlyingStream.ToArray());

            // Act: Flush and verify.
            chunkStream.Flush();
            Assert.Equal(inputData, underlyingStream.ToArray());
        }

        /// <summary>
        /// Tests that WriteAsync correctly handles inputs spanning multiple chunks.
        /// </summary>
        [Fact]
        public async Task WriteAsync_MultipleChunks_WorksCorrectly()
        {
            // Arrange
            var underlyingStream = new MemoryStream();
            int bufferSize = 4;
            var chunkStream = new ChunkedBufferStream(underlyingStream, bufferSize);
            byte[] inputData = new byte[9];
            for (int i = 0; i < inputData.Length; i++)
            {
                inputData[i] = (byte)(i + 1);
            }

            // Act
            await chunkStream.WriteAsync(inputData, 0, inputData.Length, CancellationToken.None);
            chunkStream.Flush();

            // Assert: Underlying stream contains all the bytes.
            Assert.Equal(inputData, underlyingStream.ToArray());
        }

#if NET
        /// <summary>
        /// Tests that Write(ReadOnlySpan{byte}) correctly buffers data and flushes when necessary.
        /// </summary>
        [Fact]
        public void Write_ReadOnlySpan_WorksCorrectly()
        {
            // Arrange
            var underlyingStream = new MemoryStream();
            int bufferSize = 4;
            var chunkStream = new ChunkedBufferStream(underlyingStream, bufferSize);
            byte[] inputData = new byte[] { 11, 12, 13, 14, 15, 16 };

            // Act
            chunkStream.Write(inputData);
            // Flush remaining partial buffer.
            chunkStream.Flush();

            // Assert: Underlying stream should have the same data as inputData.
            Assert.Equal(inputData, underlyingStream.ToArray());
        }

        /// <summary>
        /// Tests that WriteAsync(ReadOnlyMemory{byte}) correctly buffers data and flushes when necessary.
        /// </summary>
        [Fact]
        public async Task WriteAsync_ReadOnlyMemory_WorksCorrectly()
        {
            // Arrange
            var underlyingStream = new MemoryStream();
            int bufferSize = 4;
            var chunkStream = new ChunkedBufferStream(underlyingStream, bufferSize);
            byte[] inputData = new byte[] { 21, 22, 23, 24, 25 };
            ReadOnlyMemory<byte> memoryBuffer = inputData;

            // Act
            await chunkStream.WriteAsync(memoryBuffer);
            chunkStream.Flush();

            // Assert: Underlying stream should have the same data as inputData.
            Assert.Equal(inputData, underlyingStream.ToArray());
        }
#endif

        /// <summary>
        /// Tests that Read method throws a NotSupportedException.
        /// </summary>
        [Fact]
        public void Read_Always_ThrowsNotSupportedException()
        {
            // Arrange
            var underlyingStream = new MemoryStream();
            var chunkStream = new ChunkedBufferStream(underlyingStream, DefaultBufferSize);
            byte[] buffer = new byte[10];

            // Act & Assert
            var exception = Assert.Throws<NotSupportedException>(() => chunkStream.Read(buffer, 0, buffer.Length));
            Assert.Equal("GreedyBufferedStream is write-only, append-only", exception.Message);
        }

        /// <summary>
        /// Tests that Seek method throws a NotSupportedException.
        /// </summary>
        [Fact]
        public void Seek_Always_ThrowsNotSupportedException()
        {
            // Arrange
            var underlyingStream = new MemoryStream();
            var chunkStream = new ChunkedBufferStream(underlyingStream, DefaultBufferSize);

            // Act & Assert
            var exception = Assert.Throws<NotSupportedException>(() => chunkStream.Seek(0, SeekOrigin.Begin));
            Assert.Equal("GreedyBufferedStream is write-only, append-only", exception.Message);
        }

        /// <summary>
        /// Tests that SetLength method throws a NotSupportedException.
        /// </summary>
        [Fact]
        public void SetLength_Always_ThrowsNotSupportedException()
        {
            // Arrange
            var underlyingStream = new MemoryStream();
            var chunkStream = new ChunkedBufferStream(underlyingStream, DefaultBufferSize);

            // Act & Assert
            var exception = Assert.Throws<NotSupportedException>(() => chunkStream.SetLength(100));
            Assert.Equal("GreedyBufferedStream is write-only, append-only", exception.Message);
        }

        /// <summary>
        /// Tests that setting the Position property throws a NotSupportedException.
        /// </summary>
        [Fact]
        public void PositionSetter_Always_ThrowsNotSupportedException()
        {
            // Arrange
            var underlyingStream = new MemoryStream();
            var chunkStream = new ChunkedBufferStream(underlyingStream, DefaultBufferSize);

            // Act & Assert
            var exception = Assert.Throws<NotSupportedException>(() => chunkStream.Position = 10);
            Assert.Equal("GreedyBufferedStream is write-only, append-only", exception.Message);
        }

        /// <summary>
        /// Tests that the CanRead, CanSeek, and CanWrite properties return the correct values.
        /// </summary>
        [Fact]
        public void Properties_CanReadCanSeekCanWrite_ReturnExpectedValues()
        {
            // Arrange
            var underlyingStream = new MemoryStream();
            var chunkStream = new ChunkedBufferStream(underlyingStream, DefaultBufferSize);

            // Act & Assert
            Assert.False(chunkStream.CanRead);
            Assert.False(chunkStream.CanSeek);
            Assert.True(chunkStream.CanWrite);
        }

        /// <summary>
        /// Tests that the Length property returns the sum of the underlying stream length and the current buffered data length.
        /// </summary>
        [Fact]
        public void LengthProperty_ReturnsUnderlyingStreamLengthPlusBufferPosition()
        {
            // Arrange
            var underlyingStream = new MemoryStream();
            var chunkStream = new ChunkedBufferStream(underlyingStream, DefaultBufferSize);
            byte[] inputData = new byte[] { 30, 31 }; // not enough to flush
            chunkStream.Write(inputData, 0, inputData.Length);

            // Act
            long lengthBeforeFlush = chunkStream.Length;
            chunkStream.Flush();
            long lengthAfterFlush = chunkStream.Length;

            // Assert
            // Before flush, underlying stream length is 0, so expected length equals inputData length.
            Assert.Equal(inputData.Length, lengthBeforeFlush);
            // After flush, underlying stream length should equal inputData length and buffer is empty.
            Assert.Equal(inputData.Length, lengthAfterFlush);
        }

        /// <summary>
        /// Tests that the Position getter returns the sum of the underlying stream position and the current buffered data length.
        /// </summary>
        [Fact]
        public void PositionGetter_ReturnsUnderlyingStreamPositionPlusBufferPosition()
        {
            // Arrange
            var underlyingStream = new MemoryStream();
            var chunkStream = new ChunkedBufferStream(underlyingStream, DefaultBufferSize);

            // Act & Assert
            // Initially, both underlying stream position and buffer position are 0.
            Assert.Equal(0, chunkStream.Position);

            byte[] inputData = new byte[] { 40, 41, 42 }; // partial buffer write
            chunkStream.Write(inputData, 0, inputData.Length);
            // Underlying stream remains unchanged until flush, so Position equals inputData.Length.
            Assert.Equal(inputData.Length, chunkStream.Position);

            chunkStream.Flush();
            // After flushing, underlying stream position should have advanced and buffer reset.
            Assert.Equal(underlyingStream.Position, chunkStream.Position);
        }

        /// <summary>
        /// Tests that Close calls Flush and then closes the underlying stream.
        /// </summary>
        [Fact]
        public void Close_CallsFlushAndUnderlyingStreamClose()
        {
            // Arrange
            var testStream = new TestStream();
            var chunkStream = new ChunkedBufferStream(testStream, DefaultBufferSize);
            byte[] inputData = new byte[] { 50, 51 };
            chunkStream.Write(inputData, 0, inputData.Length);

            // Act
            chunkStream.Close();

            // Assert
            Assert.True(testStream.IsClosed);
            // Verify that data was flushed.
            Assert.Equal(inputData, testStream.ToArray());
        }
    }
}

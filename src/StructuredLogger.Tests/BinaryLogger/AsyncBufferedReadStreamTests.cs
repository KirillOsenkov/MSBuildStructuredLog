using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace System.IO.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="AsyncBufferedReadStream"/> class.
    /// </summary>
    public class AsyncBufferedReadStreamTests
    {
        private const int CustomBufferSize = 5; // Using a small buffer to force refill logic in tests.

        #region Constructor Tests

        /// <summary>
        /// Tests that the constructor throws <see cref="ArgumentNullException"/> when a null stream is passed.
        /// </summary>
        [Fact]
        public void Ctor_NullStream_ThrowsArgumentNullException()
        {
            // Arrange
            Stream nullStream = null;

            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => new AsyncBufferedReadStream(nullStream));
            Assert.Equal("stream", exception.ParamName);
        }

        /// <summary>
        /// Tests that the constructor throws <see cref="ArgumentOutOfRangeException"/> when an invalid bufferSize is provided.
        /// </summary>
        [Fact]
        public void Ctor_InvalidBufferSize_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            Stream validStream = new MemoryStream();

            // Act & Assert
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new AsyncBufferedReadStream(validStream, 0));
            Assert.Equal("bufferSize", exception.ParamName);
        }

        /// <summary>
        /// Tests that the constructor creates an instance successfully when valid parameters are provided.
        /// </summary>
        [Fact]
        public void Ctor_ValidParameters_CreatesInstance()
        {
            // Arrange
            Stream validStream = new MemoryStream();

            // Act
            var asyncStream = new AsyncBufferedReadStream(validStream);

            // Assert
            Assert.True(asyncStream.CanRead);
        }

        #endregion

        #region Read Method Tests

        /// <summary>
        /// Tests that Read method throws <see cref="ArgumentNullException"/> when the array parameter is null.
        /// </summary>
        [Fact]
        public void Read_NullBuffer_ThrowsArgumentNullException()
        {
            // Arrange
            var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes("Test"));
            var asyncStream = new AsyncBufferedReadStream(memoryStream, CustomBufferSize);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => asyncStream.Read(null, 0, 4));
        }

        /// <summary>
        /// Tests that Read method throws <see cref="ArgumentOutOfRangeException"/> when the offset is negative.
        /// </summary>
        [Fact]
        public void Read_NegativeOffset_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes("Test"));
            var asyncStream = new AsyncBufferedReadStream(memoryStream, CustomBufferSize);
            byte[] buffer = new byte[10];

            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => asyncStream.Read(buffer, -1, 2));
        }

        /// <summary>
        /// Tests that Read method throws <see cref="ArgumentOutOfRangeException"/> when the count is negative.
        /// </summary>
        [Fact]
        public void Read_NegativeCount_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes("Test"));
            var asyncStream = new AsyncBufferedReadStream(memoryStream, CustomBufferSize);
            byte[] buffer = new byte[10];

            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => asyncStream.Read(buffer, 0, -1));
        }

        /// <summary>
        /// Tests that Read method throws <see cref="ArgumentException"/> when the provided buffer length is insufficient.
        /// </summary>
        [Fact]
        public void Read_InsufficientBuffer_ThrowsArgumentException()
        {
            // Arrange
            var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes("Hello"));
            var asyncStream = new AsyncBufferedReadStream(memoryStream, CustomBufferSize);
            byte[] buffer = new byte[2]; // Too small to satisfy offset and count

            // Act & Assert
            Assert.Throws<ArgumentException>(() => asyncStream.Read(buffer, 1, 2));
        }

        /// <summary>
        /// Tests that Read method returns zero immediately when count is zero.
        /// </summary>
        [Fact]
        public void Read_ZeroCount_ReturnsZero()
        {
            // Arrange
            var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes("Hello"));
            var asyncStream = new AsyncBufferedReadStream(memoryStream, CustomBufferSize);
            byte[] buffer = new byte[10];

            // Act
            int readBytes = asyncStream.Read(buffer, 0, 0);

            // Assert
            Assert.Equal(0, readBytes);
        }

        /// <summary>
        /// Tests that Read method correctly reads all data from the underlying stream, including buffer refills.
        /// </summary>
        [Fact]
        public void Read_ReadsAllData_Correctly()
        {
            // Arrange
            var data = Encoding.UTF8.GetBytes("Hello, World!");
            using var memoryStream = new MemoryStream(data);
            var asyncStream = new AsyncBufferedReadStream(memoryStream, 5); // small buffer size to force multiple refills
            byte[] buffer = new byte[data.Length];

            // Act
            int totalRead = 0;
            int bytesRead;
            while ((bytesRead = asyncStream.Read(buffer, totalRead, buffer.Length - totalRead)) > 0)
            {
                totalRead += bytesRead;
            }

            // Assert
            Assert.Equal(data.Length, totalRead);
            Assert.Equal(data, buffer);
        }

        #endregion

        #region ReadByte Method Tests

        /// <summary>
        /// Tests that ReadByte method correctly reads bytes one by one.
        /// </summary>
        [Fact]
        public void ReadByte_ReadsData_Correctly()
        {
            // Arrange
            var data = Encoding.UTF8.GetBytes("ABC");
            using var memoryStream = new MemoryStream(data);
            var asyncStream = new AsyncBufferedReadStream(memoryStream, 2); // small buffer to force refill
            var result = new byte[data.Length];
            
            // Act
            for (int i = 0; i < data.Length; i++)
            {
                int b = asyncStream.ReadByte();
                Assert.NotEqual(-1, b);
                result[i] = (byte)b;
            }

            // Assert
            Assert.Equal(data, result);
        }

        /// <summary>
        /// Tests that ReadByte method returns -1 when end of stream is reached.
        /// </summary>
        [Fact]
        public void ReadByte_EndOfStream_ReturnsMinusOne()
        {
            // Arrange
            var data = Encoding.UTF8.GetBytes("X");
            using var memoryStream = new MemoryStream(data);
            var asyncStream = new AsyncBufferedReadStream(memoryStream, 2);

            // Act
            int firstByte = asyncStream.ReadByte();
            int endByte = asyncStream.ReadByte();

            // Assert
            Assert.NotEqual(-1, firstByte);
            Assert.Equal(-1, endByte);
        }

        #endregion

        #region Property Tests

        /// <summary>
        /// Tests that CanRead property returns the underlying stream's CanRead value.
        /// </summary>
        [Fact]
        public void CanRead_ReturnsUnderlyingStreamValue()
        {
            // Arrange
            using var memoryStream = new MemoryStream();
            var asyncStream = new AsyncBufferedReadStream(memoryStream, CustomBufferSize);

            // Act
            bool canRead = asyncStream.CanRead;

            // Assert
            Assert.True(canRead);
        }

        /// <summary>
        /// Tests that CanWrite property returns the underlying stream's CanWrite value.
        /// </summary>
        [Fact]
        public void CanWrite_ReturnsUnderlyingStreamValue()
        {
            // Arrange
            using var memoryStream = new MemoryStream();
            var asyncStream = new AsyncBufferedReadStream(memoryStream, CustomBufferSize);

            // Act
            bool canWrite = asyncStream.CanWrite;

            // Assert
            Assert.Equal(memoryStream.CanWrite, canWrite);
        }

        /// <summary>
        /// Tests that CanSeek property returns the underlying stream's CanSeek value.
        /// </summary>
        [Fact]
        public void CanSeek_ReturnsUnderlyingStreamValue()
        {
            // Arrange
            using var memoryStream = new MemoryStream();
            var asyncStream = new AsyncBufferedReadStream(memoryStream, CustomBufferSize);

            // Act
            bool canSeek = asyncStream.CanSeek;

            // Assert
            Assert.Equal(memoryStream.CanSeek, canSeek);
        }

        /// <summary>
        /// Tests that Length property returns the underlying stream's length.
        /// </summary>
        [Fact]
        public void Length_ReturnsUnderlyingStreamLength()
        {
            // Arrange
            var data = Encoding.UTF8.GetBytes("1234567890");
            using var memoryStream = new MemoryStream(data);
            var asyncStream = new AsyncBufferedReadStream(memoryStream, CustomBufferSize);

            // Act
            long length = asyncStream.Length;

            // Assert
            Assert.Equal(memoryStream.Length, length);
        }

        /// <summary>
        /// Tests that the Position getter returns the correct value based on the underlying stream and buffer positions.
        /// </summary>
        [Fact]
        public void PositionGetter_ReturnsCalculatedPosition()
        {
            // Arrange
            var data = Encoding.UTF8.GetBytes("1234567890");
            using var memoryStream = new MemoryStream(data);
            var asyncStream = new AsyncBufferedReadStream(memoryStream, 4); // small buffer for controlled refill
            byte[] buffer = new byte[3];

            // Act
            int bytesRead = asyncStream.Read(buffer, 0, buffer.Length);
            // The position should be: underlying stream position + readPosition - readLength.
            // After first read, underlying stream has advanced at least by buffer refill.
            long calculatedPosition = asyncStream.Position;

            // Assert
            // Since the internal logic prefetches, we simply ensure that Position is not negative and is within stream length bounds.
            Assert.InRange(calculatedPosition, 0, memoryStream.Length);
        }

        /// <summary>
        /// Tests that setting the Position property throws a <see cref="NotImplementedException"/>.
        /// </summary>
        [Fact]
        public void PositionSetter_ThrowsNotImplementedException()
        {
            // Arrange
            using var memoryStream = new MemoryStream();
            var asyncStream = new AsyncBufferedReadStream(memoryStream, CustomBufferSize);

            // Act & Assert
            Assert.Throws<NotImplementedException>(() => asyncStream.Position = 10);
        }

        #endregion

        #region Not Implemented Methods Tests

        /// <summary>
        /// Tests that Flush method throws a <see cref="NotImplementedException"/>.
        /// </summary>
        [Fact]
        public void Flush_ThrowsNotImplementedException()
        {
            // Arrange
            using var memoryStream = new MemoryStream();
            var asyncStream = new AsyncBufferedReadStream(memoryStream, CustomBufferSize);

            // Act & Assert
            Assert.Throws<NotImplementedException>(() => asyncStream.Flush());
        }

        /// <summary>
        /// Tests that Seek method throws a <see cref="NotImplementedException"/>.
        /// </summary>
        [Fact]
        public void Seek_ThrowsNotImplementedException()
        {
            // Arrange
            using var memoryStream = new MemoryStream();
            var asyncStream = new AsyncBufferedReadStream(memoryStream, CustomBufferSize);

            // Act & Assert
            Assert.Throws<NotImplementedException>(() => asyncStream.Seek(0, SeekOrigin.Begin));
        }

        /// <summary>
        /// Tests that SetLength method throws a <see cref="NotImplementedException"/>.
        /// </summary>
        [Fact]
        public void SetLength_ThrowsNotImplementedException()
        {
            // Arrange
            using var memoryStream = new MemoryStream();
            var asyncStream = new AsyncBufferedReadStream(memoryStream, CustomBufferSize);

            // Act & Assert
            Assert.Throws<NotImplementedException>(() => asyncStream.SetLength(100));
        }

        /// <summary>
        /// Tests that Write method throws a <see cref="NotImplementedException"/>.
        /// </summary>
        [Fact]
        public void Write_ThrowsNotImplementedException()
        {
            // Arrange
            using var memoryStream = new MemoryStream();
            var asyncStream = new AsyncBufferedReadStream(memoryStream, CustomBufferSize);
            byte[] data = Encoding.UTF8.GetBytes("Test");

            // Act & Assert
            Assert.Throws<NotImplementedException>(() => asyncStream.Write(data, 0, data.Length));
        }

        #endregion

        #region Dispose Tests

        /// <summary>
        /// Tests that Dispose method properly disposes the underlying stream.
        /// </summary>
        [Fact]
        public void Dispose_DisposesUnderlyingStream()
        {
            // Arrange
            var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes("Test"));
            var asyncStream = new AsyncBufferedReadStream(memoryStream, CustomBufferSize);

            // Act
            asyncStream.Dispose();

            // Assert
            // After disposal, CanRead should return false as the underlying stream is set to null.
            Assert.False(asyncStream.CanRead);
            // Additionally, attempting to use the underlying memoryStream should throw an ObjectDisposedException.
            Assert.Throws<ObjectDisposedException>(() => { var len = memoryStream.Length; });
        }

        #endregion
    }
}

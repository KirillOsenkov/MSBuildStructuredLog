using Microsoft.Build.Logging.StructuredLogger;
using Moq;
using System;
using System.IO;
using Xunit;

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="BetterBinaryWriter"/> class.
    /// </summary>
    public class BetterBinaryWriterTests
    {
        /// <summary>
        /// Tests that the constructor of BetterBinaryWriter does not throw an exception when provided a valid stream.
        /// </summary>
        [Fact]
        public void Constructor_ValidStream_ShouldNotThrow()
        {
            // Arrange
            using var memoryStream = new MemoryStream();

            // Act
            BetterBinaryWriter writer = new BetterBinaryWriter(memoryStream);

            // Assert
            Assert.NotNull(writer);
        }

        /// <summary>
        /// Tests that the constructor of BetterBinaryWriter throws an ArgumentNullException when provided a null stream.
        /// </summary>
        [Fact]
        public void Constructor_NullStream_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new BetterBinaryWriter(null));
        }

        /// <summary>
        /// Tests that calling Write with a value of 0 writes a single zero byte to the underlying stream.
        /// </summary>
        [Fact]
        public void Write_ZeroValue_WritesExpectedBytes()
        {
            // Arrange
            using var memoryStream = new MemoryStream();
            using var writer = new BetterBinaryWriter(memoryStream);
            byte[] expectedBytes = new byte[] { 0x00 };

            // Act
            writer.Write(0);
            writer.Flush();
            var actualBytes = memoryStream.ToArray();

            // Assert
            Assert.Equal(expectedBytes, actualBytes);
        }

        /// <summary>
        /// Tests that calling Write with a positive value (128) writes the expected 7-bit encoded bytes to the underlying stream.
        /// Expected encoding for 128 is { 0x80, 0x01 }.
        /// </summary>
        [Fact]
        public void Write_PositiveValue_WritesExpectedBytes()
        {
            // Arrange
            using var memoryStream = new MemoryStream();
            using var writer = new BetterBinaryWriter(memoryStream);
            int testValue = 128;
            byte[] expectedBytes = new byte[] { 0x80, 0x01 };

            // Act
            writer.Write(testValue);
            writer.Flush();
            var actualBytes = memoryStream.ToArray();

            // Assert
            Assert.Equal(expectedBytes, actualBytes);
        }

        /// <summary>
        /// Tests that calling Write with a negative value (-1) writes the expected 7-bit encoded bytes to the underlying stream.
        /// For -1, the expected encoding is { 0xFF, 0xFF, 0xFF, 0xFF, 0x0F }.
        /// </summary>
        [Fact]
        public void Write_NegativeValue_WritesExpectedBytes()
        {
            // Arrange
            using var memoryStream = new MemoryStream();
            using var writer = new BetterBinaryWriter(memoryStream);
            int testValue = -1;
            byte[] expectedBytes = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x0F };

            // Act
            writer.Write(testValue);
            writer.Flush();
            var actualBytes = memoryStream.ToArray();

            // Assert
            Assert.Equal(expectedBytes, actualBytes);
        }
    }
}

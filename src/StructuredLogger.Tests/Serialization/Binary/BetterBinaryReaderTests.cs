using System;
using System.IO;
using Microsoft.Build.Logging.StructuredLogger;
using Xunit;

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="BetterBinaryReader"/> class.
    /// </summary>
    public class BetterBinaryReaderTests
    {
        /// <summary>
        /// Tests that the constructor throws an ArgumentNullException when passed a null stream.
        /// This verifies that a valid stream is required upon instantiation.
        /// </summary>
        [Fact]
        public void Constructor_NullStream_ThrowsArgumentNullException()
        {
            // Arrange
            Stream nullStream = null;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new BetterBinaryReader(nullStream));
        }

        /// <summary>
        /// Tests that ReadInt32 returns the correct integer value when the underlying stream contains a valid one-byte 7-bit encoded integer.
        /// The test arranges a MemoryStream with a single byte value less than 128 and expects the same value to be returned.
        /// </summary>
        [Fact]
        public void ReadInt32_WithOneByteEncodedValue_ReturnsCorrectInteger()
        {
            // Arrange
            int expectedValue = 123;
            // One-byte 7-bit encoded integer (value is 123 because it's less than 128)
            byte[] encodedValue = new byte[] { 123 };
            using var memoryStream = new MemoryStream(encodedValue);
            using var reader = new BetterBinaryReader(memoryStream);

            // Act
            int actualValue = reader.ReadInt32();

            // Assert
            Assert.Equal(expectedValue, actualValue);
        }

        /// <summary>
        /// Tests that ReadInt32 returns the correct integer value when the underlying stream contains a valid multi-byte 7-bit encoded integer.
        /// The test uses the number 300 which is encoded in two bytes.
        /// </summary>
        [Fact]
        public void ReadInt32_WithMultiByteEncodedValue_ReturnsCorrectInteger()
        {
            // Arrange
            int expectedValue = 300;
            // 7-bit encoding for 300: first byte = 0xAC (172) with continuation bit, second byte = 0x02.
            byte[] encodedValue = new byte[] { 0xAC, 0x02 };
            using var memoryStream = new MemoryStream(encodedValue);
            using var reader = new BetterBinaryReader(memoryStream);

            // Act
            int actualValue = reader.ReadInt32();

            // Assert
            Assert.Equal(expectedValue, actualValue);
        }

        /// <summary>
        /// Tests that ReadInt32 throws an EndOfStreamException when the stream terminates unexpectedly,
        /// i.e. when the 7-bit encoded integer is incomplete.
        /// </summary>
        [Fact]
        public void ReadInt32_WithIncompleteEncoding_ThrowsEndOfStreamException()
        {
            // Arrange
            // Incomplete encoding: single byte with continuation bit set (e.g., 0xAC), but no subsequent byte provided.
            byte[] incompleteEncodedValue = new byte[] { 0xAC };
            using var memoryStream = new MemoryStream(incompleteEncodedValue);
            using var reader = new BetterBinaryReader(memoryStream);

            // Act & Assert
            Assert.Throws<EndOfStreamException>(() => reader.ReadInt32());
        }
    }
}

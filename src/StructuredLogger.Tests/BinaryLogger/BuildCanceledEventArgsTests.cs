using System;
using StructuredLogger.BinaryLogger;
using Xunit;

namespace StructuredLogger.BinaryLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="BuildCanceledEventArgs"/> class.
    /// </summary>
    public class BuildCanceledEventArgsTests
    {
        /// <summary>
        /// Tests that the two-parameter constructor creates an instance successfully when provided with a valid non-empty message.
        /// </summary>
        [Fact]
        public void Constructor_TwoParameters_ValidMessage_CreatesInstance()
        {
            // Arrange
            string validMessage = "Build canceled due to error.";
            DateTime eventTimestamp = DateTime.UtcNow;

            // Act
            var instance = new BuildCanceledEventArgs(validMessage, eventTimestamp);

            // Assert
            Assert.NotNull(instance);
            Assert.Equal(validMessage, instance.Message);
            Assert.Equal(eventTimestamp, instance.Timestamp);
        }

        /// <summary>
        /// Tests that the two-parameter constructor throws an ArgumentException when provided with a null message.
        /// </summary>
        [Fact]
        public void Constructor_TwoParameters_NullMessage_ThrowsArgumentException()
        {
            // Arrange
            string? invalidMessage = null;
            DateTime eventTimestamp = DateTime.UtcNow;

            // Act & Assert
            ArgumentException exception = Assert.Throws<ArgumentException>(
                () => new BuildCanceledEventArgs(invalidMessage!, eventTimestamp));
            Assert.Equal("Message cannot be null or consist only white-space characters.", exception.Message);
        }

        /// <summary>
        /// Tests that the two-parameter constructor throws an ArgumentException when provided with an empty message.
        /// </summary>
        [Fact]
        public void Constructor_TwoParameters_EmptyMessage_ThrowsArgumentException()
        {
            // Arrange
            string invalidMessage = "";
            DateTime eventTimestamp = DateTime.UtcNow;

            // Act & Assert
            ArgumentException exception = Assert.Throws<ArgumentException>(
                () => new BuildCanceledEventArgs(invalidMessage, eventTimestamp));
            Assert.Equal("Message cannot be null or consist only white-space characters.", exception.Message);
        }

        /// <summary>
        /// Tests that the two-parameter constructor throws an ArgumentException when provided with a whitespace message.
        /// </summary>
        [Fact]
        public void Constructor_TwoParameters_WhitespaceMessage_ThrowsArgumentException()
        {
            // Arrange
            string invalidMessage = "   ";
            DateTime eventTimestamp = DateTime.UtcNow;

            // Act & Assert
            ArgumentException exception = Assert.Throws<ArgumentException>(
                () => new BuildCanceledEventArgs(invalidMessage, eventTimestamp));
            Assert.Equal("Message cannot be null or consist only white-space characters.", exception.Message);
        }

        /// <summary>
        /// Tests that the three-parameter constructor creates an instance successfully when provided with a valid non-empty message and message arguments (which can be null).
        /// </summary>
        [Fact]
        public void Constructor_ThreeParameters_ValidMessageWithNullArgs_CreatesInstance()
        {
            // Arrange
            string validMessage = "Build canceled due to dependency failure.";
            DateTime eventTimestamp = DateTime.UtcNow;
            object[]? messageArgs = null;

            // Act
            var instance = new BuildCanceledEventArgs(validMessage, eventTimestamp, messageArgs);

            // Assert
            Assert.NotNull(instance);
            Assert.Equal(validMessage, instance.Message);
            Assert.Equal(eventTimestamp, instance.Timestamp);
        }

        /// <summary>
        /// Tests that the three-parameter constructor creates an instance successfully when provided with a valid non-empty message and non-null message arguments.
        /// </summary>
        [Fact]
        public void Constructor_ThreeParameters_ValidMessageWithArgs_CreatesInstance()
        {
            // Arrange
            string validMessage = "Build canceled because of {0} error.";
            DateTime eventTimestamp = DateTime.UtcNow;
            object[] messageArgs = new object[] { "critical" };

            // Act
            var instance = new BuildCanceledEventArgs(validMessage, eventTimestamp, messageArgs);

            // Assert
            Assert.NotNull(instance);
            Assert.Equal(validMessage, instance.Message);
            Assert.Equal(eventTimestamp, instance.Timestamp);
        }

        /// <summary>
        /// Tests that the three-parameter constructor throws an ArgumentException when provided with a null message.
        /// </summary>
        [Fact]
        public void Constructor_ThreeParameters_NullMessage_ThrowsArgumentException()
        {
            // Arrange
            string? invalidMessage = null;
            DateTime eventTimestamp = DateTime.UtcNow;
            object[] messageArgs = new object[] { "argument" };

            // Act & Assert
            ArgumentException exception = Assert.Throws<ArgumentException>(
                () => new BuildCanceledEventArgs(invalidMessage!, eventTimestamp, messageArgs));
            Assert.Equal("Message cannot be null or consist only white-space characters.", exception.Message);
        }

        /// <summary>
        /// Tests that the three-parameter constructor throws an ArgumentException when provided with an empty message.
        /// </summary>
        [Fact]
        public void Constructor_ThreeParameters_EmptyMessage_ThrowsArgumentException()
        {
            // Arrange
            string invalidMessage = "";
            DateTime eventTimestamp = DateTime.UtcNow;
            object[] messageArgs = new object[] { "argument" };

            // Act & Assert
            ArgumentException exception = Assert.Throws<ArgumentException>(
                () => new BuildCanceledEventArgs(invalidMessage, eventTimestamp, messageArgs));
            Assert.Equal("Message cannot be null or consist only white-space characters.", exception.Message);
        }

        /// <summary>
        /// Tests that the three-parameter constructor throws an ArgumentException when provided with a whitespace message.
        /// </summary>
        [Fact]
        public void Constructor_ThreeParameters_WhitespaceMessage_ThrowsArgumentException()
        {
            // Arrange
            string invalidMessage = "   ";
            DateTime eventTimestamp = DateTime.UtcNow;
            object[] messageArgs = new object[] { "argument" };

            // Act & Assert
            ArgumentException exception = Assert.Throws<ArgumentException>(
                () => new BuildCanceledEventArgs(invalidMessage, eventTimestamp, messageArgs));
            Assert.Equal("Message cannot be null or consist only white-space characters.", exception.Message);
        }
    }
}

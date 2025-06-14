using System;
using Microsoft.Build.Logging.StructuredLogger;
using Moq;
using Xunit;

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="StreamChunkOverReadException"/> class.
    /// </summary>
    public class StreamChunkOverReadExceptionTests
    {
        /// <summary>
        /// Tests that the parameterless constructor of <see cref="StreamChunkOverReadException"/> creates an instance with a default message and a null inner exception.
        /// </summary>
        [Fact]
        public void Constructor_Parameterless_ShouldHaveDefaultMessageAndNullInnerException()
        {
            // Arrange & Act
            var exception = new StreamChunkOverReadException();

            // Assert
            Assert.IsType<StreamChunkOverReadException>(exception);
            Assert.NotNull(exception.Message);
            Assert.Null(exception.InnerException);
        }

        /// <summary>
        /// Tests that the constructor of <see cref="StreamChunkOverReadException"/> which takes a message assigns the message correctly.
        /// </summary>
        [Theory]
        [InlineData("Test error message")]
        [InlineData("")]
        [InlineData(null)]
        public void Constructor_WithMessage_ShouldSetMessage(string message)
        {
            // Act
            var exception = new StreamChunkOverReadException(message);

            // Assert
            // When a null message is provided, Exception.Message may be set to a system-supplied message.
            if (message == null)
            {
                Assert.NotNull(exception.Message);
                Assert.NotEqual("null", exception.Message);
            }
            else
            {
                Assert.Equal(message, exception.Message);
            }
            Assert.Null(exception.InnerException);
        }

        /// <summary>
        /// Tests that the constructor of <see cref="StreamChunkOverReadException"/> which takes a message and inner exception sets both properties correctly.
        /// </summary>
        [Theory]
        [InlineData("Error occurred", "Inner error message")]
        [InlineData("", "")]
        [InlineData(null, null)]
        public void Constructor_WithMessageAndInnerException_ShouldSetProperties(string message, string innerErrorMessage)
        {
            // Arrange
            Exception innerException = innerErrorMessage != null ? new Exception(innerErrorMessage) : null;

            // Act
            var exception = new StreamChunkOverReadException(message, innerException);

            // Assert
            if (message == null)
            {
                Assert.NotNull(exception.Message);
                Assert.NotEqual("null", exception.Message);
            }
            else
            {
                Assert.Equal(message, exception.Message);
            }
            Assert.Equal(innerException, exception.InnerException);
        }
    }
}

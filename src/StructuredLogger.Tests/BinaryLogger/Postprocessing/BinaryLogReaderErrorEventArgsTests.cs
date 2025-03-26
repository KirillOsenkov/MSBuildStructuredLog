using System;
using Microsoft.Build.Logging.StructuredLogger;
using Xunit;

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="BinaryLogReaderErrorEventArgs"/> class.
    /// </summary>
    public class BinaryLogReaderErrorEventArgsTests
    {
        /// <summary>
        /// Tests that GetFormattedMessage returns the expected error message 
        /// as provided by the FormatErrorMessage delegate.
        /// </summary>
        [Fact]
        public void GetFormattedMessage_WhenCalled_ReturnsCorrectMessage()
        {
            // Arrange
            const string expectedMessage = "Formatted error message";
            FormatErrorMessage formatErrorMessage = () => expectedMessage;
            var errorType = (ReaderErrorType)1;
            var recordKind = (BinaryLogRecordKind)1;
            var errorEventArgs = new BinaryLogReaderErrorEventArgs(errorType, recordKind, formatErrorMessage);

            // Act
            string actualMessage = errorEventArgs.GetFormattedMessage();

            // Assert
            Assert.Equal(expectedMessage, actualMessage);
        }

        /// <summary>
        /// Tests that GetFormattedMessage returns null when the FormatErrorMessage delegate returns null.
        /// </summary>
        [Fact]
        public void GetFormattedMessage_WhenDelegateReturnsNull_ReturnsNull()
        {
            // Arrange
            FormatErrorMessage formatErrorMessage = () => null;
            var errorType = (ReaderErrorType)0;
            var recordKind = (BinaryLogRecordKind)0;
            var errorEventArgs = new BinaryLogReaderErrorEventArgs(errorType, recordKind, formatErrorMessage);

            // Act
            string actualMessage = errorEventArgs.GetFormattedMessage();

            // Assert
            Assert.Null(actualMessage);
        }

        /// <summary>
        /// Tests that the properties ErrorType and RecordKind return the values
        /// that were provided during construction.
        /// </summary>
        [Fact]
        public void Properties_WhenConstructed_ReturnConstructorValues()
        {
            // Arrange
            FormatErrorMessage formatErrorMessage = () => "Any message";
            var expectedErrorType = (ReaderErrorType)2;
            var expectedRecordKind = (BinaryLogRecordKind)3;
            var errorEventArgs = new BinaryLogReaderErrorEventArgs(expectedErrorType, expectedRecordKind, formatErrorMessage);

            // Act
            var actualErrorType = errorEventArgs.ErrorType;
            var actualRecordKind = errorEventArgs.RecordKind;

            // Assert
            Assert.Equal(expectedErrorType, actualErrorType);
            Assert.Equal(expectedRecordKind, actualRecordKind);
        }
    }
}

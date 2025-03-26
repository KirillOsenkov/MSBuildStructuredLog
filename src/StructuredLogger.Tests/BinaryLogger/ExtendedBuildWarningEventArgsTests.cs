using System;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.Build.Framework.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="ExtendedBuildWarningEventArgs"/> class.
    /// </summary>
    public class ExtendedBuildWarningEventArgsTests
    {
        /// <summary>
        /// Tests that the default (internal) constructor sets ExtendedType to "undefined" and leaves ExtendedMetadata and ExtendedData as null.
        /// </summary>
        [Fact]
        public void DefaultConstructor_SetsExtendedTypeToUndefined()
        {
            // Arrange & Act
            var eventArgs = new ExtendedBuildWarningEventArgs();

            // Assert
            Assert.Equal("undefined", eventArgs.ExtendedType);
            Assert.Null(eventArgs.ExtendedMetadata);
            Assert.Null(eventArgs.ExtendedData);
        }

        /// <summary>
        /// Tests that the constructor with a single type parameter sets ExtendedType correctly.
        /// </summary>
        [Theory]
        [InlineData("CustomType")]
        [InlineData("")]
        public void Constructor_WithType_SetsExtendedType(string inputType)
        {
            // Arrange & Act
            var eventArgs = new ExtendedBuildWarningEventArgs(inputType);

            // Assert
            Assert.Equal(inputType, eventArgs.ExtendedType);
        }

        /// <summary>
        /// Tests that the constructor with extended build event parameters (without timestamp) sets ExtendedType correctly.
        /// </summary>
        [Fact]
        public void Constructor_WithBasicParameters_SetsExtendedType()
        {
            // Arrange
            string expectedType = "BasicType";
            string subcategory = "SubCat";
            string code = "Code1";
            string file = "File.cs";
            int lineNumber = 10;
            int columnNumber = 5;
            int endLineNumber = 10;
            int endColumnNumber = 15;
            string message = "Warning message";
            string helpKeyword = "HelpKey";
            string senderName = "Sender";

            // Act
            var eventArgs = new ExtendedBuildWarningEventArgs(
                expectedType,
                subcategory,
                code,
                file,
                lineNumber,
                columnNumber,
                endLineNumber,
                endColumnNumber,
                message,
                helpKeyword,
                senderName);

            // Assert
            Assert.Equal(expectedType, eventArgs.ExtendedType);
        }

        /// <summary>
        /// Tests that the constructor with extended parameters including a timestamp sets ExtendedType correctly.
        /// </summary>
        [Fact]
        public void Constructor_WithTimestamp_SetsExtendedType()
        {
            // Arrange
            string expectedType = "TimestampType";
            string subcategory = "SubCat";
            string code = "Code2";
            string file = "File2.cs";
            int lineNumber = 20;
            int columnNumber = 10;
            int endLineNumber = 20;
            int endColumnNumber = 20;
            string message = "Warning with timestamp";
            string helpKeyword = "HelpKey2";
            string senderName = "Sender2";
            DateTime timestamp = DateTime.UtcNow;

            // Act
            var eventArgs = new ExtendedBuildWarningEventArgs(
                expectedType,
                subcategory,
                code,
                file,
                lineNumber,
                columnNumber,
                endLineNumber,
                endColumnNumber,
                message,
                helpKeyword,
                senderName,
                timestamp);

            // Assert
            Assert.Equal(expectedType, eventArgs.ExtendedType);
        }

        /// <summary>
        /// Tests that the constructor with extended parameters including a timestamp and message arguments sets ExtendedType correctly.
        /// </summary>
        [Fact]
        public void Constructor_WithTimestampAndMessageArgs_SetsExtendedType()
        {
            // Arrange
            string expectedType = "MessageArgsType";
            string subcategory = "SubCat";
            string code = "Code3";
            string file = "File3.cs";
            int lineNumber = 30;
            int columnNumber = 15;
            int endLineNumber = 30;
            int endColumnNumber = 25;
            string message = "Warning with args";
            string helpKeyword = "HelpKey3";
            string senderName = "Sender3";
            DateTime timestamp = DateTime.UtcNow;
            object[] messageArgs = { "Arg1", 42 };

            // Act
            var eventArgs = new ExtendedBuildWarningEventArgs(
                expectedType,
                subcategory,
                code,
                file,
                lineNumber,
                columnNumber,
                endLineNumber,
                endColumnNumber,
                message,
                helpKeyword,
                senderName,
                timestamp,
                messageArgs);

            // Assert
            Assert.Equal(expectedType, eventArgs.ExtendedType);
        }

        /// <summary>
        /// Tests that the constructor with extended parameters including a helpLink, timestamp, and message arguments sets ExtendedType correctly.
        /// </summary>
        [Fact]
        public void Constructor_WithHelpLinkTimestampAndMessageArgs_SetsExtendedType()
        {
            // Arrange
            string expectedType = "HelpLinkType";
            string subcategory = "SubCat";
            string code = "Code4";
            string file = "File4.cs";
            int lineNumber = 40;
            int columnNumber = 20;
            int endLineNumber = 40;
            int endColumnNumber = 30;
            string message = "Warning with help link";
            string helpKeyword = "HelpKey4";
            string senderName = "Sender4";
            string helpLink = "http://example.com/help";
            DateTime timestamp = DateTime.UtcNow;
            object[] messageArgs = { "ArgA", "ArgB" };

            // Act
            var eventArgs = new ExtendedBuildWarningEventArgs(
                expectedType,
                subcategory,
                code,
                file,
                lineNumber,
                columnNumber,
                endLineNumber,
                endColumnNumber,
                message,
                helpKeyword,
                senderName,
                helpLink,
                timestamp,
                messageArgs);

            // Assert
            Assert.Equal(expectedType, eventArgs.ExtendedType);
        }

        /// <summary>
        /// Tests that the ExtendedMetadata and ExtendedData properties can be set and retrieved.
        /// </summary>
        [Fact]
        public void ExtendedProperties_SetAndGet_WorkCorrectly()
        {
            // Arrange
            var eventArgs = new ExtendedBuildWarningEventArgs("TestType");
            var metadata = new Dictionary<string, string?>() { { "key", "value" } };
            string extendedData = "Extended information";

            // Act
            eventArgs.ExtendedMetadata = metadata;
            eventArgs.ExtendedData = extendedData;

            // Assert
            Assert.Equal("TestType", eventArgs.ExtendedType);
            Assert.Equal(metadata, eventArgs.ExtendedMetadata);
            Assert.Equal(extendedData, eventArgs.ExtendedData);
        }
    }
}

using System;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.Build.Framework.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="ExtendedBuildErrorEventArgs"/> class.
    /// </summary>
    public class ExtendedBuildErrorEventArgsTests
    {
        /// <summary>
        /// Tests that the internal default constructor initializes ExtendedType to "undefined" and leaves extended properties as null.
        /// </summary>
        [Fact]
        public void DefaultConstructor_ShouldSetExtendedTypeToUndefined()
        {
            // Arrange & Act
            var eventArgs = new ExtendedBuildErrorEventArgs();

            // Assert
            Assert.Equal("undefined", eventArgs.ExtendedType);
            Assert.Null(eventArgs.ExtendedMetadata);
            Assert.Null(eventArgs.ExtendedData);
        }

        /// <summary>
        /// Tests that the simple constructor with type parameter sets ExtendedType correctly.
        /// </summary>
        [Fact]
        public void Constructor_WithType_ShouldSetExtendedType()
        {
            // Arrange
            string expectedType = "TestType";

            // Act
            var eventArgs = new ExtendedBuildErrorEventArgs(expectedType);

            // Assert
            Assert.Equal(expectedType, eventArgs.ExtendedType);
            Assert.Null(eventArgs.ExtendedMetadata);
            Assert.Null(eventArgs.ExtendedData);
        }

        /// <summary>
        /// Tests the constructor with all event data parameters (without timestamp) to ensure base and extended properties are set correctly.
        /// </summary>
        [Fact]
        public void Constructor_WithAllParametersWithoutTimestamp_ShouldSetPropertiesCorrectly()
        {
            // Arrange
            string expectedType = "ErrorType";
            string expectedSubcategory = "SubCat";
            string expectedCode = "ERR001";
            string expectedFile = "file.cs";
            int expectedLineNumber = 10;
            int expectedColumnNumber = 20;
            int expectedEndLineNumber = 30;
            int expectedEndColumnNumber = 40;
            string expectedMessage = "An error occurred.";
            string expectedHelpKeyword = "HelpKeyword";
            string expectedSenderName = "Sender";

            // Act
            var eventArgs = new ExtendedBuildErrorEventArgs(
                expectedType,
                expectedSubcategory,
                expectedCode,
                expectedFile,
                expectedLineNumber,
                expectedColumnNumber,
                expectedEndLineNumber,
                expectedEndColumnNumber,
                expectedMessage,
                expectedHelpKeyword,
                expectedSenderName);

            // Assert
            Assert.Equal(expectedType, eventArgs.ExtendedType);
            // Verify base properties (assuming BuildErrorEventArgs exposes them as public properties)
            Assert.Equal(expectedSubcategory, eventArgs.Subcategory);
            Assert.Equal(expectedCode, eventArgs.Code);
            Assert.Equal(expectedFile, eventArgs.File);
            Assert.Equal(expectedLineNumber, eventArgs.LineNumber);
            Assert.Equal(expectedColumnNumber, eventArgs.ColumnNumber);
            Assert.Equal(expectedEndLineNumber, eventArgs.EndLineNumber);
            Assert.Equal(expectedEndColumnNumber, eventArgs.EndColumnNumber);
            Assert.Equal(expectedMessage, eventArgs.Message);
            Assert.Equal(expectedHelpKeyword, eventArgs.HelpKeyword);
            Assert.Equal(expectedSenderName, eventArgs.SenderName);
        }

        /// <summary>
        /// Tests the constructor that accepts a timestamp (without message arguments) to ensure the timestamp and other properties are set correctly.
        /// </summary>
        [Fact]
        public void Constructor_WithTimestamp_ShouldSetTimestampAndPropertiesCorrectly()
        {
            // Arrange
            string expectedType = "ErrorTypeWithTimestamp";
            string expectedSubcategory = "SubCatTS";
            string expectedCode = "ERR002";
            string expectedFile = "file_ts.cs";
            int expectedLineNumber = 15;
            int expectedColumnNumber = 25;
            int expectedEndLineNumber = 35;
            int expectedEndColumnNumber = 45;
            string expectedMessage = "Timestamp error message.";
            string expectedHelpKeyword = "HelpTS";
            string expectedSenderName = "SenderTS";
            DateTime expectedTimestamp = DateTime.Now;

            // Act
            var eventArgs = new ExtendedBuildErrorEventArgs(
                expectedType,
                expectedSubcategory,
                expectedCode,
                expectedFile,
                expectedLineNumber,
                expectedColumnNumber,
                expectedEndLineNumber,
                expectedEndColumnNumber,
                expectedMessage,
                expectedHelpKeyword,
                expectedSenderName,
                expectedTimestamp);

            // Assert
            Assert.Equal(expectedType, eventArgs.ExtendedType);
            Assert.Equal(expectedSubcategory, eventArgs.Subcategory);
            Assert.Equal(expectedCode, eventArgs.Code);
            Assert.Equal(expectedFile, eventArgs.File);
            Assert.Equal(expectedLineNumber, eventArgs.LineNumber);
            Assert.Equal(expectedColumnNumber, eventArgs.ColumnNumber);
            Assert.Equal(expectedEndLineNumber, eventArgs.EndLineNumber);
            Assert.Equal(expectedEndColumnNumber, eventArgs.EndColumnNumber);
            Assert.Equal(expectedMessage, eventArgs.Message);
            Assert.Equal(expectedHelpKeyword, eventArgs.HelpKeyword);
            Assert.Equal(expectedSenderName, eventArgs.SenderName);
            Assert.Equal(expectedTimestamp, eventArgs.Timestamp);
        }

        /// <summary>
        /// Tests the constructor that accepts a timestamp and message arguments to ensure properties are set correctly.
        /// </summary>
        [Fact]
        public void Constructor_WithTimestampAndMessageArgs_ShouldSetPropertiesCorrectly()
        {
            // Arrange
            string expectedType = "ErrorTypeWithArgs";
            string expectedSubcategory = "SubCatArgs";
            string expectedCode = "ERR003";
            string expectedFile = "file_args.cs";
            int expectedLineNumber = 5;
            int expectedColumnNumber = 10;
            int expectedEndLineNumber = 15;
            int expectedEndColumnNumber = 20;
            string expectedMessage = "Error with arguments: {0}, {1}";
            string expectedHelpKeyword = "HelpArgs";
            string expectedSenderName = "SenderArgs";
            DateTime expectedTimestamp = DateTime.UtcNow;
            object[] messageArgs = new object[] { "Arg1", 123 };

            // Act
            var eventArgs = new ExtendedBuildErrorEventArgs(
                expectedType,
                expectedSubcategory,
                expectedCode,
                expectedFile,
                expectedLineNumber,
                expectedColumnNumber,
                expectedEndLineNumber,
                expectedEndColumnNumber,
                expectedMessage,
                expectedHelpKeyword,
                expectedSenderName,
                expectedTimestamp,
                messageArgs);

            // Assert
            Assert.Equal(expectedType, eventArgs.ExtendedType);
            Assert.Equal(expectedSubcategory, eventArgs.Subcategory);
            Assert.Equal(expectedCode, eventArgs.Code);
            Assert.Equal(expectedFile, eventArgs.File);
            Assert.Equal(expectedLineNumber, eventArgs.LineNumber);
            Assert.Equal(expectedColumnNumber, eventArgs.ColumnNumber);
            Assert.Equal(expectedEndLineNumber, eventArgs.EndLineNumber);
            Assert.Equal(expectedEndColumnNumber, eventArgs.EndColumnNumber);
            Assert.Equal(expectedMessage, eventArgs.Message);
            Assert.Equal(expectedHelpKeyword, eventArgs.HelpKeyword);
            Assert.Equal(expectedSenderName, eventArgs.SenderName);
            Assert.Equal(expectedTimestamp, eventArgs.Timestamp);
            // Message arguments are not directly exposed; the test ensures no exceptions and correct property assignments.
        }

        /// <summary>
        /// Tests the constructor that accepts a help link in addition to timestamp and message arguments to ensure all properties are set correctly.
        /// </summary>
        [Fact]
        public void Constructor_WithHelpLinkTimestampAndMessageArgs_ShouldSetPropertiesCorrectly()
        {
            // Arrange
            string expectedType = "ErrorTypeFull";
            string expectedSubcategory = "SubCatFull";
            string expectedCode = "ERR004";
            string expectedFile = "file_full.cs";
            int expectedLineNumber = 1;
            int expectedColumnNumber = 2;
            int expectedEndLineNumber = 3;
            int expectedEndColumnNumber = 4;
            string expectedMessage = "Full error message: {0}";
            string expectedHelpKeyword = "HelpFull";
            string expectedSenderName = "SenderFull";
            string expectedHelpLink = "http://help.link";
            DateTime expectedTimestamp = DateTime.Today;
            object[] messageArgs = new object[] { "Detail" };

            // Act
            var eventArgs = new ExtendedBuildErrorEventArgs(
                expectedType,
                expectedSubcategory,
                expectedCode,
                expectedFile,
                expectedLineNumber,
                expectedColumnNumber,
                expectedEndLineNumber,
                expectedEndColumnNumber,
                expectedMessage,
                expectedHelpKeyword,
                expectedSenderName,
                expectedHelpLink,
                expectedTimestamp,
                messageArgs);

            // Assert
            Assert.Equal(expectedType, eventArgs.ExtendedType);
            Assert.Equal(expectedSubcategory, eventArgs.Subcategory);
            Assert.Equal(expectedCode, eventArgs.Code);
            Assert.Equal(expectedFile, eventArgs.File);
            Assert.Equal(expectedLineNumber, eventArgs.LineNumber);
            Assert.Equal(expectedColumnNumber, eventArgs.ColumnNumber);
            Assert.Equal(expectedEndLineNumber, eventArgs.EndLineNumber);
            Assert.Equal(expectedEndColumnNumber, eventArgs.EndColumnNumber);
            Assert.Equal(expectedMessage, eventArgs.Message);
            Assert.Equal(expectedHelpKeyword, eventArgs.HelpKeyword);
            Assert.Equal(expectedSenderName, eventArgs.SenderName);
            Assert.Equal(expectedHelpLink, eventArgs.HelpLink);
            Assert.Equal(expectedTimestamp, eventArgs.Timestamp);
            // Message arguments are not directly exposed; this test verifies that the constructor sets up the instance without error.
        }

        /// <summary>
        /// Tests that the extended properties ExtendedMetadata and ExtendedData can be set and retrieved.
        /// </summary>
        [Fact]
        public void ExtendedProperties_SetAndGet_ShouldWorkCorrectly()
        {
            // Arrange
            var eventArgs = new ExtendedBuildErrorEventArgs("TestType");
            var metadata = new Dictionary<string, string?> 
            {
                { "Key1", "Value1" },
                { "Key2", null }
            };
            string extendedDataValue = "Some extended data";

            // Act
            eventArgs.ExtendedMetadata = metadata;
            eventArgs.ExtendedData = extendedDataValue;

            // Assert
            Assert.Equal(metadata, eventArgs.ExtendedMetadata);
            Assert.Equal(extendedDataValue, eventArgs.ExtendedData);
        }
    }
}

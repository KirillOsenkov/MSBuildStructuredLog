using System;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.Build.Framework.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="ExtendedBuildMessageEventArgs"/> class.
    /// </summary>
    public class ExtendedBuildMessageEventArgsTests
    {
        /// <summary>
        /// Tests that the internal default constructor initializes ExtendedType to "undefined" and leaves ExtendedData and ExtendedMetadata as null.
        /// </summary>
        [Fact]
        public void DefaultConstructor_WhenCalled_SetsExtendedTypeToUndefined()
        {
            // Arrange & Act
            var instance = new ExtendedBuildMessageEventArgs();

            // Assert
            Assert.Equal("undefined", instance.ExtendedType);
            Assert.Null(instance.ExtendedData);
            Assert.Null(instance.ExtendedMetadata);
        }

        /// <summary>
        /// Tests that the constructor accepting only a type sets ExtendedType correctly.
        /// </summary>
        [Fact]
        public void Constructor_WithType_SetsExtendedTypeCorrectly()
        {
            // Arrange
            string type = "CustomType";

            // Act
            var instance = new ExtendedBuildMessageEventArgs(type);

            // Assert
            Assert.Equal(type, instance.ExtendedType);
            Assert.Null(instance.ExtendedData);
            Assert.Null(instance.ExtendedMetadata);
        }

        /// <summary>
        /// Tests that the constructor with basic parameters sets the properties correctly.
        /// </summary>
        [Fact]
        public void Constructor_WithBasicParameters_SetsProperties()
        {
            // Arrange
            string type = "Basic";
            string message = "Test message";
            string helpKeyword = "Help";
            string senderName = "Sender";
            MessageImportance importance = MessageImportance.High;

            // Act
            var instance = new ExtendedBuildMessageEventArgs(type, message, helpKeyword, senderName, importance);

            // Assert
            Assert.Equal(type, instance.ExtendedType);
            Assert.Equal(message, instance.Message);
            Assert.Equal(helpKeyword, instance.HelpKeyword);
            Assert.Equal(senderName, instance.SenderName);
            Assert.Equal(importance, instance.Importance);
        }

        /// <summary>
        /// Tests that the constructor with timestamp sets the properties including the event timestamp.
        /// </summary>
        [Fact]
        public void Constructor_WithTimestamp_SetsProperties()
        {
            // Arrange
            string type = "TimestampTest";
            string message = "Message with timestamp";
            string helpKeyword = "TimestampHelp";
            string senderName = "TimestampSender";
            MessageImportance importance = MessageImportance.Low;
            DateTime timestamp = new DateTime(2023, 1, 1, 12, 0, 0);

            // Act
            var instance = new ExtendedBuildMessageEventArgs(type, message, helpKeyword, senderName, importance, timestamp);

            // Assert
            Assert.Equal(type, instance.ExtendedType);
            Assert.Equal(message, instance.Message);
            Assert.Equal(helpKeyword, instance.HelpKeyword);
            Assert.Equal(senderName, instance.SenderName);
            Assert.Equal(importance, instance.Importance);
            Assert.Equal(timestamp, instance.Timestamp);
        }

        /// <summary>
        /// Tests that the constructor with timestamp and message arguments sets the properties correctly.
        /// </summary>
        [Fact]
        public void Constructor_WithTimestampAndMessageArgs_SetsProperties()
        {
            // Arrange
            string type = "ArgsTest";
            string message = "Message with args";
            string helpKeyword = "ArgsHelp";
            string senderName = "ArgsSender";
            MessageImportance importance = MessageImportance.Normal;
            DateTime timestamp = new DateTime(2022, 12, 31, 23, 59, 59);
            object[] messageArgs = new object[] { "arg1", 42 };

            // Act
            var instance = new ExtendedBuildMessageEventArgs(type, message, helpKeyword, senderName, importance, timestamp, messageArgs);

            // Assert
            Assert.Equal(type, instance.ExtendedType);
            Assert.Equal(message, instance.Message);
            Assert.Equal(helpKeyword, instance.HelpKeyword);
            Assert.Equal(senderName, instance.SenderName);
            Assert.Equal(importance, instance.Importance);
            Assert.Equal(timestamp, instance.Timestamp);
            // If the base class exposes MessageArgs, verify them; if not, at least ensure no exception was thrown.
            // Using reflection to check for property "MessageArgs" if available.
            var messageArgsProperty = instance.GetType().GetProperty("MessageArgs");
            if (messageArgsProperty != null)
            {
                var argsValue = messageArgsProperty.GetValue(instance) as object[];
                Assert.NotNull(argsValue);
                Assert.Equal(messageArgs, argsValue);
            }
        }

        /// <summary>
        /// Tests that the constructor with file information sets all the corresponding properties.
        /// </summary>
        [Fact]
        public void Constructor_WithFileInfo_SetsProperties()
        {
            // Arrange
            string type = "FileInfoTest";
            string subcategory = "Subcat";
            string code = "Code123";
            string file = "file.txt";
            int lineNumber = 10;
            int columnNumber = 5;
            int endLineNumber = 10;
            int endColumnNumber = 15;
            string message = "File info message";
            string helpKeyword = "FileHelp";
            string senderName = "FileSender";
            MessageImportance importance = MessageImportance.High;

            // Act
            var instance = new ExtendedBuildMessageEventArgs(type, subcategory, code, file, lineNumber, columnNumber, endLineNumber, endColumnNumber, message, helpKeyword, senderName, importance);

            // Assert
            Assert.Equal(type, instance.ExtendedType);
            Assert.Equal(message, instance.Message);
            Assert.Equal(helpKeyword, instance.HelpKeyword);
            Assert.Equal(senderName, instance.SenderName);
            Assert.Equal(importance, instance.Importance);

            // Verify file information properties if available
            var subcategoryProperty = instance.GetType().GetProperty("Subcategory");
            if (subcategoryProperty != null)
            {
                Assert.Equal(subcategory, subcategoryProperty.GetValue(instance));
            }
            var codeProperty = instance.GetType().GetProperty("Code");
            if (codeProperty != null)
            {
                Assert.Equal(code, codeProperty.GetValue(instance));
            }
            var fileProperty = instance.GetType().GetProperty("File");
            if (fileProperty != null)
            {
                Assert.Equal(file, fileProperty.GetValue(instance));
            }
            var lineNumberProperty = instance.GetType().GetProperty("LineNumber");
            if (lineNumberProperty != null)
            {
                Assert.Equal(lineNumber, lineNumberProperty.GetValue(instance));
            }
            var columnNumberProperty = instance.GetType().GetProperty("ColumnNumber");
            if (columnNumberProperty != null)
            {
                Assert.Equal(columnNumber, columnNumberProperty.GetValue(instance));
            }
            var endLineNumberProperty = instance.GetType().GetProperty("EndLineNumber");
            if (endLineNumberProperty != null)
            {
                Assert.Equal(endLineNumber, endLineNumberProperty.GetValue(instance));
            }
            var endColumnNumberProperty = instance.GetType().GetProperty("EndColumnNumber");
            if (endColumnNumberProperty != null)
            {
                Assert.Equal(endColumnNumber, endColumnNumberProperty.GetValue(instance));
            }
        }

        /// <summary>
        /// Tests that the constructor with file information and timestamp sets all the properties correctly.
        /// </summary>
        [Fact]
        public void Constructor_WithFileInfoAndTimestamp_SetsProperties()
        {
            // Arrange
            string type = "FileInfoTimestampTest";
            string subcategory = "SubcatTS";
            string code = "CodeTS";
            string file = "tsfile.txt";
            int lineNumber = 20;
            int columnNumber = 6;
            int endLineNumber = 20;
            int endColumnNumber = 16;
            string message = "Timestamp file info message";
            string helpKeyword = "TSHelp";
            string senderName = "TSSender";
            MessageImportance importance = MessageImportance.Normal;
            DateTime timestamp = new DateTime(2021, 6, 15, 8, 30, 0);

            // Act
            var instance = new ExtendedBuildMessageEventArgs(type, subcategory, code, file, lineNumber, columnNumber, endLineNumber, endColumnNumber, message, helpKeyword, senderName, importance, timestamp);

            // Assert
            Assert.Equal(type, instance.ExtendedType);
            Assert.Equal(message, instance.Message);
            Assert.Equal(helpKeyword, instance.HelpKeyword);
            Assert.Equal(senderName, instance.SenderName);
            Assert.Equal(importance, instance.Importance);
            Assert.Equal(timestamp, instance.Timestamp);

            // Verify file information properties if available
            var subcategoryProperty = instance.GetType().GetProperty("Subcategory");
            if (subcategoryProperty != null)
            {
                Assert.Equal(subcategory, subcategoryProperty.GetValue(instance));
            }
            var codeProperty = instance.GetType().GetProperty("Code");
            if (codeProperty != null)
            {
                Assert.Equal(code, codeProperty.GetValue(instance));
            }
            var fileProperty = instance.GetType().GetProperty("File");
            if (fileProperty != null)
            {
                Assert.Equal(file, fileProperty.GetValue(instance));
            }
            var lineNumberProperty = instance.GetType().GetProperty("LineNumber");
            if (lineNumberProperty != null)
            {
                Assert.Equal(lineNumber, lineNumberProperty.GetValue(instance));
            }
            var columnNumberProperty = instance.GetType().GetProperty("ColumnNumber");
            if (columnNumberProperty != null)
            {
                Assert.Equal(columnNumber, columnNumberProperty.GetValue(instance));
            }
            var endLineNumberProperty = instance.GetType().GetProperty("EndLineNumber");
            if (endLineNumberProperty != null)
            {
                Assert.Equal(endLineNumber, endLineNumberProperty.GetValue(instance));
            }
            var endColumnNumberProperty = instance.GetType().GetProperty("EndColumnNumber");
            if (endColumnNumberProperty != null)
            {
                Assert.Equal(endColumnNumber, endColumnNumberProperty.GetValue(instance));
            }
        }

        /// <summary>
        /// Tests that the constructor with file information, timestamp, and message arguments sets all properties correctly.
        /// </summary>
        [Fact]
        public void Constructor_WithFileInfoTimestampAndMessageArgs_SetsProperties()
        {
            // Arrange
            string type = "FullTest";
            string subcategory = "FullSubcat";
            string code = "FullCode";
            string file = "fullfile.txt";
            int lineNumber = 30;
            int columnNumber = 7;
            int endLineNumber = 30;
            int endColumnNumber = 17;
            string message = "Full constructor message";
            string helpKeyword = "FullHelp";
            string senderName = "FullSender";
            MessageImportance importance = MessageImportance.High;
            DateTime timestamp = new DateTime(2020, 3, 10, 14, 0, 0);
            object[] messageArgs = new object[] { "argA", "argB" };

            // Act
            var instance = new ExtendedBuildMessageEventArgs(type, subcategory, code, file, lineNumber, columnNumber, endLineNumber, endColumnNumber, message, helpKeyword, senderName, importance, timestamp, messageArgs);

            // Assert
            Assert.Equal(type, instance.ExtendedType);
            Assert.Equal(message, instance.Message);
            Assert.Equal(helpKeyword, instance.HelpKeyword);
            Assert.Equal(senderName, instance.SenderName);
            Assert.Equal(importance, instance.Importance);
            Assert.Equal(timestamp, instance.Timestamp);

            // Verify file information properties if available
            var subcategoryProperty = instance.GetType().GetProperty("Subcategory");
            if (subcategoryProperty != null)
            {
                Assert.Equal(subcategory, subcategoryProperty.GetValue(instance));
            }
            var codeProperty = instance.GetType().GetProperty("Code");
            if (codeProperty != null)
            {
                Assert.Equal(code, codeProperty.GetValue(instance));
            }
            var fileProperty = instance.GetType().GetProperty("File");
            if (fileProperty != null)
            {
                Assert.Equal(file, fileProperty.GetValue(instance));
            }
            var lineNumberProperty = instance.GetType().GetProperty("LineNumber");
            if (lineNumberProperty != null)
            {
                Assert.Equal(lineNumber, lineNumberProperty.GetValue(instance));
            }
            var columnNumberProperty = instance.GetType().GetProperty("ColumnNumber");
            if (columnNumberProperty != null)
            {
                Assert.Equal(columnNumber, columnNumberProperty.GetValue(instance));
            }
            var endLineNumberProperty = instance.GetType().GetProperty("EndLineNumber");
            if (endLineNumberProperty != null)
            {
                Assert.Equal(endLineNumber, endLineNumberProperty.GetValue(instance));
            }
            var endColumnNumberProperty = instance.GetType().GetProperty("EndColumnNumber");
            if (endColumnNumberProperty != null)
            {
                Assert.Equal(endColumnNumber, endColumnNumberProperty.GetValue(instance));
            }

            // Optionally verify message arguments if the property is exposed.
            var messageArgsProperty = instance.GetType().GetProperty("MessageArgs");
            if (messageArgsProperty != null)
            {
                var argsValue = messageArgsProperty.GetValue(instance) as object[];
                Assert.NotNull(argsValue);
                Assert.Equal(messageArgs, argsValue);
            }
        }
    }
}

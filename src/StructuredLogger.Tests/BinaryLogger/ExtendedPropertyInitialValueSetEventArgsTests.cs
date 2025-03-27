using Microsoft.Build.Framework;
using StructuredLogger.BinaryLogger;
using System;
using Xunit;

namespace StructuredLogger.BinaryLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="ExtendedPropertyInitialValueSetEventArgs"/> class.
    /// </summary>
    public class ExtendedPropertyInitialValueSetEventArgsTests
    {
        private readonly string _testPropertyName = "TestPropertyName";
        private readonly string _testPropertyValue = "TestPropertyValue";
        private readonly string _testPropertySource = "TestPropertySource";
        private readonly string _testFile = "TestFile.cs";
        private readonly int _testLine = 42;
        private readonly int _testColumn = 5;
        private readonly string _testMessage = "Test message";
        private readonly string _testHelpKeyword = "HELP001";
        private readonly string _testSenderName = "TestSender";
        // Cannot be defined here as vstest runner would fail to load the tests
        // private readonly MessageImportance _testImportance = MessageImportance.High;

        /// <summary>
        /// Tests that constructing the event args with all parameters sets the properties correctly.
        /// </summary>
        [Fact]
        public void Constructor_WithAllParameters_SetsPropertiesCorrectly()
        {
            // Arrange
            // All required parameters including optional ones are provided

            // Act
            var eventArgs = new ExtendedPropertyInitialValueSetEventArgs(
                propertyName: _testPropertyName,
                propertyValue: _testPropertyValue,
                propertySource: _testPropertySource,
                file: _testFile,
                line: _testLine,
                column: _testColumn,
                message: _testMessage,
                helpKeyword: _testHelpKeyword,
                senderName: _testSenderName,
                importance: MessageImportance.High);

            // Assert
            Assert.Equal(_testPropertyName, eventArgs.PropertyName);
            Assert.Equal(_testPropertyValue, eventArgs.PropertyValue);
            Assert.Equal(_testPropertySource, eventArgs.PropertySource);
            Assert.Equal(_testFile, eventArgs.File);
            Assert.Equal(_testLine, eventArgs.LineNumber);
            Assert.Equal(_testColumn, eventArgs.ColumnNumber);
            Assert.Equal(_testMessage, eventArgs.Message);
            Assert.Equal(_testHelpKeyword, eventArgs.HelpKeyword);
            Assert.Equal(_testSenderName, eventArgs.SenderName);
            Assert.Equal(MessageImportance.High, eventArgs.Importance);
        }

        /// <summary>
        /// Tests that constructing the event args without optional parameters assigns default values.
        /// </summary>
        [Fact]
        public void Constructor_WithoutOptionalParameters_AssignsDefaultValues()
        {
            // Arrange
            // Optional parameters helpKeyword and senderName are not provided. Importance should default to MessageImportance.Low.
            var expectedDefaultHelpKeyword = (string)null;
            var expectedDefaultSenderName = (string)null;
            var expectedDefaultImportance = MessageImportance.Low;

            // Act
            var eventArgs = new ExtendedPropertyInitialValueSetEventArgs(
                propertyName: _testPropertyName,
                propertyValue: _testPropertyValue,
                propertySource: _testPropertySource,
                file: _testFile,
                line: _testLine,
                column: _testColumn,
                message: _testMessage);

            // Assert
            Assert.Equal(_testPropertyName, eventArgs.PropertyName);
            Assert.Equal(_testPropertyValue, eventArgs.PropertyValue);
            Assert.Equal(_testPropertySource, eventArgs.PropertySource);
            Assert.Equal(_testFile, eventArgs.File);
            Assert.Equal(_testLine, eventArgs.LineNumber);
            Assert.Equal(_testColumn, eventArgs.ColumnNumber);
            Assert.Equal(_testMessage, eventArgs.Message);
            Assert.Equal(expectedDefaultHelpKeyword, eventArgs.HelpKeyword);
            Assert.Equal(expectedDefaultSenderName, eventArgs.SenderName);
            Assert.Equal(expectedDefaultImportance, eventArgs.Importance);
        }

        /// <summary>
        /// Tests that the public property setters allow updating of the property values after construction.
        /// </summary>
        [Fact]
        public void PropertySetters_WhenCalled_UpdatesProperties()
        {
            // Arrange
            var eventArgs = new ExtendedPropertyInitialValueSetEventArgs(
                propertyName: _testPropertyName,
                propertyValue: _testPropertyValue,
                propertySource: _testPropertySource,
                file: _testFile,
                line: _testLine,
                column: _testColumn,
                message: _testMessage,
                helpKeyword: _testHelpKeyword,
                senderName: _testSenderName,
                importance: MessageImportance.High);

            var newPropertyName = "NewPropertyName";
            var newPropertyValue = "NewPropertyValue";
            var newPropertySource = "NewPropertySource";

            // Act
            eventArgs.PropertyName = newPropertyName;
            eventArgs.PropertyValue = newPropertyValue;
            eventArgs.PropertySource = newPropertySource;

            // Assert
            Assert.Equal(newPropertyName, eventArgs.PropertyName);
            Assert.Equal(newPropertyValue, eventArgs.PropertyValue);
            Assert.Equal(newPropertySource, eventArgs.PropertySource);
        }
    }
}

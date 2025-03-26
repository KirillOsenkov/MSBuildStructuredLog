using Microsoft.Build.Framework;
using StructuredLogger.BinaryLogger;
using Xunit;

namespace StructuredLogger.BinaryLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="ExtendedPropertyReassignmentEventArgs"/> class.
    /// </summary>
    public class ExtendedPropertyReassignmentEventArgsTests
    {
        private readonly string _validPropertyName;
        private readonly string _validPreviousValue;
        private readonly string _validNewValue;
        private readonly string _validFile;
        private readonly int _validLine;
        private readonly int _validColumn;
        private readonly string _validMessage;
        private readonly string _validHelpKeyword;
        private readonly string _validSenderName;
        private readonly MessageImportance _validImportance;

        /// <summary>
        /// Initializes test data used in the tests.
        /// </summary>
        public ExtendedPropertyReassignmentEventArgsTests()
        {
            _validPropertyName = "TestProperty";
            _validPreviousValue = "OldValue";
            _validNewValue = "NewValue";
            _validFile = "dummy.cs";
            _validLine = 10;
            _validColumn = 5;
            _validMessage = "Test Message";
            _validHelpKeyword = "HelpKeyword123";
            _validSenderName = "UnitTest";
            _validImportance = MessageImportance.Low;
        }

        /// <summary>
        /// Tests that the constructor initializes properties correctly when provided with valid, non-null parameters.
        /// The test verifies both the properties defined in the derived class and the inherited properties from BuildMessageEventArgs.
        /// </summary>
        [Fact]
        public void Constructor_ValidParameters_SetsPropertiesCorrectly()
        {
            // Arrange & Act
            var eventArgs = new ExtendedPropertyReassignmentEventArgs(
                _validPropertyName,
                _validPreviousValue,
                _validNewValue,
                _validFile,
                _validLine,
                _validColumn,
                _validMessage,
                _validHelpKeyword,
                _validSenderName,
                _validImportance);

            // Assert - check properties defined in ExtendedPropertyReassignmentEventArgs
            Assert.Equal(_validPropertyName, eventArgs.PropertyName);
            Assert.Equal(_validPreviousValue, eventArgs.PreviousValue);
            Assert.Equal(_validNewValue, eventArgs.NewValue);

            // Assert - check inherited properties from BuildMessageEventArgs
            Assert.Equal(_validFile, eventArgs.File);
            Assert.Equal(_validLine, eventArgs.LineNumber);
            Assert.Equal(_validColumn, eventArgs.ColumnNumber);
            Assert.Equal(_validMessage, eventArgs.Message);
            Assert.Equal(_validHelpKeyword, eventArgs.HelpKeyword);
            Assert.Equal(_validSenderName, eventArgs.SenderName);
            Assert.Equal(_validImportance, eventArgs.Importance);
        }

        /// <summary>
        /// Tests that the constructor correctly handles null values for the optional parameters helpKeyword and senderName,
        /// ensuring that these properties are set to null without affecting other properties.
        /// </summary>
        [Fact]
        public void Constructor_NullOptionalParameters_SetsOptionalPropertiesToNull()
        {
            // Arrange
            string nullHelpKeyword = null;
            string nullSenderName = null;

            // Act
            var eventArgs = new ExtendedPropertyReassignmentEventArgs(
                _validPropertyName,
                _validPreviousValue,
                _validNewValue,
                _validFile,
                _validLine,
                _validColumn,
                _validMessage,
                nullHelpKeyword,
                nullSenderName,
                _validImportance);

            // Assert
            Assert.Equal(_validPropertyName, eventArgs.PropertyName);
            Assert.Equal(_validPreviousValue, eventArgs.PreviousValue);
            Assert.Equal(_validNewValue, eventArgs.NewValue);
            Assert.Null(eventArgs.HelpKeyword);
            Assert.Null(eventArgs.SenderName);
        }

        /// <summary>
        /// Tests that the public properties of the ExtendedPropertyReassignmentEventArgs class can be modified after construction.
        /// This ensures that the get/set accessors for PropertyName, PreviousValue, and NewValue work as expected.
        /// </summary>
        [Fact]
        public void Properties_CanBeModifiedAfterConstruction()
        {
            // Arrange
            var eventArgs = new ExtendedPropertyReassignmentEventArgs(
                _validPropertyName,
                _validPreviousValue,
                _validNewValue,
                _validFile,
                _validLine,
                _validColumn,
                _validMessage,
                _validHelpKeyword,
                _validSenderName,
                _validImportance);

            string updatedPropertyName = "UpdatedProperty";
            string updatedPreviousValue = "UpdatedOldValue";
            string updatedNewValue = "UpdatedNewValue";

            // Act
            eventArgs.PropertyName = updatedPropertyName;
            eventArgs.PreviousValue = updatedPreviousValue;
            eventArgs.NewValue = updatedNewValue;

            // Assert
            Assert.Equal(updatedPropertyName, eventArgs.PropertyName);
            Assert.Equal(updatedPreviousValue, eventArgs.PreviousValue);
            Assert.Equal(updatedNewValue, eventArgs.NewValue);
        }
    }
}

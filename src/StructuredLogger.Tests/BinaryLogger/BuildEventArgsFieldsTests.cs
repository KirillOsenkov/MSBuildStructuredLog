using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Xunit;

namespace Microsoft.Build.Logging.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="BuildEventArgsFields"/> class.
    /// </summary>
    public class BuildEventArgsFieldsTests
    {
        private readonly BuildEventArgsFields _buildEventArgsFields;

        /// <summary>
        /// Initializes a new instance of the <see cref="BuildEventArgsFieldsTests"/> class.
        /// </summary>
        public BuildEventArgsFieldsTests()
        {
            _buildEventArgsFields = new BuildEventArgsFields();
        }

        /// <summary>
        /// Tests that the default value for the Importance property is MessageImportance.Low.
        /// </summary>
        [Fact]
        public void Constructor_ShouldInitializeImportanceToLow()
        {
            // Arrange & Act done in constructor

            // Assert
            Assert.Equal(MessageImportance.Low, _buildEventArgsFields.Importance);
        }

        /// <summary>
        /// Tests the getters and setters of all properties to ensure they store and retrieve values correctly.
        /// This test sets all properties using sample values, including edge cases like empty and null strings.
        /// </summary>
        [Fact]
        public void Properties_SetterAndGetter_ShouldWorkCorrectly()
        {
            // Arrange
            // For properties of unknown types, using default values or simple assignments.
            var expectedFlags = (BuildEventArgsFieldFlags)123;
            string expectedMessage = "Test Message";
            object[] expectedArguments = new object[] { 1, "two", 3.0 };
            var expectedBuildEventContext = new BuildEventContext(1, 2, 3, 4);
            int expectedThreadId = 42;
            string expectedHelpKeyword = "HelpTest";
            string expectedSenderName = "SenderTest";
            DateTime expectedTimestamp = new DateTime(2023, 1, 1);
            MessageImportance expectedImportance = MessageImportance.High;
            string expectedSubcategory = "SubcategoryTest";
            string expectedCode = "CodeTest";
            string expectedFile = "C:\\temp\\file.txt";
            string expectedProjectFile = "C:\\temp\\project.csproj";
            int expectedLineNumber = 10;
            int expectedColumnNumber = 20;
            int expectedEndLineNumber = 15;
            int expectedEndColumnNumber = 25;
            var expectedExtended = new ExtendedDataFields();

            // Act
            _buildEventArgsFields.Flags = expectedFlags;
            _buildEventArgsFields.Message = expectedMessage;
            _buildEventArgsFields.Arguments = expectedArguments;
            _buildEventArgsFields.BuildEventContext = expectedBuildEventContext;
            _buildEventArgsFields.ThreadId = expectedThreadId;
            _buildEventArgsFields.HelpKeyword = expectedHelpKeyword;
            _buildEventArgsFields.SenderName = expectedSenderName;
            _buildEventArgsFields.Timestamp = expectedTimestamp;
            _buildEventArgsFields.Importance = expectedImportance;
            _buildEventArgsFields.Subcategory = expectedSubcategory;
            _buildEventArgsFields.Code = expectedCode;
            _buildEventArgsFields.File = expectedFile;
            _buildEventArgsFields.ProjectFile = expectedProjectFile;
            _buildEventArgsFields.LineNumber = expectedLineNumber;
            _buildEventArgsFields.ColumnNumber = expectedColumnNumber;
            _buildEventArgsFields.EndLineNumber = expectedEndLineNumber;
            _buildEventArgsFields.EndColumnNumber = expectedEndColumnNumber;
            _buildEventArgsFields.Extended = expectedExtended;

            // Assert
            Assert.Equal(expectedFlags, _buildEventArgsFields.Flags);
            Assert.Equal(expectedMessage, _buildEventArgsFields.Message);
            Assert.Equal(expectedArguments, _buildEventArgsFields.Arguments);
            Assert.Equal(expectedBuildEventContext, _buildEventArgsFields.BuildEventContext);
            Assert.Equal(expectedThreadId, _buildEventArgsFields.ThreadId);
            Assert.Equal(expectedHelpKeyword, _buildEventArgsFields.HelpKeyword);
            Assert.Equal(expectedSenderName, _buildEventArgsFields.SenderName);
            Assert.Equal(expectedTimestamp, _buildEventArgsFields.Timestamp);
            Assert.Equal(expectedImportance, _buildEventArgsFields.Importance);
            Assert.Equal(expectedSubcategory, _buildEventArgsFields.Subcategory);
            Assert.Equal(expectedCode, _buildEventArgsFields.Code);
            Assert.Equal(expectedFile, _buildEventArgsFields.File);
            Assert.Equal(expectedProjectFile, _buildEventArgsFields.ProjectFile);
            Assert.Equal(expectedLineNumber, _buildEventArgsFields.LineNumber);
            Assert.Equal(expectedColumnNumber, _buildEventArgsFields.ColumnNumber);
            Assert.Equal(expectedEndLineNumber, _buildEventArgsFields.EndLineNumber);
            Assert.Equal(expectedEndColumnNumber, _buildEventArgsFields.EndColumnNumber);
            Assert.Equal(expectedExtended, _buildEventArgsFields.Extended);
        }

        /// <summary>
        /// Tests the behavior of setting nullable string properties to null.
        /// This ensures that the properties can handle null input without issues.
        /// </summary>
        [Fact]
        public void StringProperties_SetToNull_ShouldReturnNull()
        {
            // Arrange
            _buildEventArgsFields.Message = null;
            _buildEventArgsFields.HelpKeyword = null;
            _buildEventArgsFields.SenderName = null;
            _buildEventArgsFields.Subcategory = null;
            _buildEventArgsFields.Code = null;
            _buildEventArgsFields.File = null;
            _buildEventArgsFields.ProjectFile = null;

            // Act & Assert
            Assert.Null(_buildEventArgsFields.Message);
            Assert.Null(_buildEventArgsFields.HelpKeyword);
            Assert.Null(_buildEventArgsFields.SenderName);
            Assert.Null(_buildEventArgsFields.Subcategory);
            Assert.Null(_buildEventArgsFields.Code);
            Assert.Null(_buildEventArgsFields.File);
            Assert.Null(_buildEventArgsFields.ProjectFile);
        }
    }
}

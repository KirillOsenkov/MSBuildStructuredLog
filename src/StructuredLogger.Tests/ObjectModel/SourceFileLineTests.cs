using System;
using Microsoft.Build.Logging.StructuredLogger;
using Xunit;

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="SourceFileLine"/> class.
    /// </summary>
    public class SourceFileLineTests
    {
        /// <summary>
        /// Tests that the <see cref="SourceFileLine.TypeName"/> property returns the correct name.
        /// </summary>
        [Fact]
        public void TypeName_WhenAccessed_ReturnsCorrectName()
        {
            // Arrange
            var sourceFileLine = new SourceFileLine();

            // Act
            string actualTypeName = sourceFileLine.TypeName;

            // Assert
            Assert.Equal(nameof(SourceFileLine), actualTypeName);
        }

        /// <summary>
        /// Tests that the <see cref="SourceFileLine.ToString"/> method formats the string correctly
        /// when both LineNumber and LineText are provided.
        /// </summary>
        /// <param name="lineNumber">The line number to set.</param>
        /// <param name="lineText">The line text to set.</param>
        /// <param name="expected">The expected resulting string.</param>
        [Theory]
        [InlineData(1, "Test line", "1    Test line")]
        [InlineData(12345, "Another test", "12345Another test")]
        [InlineData(0, "", "0    ")]
        [InlineData(-1, "Negative", "-1   Negative")]
        public void ToString_WithValidData_ReturnsFormattedString(int lineNumber, string lineText, string expected)
        {
            // Arrange
            var sourceFileLine = new SourceFileLine
            {
                LineNumber = lineNumber,
                LineText = lineText
            };

            // Act
            string actualResult = sourceFileLine.ToString();

            // Assert
            Assert.Equal(expected, actualResult);
        }

        /// <summary>
        /// Tests that the <see cref="SourceFileLine.ToString"/> method handles a null LineText gracefully,
        /// treating null as an empty string when concatenated.
        /// </summary>
        [Fact]
        public void ToString_WhenLineTextIsNull_ReturnsPaddedLineNumberOnly()
        {
            // Arrange
            var sourceFileLine = new SourceFileLine
            {
                LineNumber = 10,
                LineText = null
            };

            // Act
            string actualResult = sourceFileLine.ToString();

            // Assert
            // Since concatenating a string with null results in the original string, we expect only the padded line number.
            Assert.Equal("10   ", actualResult);
        }
    }
}

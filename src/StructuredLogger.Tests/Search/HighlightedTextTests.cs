using System;
using Microsoft.Build.Logging.StructuredLogger;
using Xunit;

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="HighlightedText"/> class.
    /// </summary>
    public class HighlightedTextTests
    {
        private readonly HighlightedText _highlightedText;

        /// <summary>
        /// Initializes a new instance of the <see cref="HighlightedTextTests"/> class.
        /// </summary>
        public HighlightedTextTests()
        {
            _highlightedText = new HighlightedText();
        }

        /// <summary>
        /// Tests that the ToString method returns the value of the Text property when it is set to a non-null value.
        /// </summary>
        /// <param name="textValue">The non-null string value to be set for the Text property.</param>
        [Theory]
        [InlineData("Hello, World!")]
        [InlineData("")]
        [InlineData("Test String")]
        public void ToString_WhenTextIsSet_ReturnsTextValue(string textValue)
        {
            // Arrange
            _highlightedText.Text = textValue;

            // Act
            string result = _highlightedText.ToString();

            // Assert
            Assert.Equal(textValue, result);
        }

        /// <summary>
        /// Tests that the ToString method returns null when the Text property is not set.
        /// </summary>
        [Fact]
        public void ToString_WhenTextIsNotSet_ReturnsNull()
        {
            // Arrange
            // Do not set the Text property; it remains null by default.

            // Act
            string result = _highlightedText.ToString();

            // Assert
            Assert.Null(result);
        }

        /// <summary>
        /// Tests that the Text property can be set and retrieved correctly.
        /// </summary>
        [Fact]
        public void TextProperty_SetAndGet_ReturnsSameValue()
        {
            // Arrange
            string expected = "Sample Text";

            // Act
            _highlightedText.Text = expected;
            string actual = _highlightedText.Text;

            // Assert
            Assert.Equal(expected, actual);
        }

        /// <summary>
        /// Tests that the Style property can be set and retrieved correctly.
        /// </summary>
        [Fact]
        public void StyleProperty_SetAndGet_ReturnsSameValue()
        {
            // Arrange
            string expected = "Bold";

            // Act
            _highlightedText.Style = expected;
            string actual = _highlightedText.Style;

            // Assert
            Assert.Equal(expected, actual);
        }
    }
}

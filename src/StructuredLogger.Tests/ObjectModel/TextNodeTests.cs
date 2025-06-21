using System;
using Microsoft.Build.Logging.StructuredLogger;
using Xunit;

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="TextNode"/> class.
    /// </summary>
    public class TextNodeTests
    {
        /// <summary>
        /// Helper method to mimic expected behavior of TextUtilities.ShortenValue.
        /// Assumes that if the input text has a length greater than 10, it is shortened to the first 10 characters followed by "..." ; otherwise, the original text is returned.
        /// </summary>
        /// <param name="text">The original text.</param>
        /// <returns>The expected shortened text.</returns>
        private string ExpectedShortenValue(string text)
        {
            if (text == null)
            {
                return null;
            }

            return text.Length <= 10 ? text : text.Substring(0, 10) + "...";
        }

        /// <summary>
        /// Helper method to mimic expected behavior of TextUtilities.GetShortenLength.
        /// Assumes that if the input text has a length greater than 10, the shorten length is 10; otherwise, it is the original text length.
        /// </summary>
        /// <param name="text">The original text.</param>
        /// <returns>The expected shorten length.</returns>
        private int ExpectedGetShortenLength(string text)
        {
            if (text == null)
            {
                return 0;
            }

            return text.Length <= 10 ? text.Length : 10;
        }

        /// <summary>
        /// Tests the ShortenedText property when Text is null.
        /// The expected behavior is that the ShortenedText property returns null.
        /// </summary>
        [Fact]
        public void ShortenedText_NullText_ReturnsNull()
        {
            // Arrange
            var textNode = new TextNode
            {
                Text = null
            };

            // Act
            string shortenedText = textNode.ShortenedText;

            // Assert
            Assert.Null(shortenedText);
        }

        /// <summary>
        /// Tests the ShortenedText property when Text is a short string.
        /// The expected behavior is that the ShortenedText property returns the same string.
        /// </summary>
        [Theory]
        [InlineData("")]
        [InlineData("short")]
        [InlineData("1234567890")] // exactly 10 characters
        public void ShortenedText_ShortText_ReturnsOriginalText(string input)
        {
            // Arrange
            var textNode = new TextNode
            {
                Text = input
            };
            string expected = ExpectedShortenValue(input);

            // Act
            string actual = textNode.ShortenedText;

            // Assert
            Assert.Equal(expected, actual);
        }

        /// <summary>
        /// Tests the ShortenedText property when Text is a long string.
        /// The expected behavior is that the ShortenedText property returns a shortened version of the original text.
        /// </summary>
        [Fact]
        public void ShortenedText_LongText_ReturnsShortenedText()
        {
            // Arrange
            string longText = "This is a long text for testing.";
            var textNode = new TextNode
            {
                Text = longText
            };
            string expected = ExpectedShortenValue(longText);

            // Act
            string actual = textNode.ShortenedText;

            // Assert
            Assert.Equal(expected, actual);
        }

        /// <summary>
        /// Tests the IsTextShortened property when Text is null.
        /// The expected behavior is that IsTextShortened returns false.
        /// </summary>
        [Fact]
        public void IsTextShortened_NullText_ReturnsFalse()
        {
            // Arrange
            var textNode = new TextNode
            {
                Text = null
            };

            // Act
            bool isShortened = textNode.IsTextShortened;

            // Assert
            Assert.False(isShortened);
        }

        /// <summary>
        /// Tests the IsTextShortened property when Text is an empty string.
        /// The expected behavior is that IsTextShortened returns false.
        /// </summary>
        [Fact]
        public void IsTextShortened_EmptyText_ReturnsFalse()
        {
            // Arrange
            var textNode = new TextNode
            {
                Text = ""
            };

            // Act
            bool isShortened = textNode.IsTextShortened;

            // Assert
            Assert.False(isShortened);
        }

        /// <summary>
        /// Tests the IsTextShortened property when Text is a short string (length less than or equal to 10).
        /// The expected behavior is that IsTextShortened returns false.
        /// </summary>
        [Theory]
        [InlineData("short")]
        [InlineData("1234567890")] // exactly 10 characters
        public void IsTextShortened_ShortText_ReturnsFalse(string input)
        {
            // Arrange
            var textNode = new TextNode
            {
                Text = input
            };

            // Act
            bool isShortened = textNode.IsTextShortened;

            // Assert
            Assert.False(isShortened);
        }

        /// <summary>
        /// Tests the IsTextShortened property when Text is a long string (length greater than 10).
        /// The expected behavior is that IsTextShortened returns true.
        /// </summary>
        [Fact]
        public void IsTextShortened_LongText_ReturnsTrue()
        {
            // Arrange
            string longText = "This text is definitely longer than ten characters.";
            var textNode = new TextNode
            {
                Text = longText
            };

            // Act
            bool isShortened = textNode.IsTextShortened;

            // Assert
            // Expected shorten length is 10 which should differ from the original length.
            Assert.True(isShortened);
        }

        /// <summary>
        /// Tests the TypeName property.
        /// The expected behavior is that TypeName returns the string "TextNode".
        /// </summary>
        [Fact]
        public void TypeName_Always_ReturnsTextNode()
        {
            // Arrange
            var textNode = new TextNode();

            // Act
            string typeName = textNode.TypeName;

            // Assert
            Assert.Equal("TextNode", typeName);
        }

        /// <summary>
        /// Tests the Title property when Text is null.
        /// The expected behavior is that Title returns TypeName.
        /// </summary>
        [Fact]
        public void Title_NullText_ReturnsTypeName()
        {
            // Arrange
            var textNode = new TextNode
            {
                Text = null
            };

            // Act
            string title = textNode.Title;

            // Assert
            Assert.Equal(textNode.TypeName, title);
        }

        /// <summary>
        /// Tests the Title property when Text is non-null.
        /// The expected behavior is that Title returns the same value as Text.
        /// </summary>
        [Theory]
        [InlineData("Sample text")]
        [InlineData("Another example")]
        public void Title_NonNullText_ReturnsText(string input)
        {
            // Arrange
            var textNode = new TextNode
            {
                Text = input
            };

            // Act
            string title = textNode.Title;

            // Assert
            Assert.Equal(input, title);
        }

        /// <summary>
        /// Tests the ToString method.
        /// The expected behavior is that ToString returns the same value as Title.
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("Testing ToString")]
        public void ToString_ReturnsTitle(string input)
        {
            // Arrange
            var textNode = new TextNode
            {
                Text = input
            };
            string expected = textNode.Title;

            // Act
            string actual = textNode.ToString();

            // Assert
            Assert.Equal(expected, actual);
        }
    }
}

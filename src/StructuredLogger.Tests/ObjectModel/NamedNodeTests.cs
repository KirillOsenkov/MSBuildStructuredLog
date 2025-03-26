using System;
using Microsoft.Build.Logging.StructuredLogger;
using Xunit;

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="NamedNode"/> class.
    /// </summary>
    public class NamedNodeTests
    {
        /// <summary>
        /// Tests that the default constructor initializes properties to their expected default values.
        /// Expected: Name, Title, and ToString return null, ShortenedName returns null, IsNameShortened is false,
        /// and TypeName returns "NamedNode".
        /// </summary>
        [Fact]
        public void Constructor_DefaultProperties_ReturnsExpectedDefaults()
        {
            // Arrange & Act
            var node = new NamedNode();

            // Assert
            Assert.Null(node.Name);
            Assert.Null(node.Title);
            Assert.Null(node.ToString());
            Assert.Null(node.ShortenedName);
            Assert.False(node.IsNameShortened);
            Assert.Equal("NamedNode", node.TypeName);
        }

        /// <summary>
        /// Tests that setting the Name property updates Title and ToString properties accordingly.
        /// Expected: Title and ToString return the same value as Name, and TypeName remains "NamedNode".
        /// </summary>
        /// <param name="name">Input name value.</param>
        [Theory]
        [InlineData("TestNode")]
        [InlineData("")]
        public void SetName_ValidValue_UpdatesTitleAndToString(string name)
        {
            // Arrange
            var node = new NamedNode { Name = name };

            // Act
            var title = node.Title;
            var toString = node.ToString();

            // Assert
            Assert.Equal(name, title);
            Assert.Equal(name, toString);
            Assert.Equal("NamedNode", node.TypeName);
        }

        /// <summary>
        /// Tests the ShortenedName property when Name is a short string that is unlikely to be shortened.
        /// Expected: ShortenedName returns the same value as Name, and IsNameShortened is false.
        /// </summary>
        [Fact]
        public void ShortenedName_WhenNameIsShort_ReturnsSameValueAndNotShortened()
        {
            // Arrange
            const string shortName = "Short";
            var node = new NamedNode { Name = shortName };

            // Act
            var shortened = node.ShortenedName;
            var isShortened = node.IsNameShortened;

            // Assert
            // Assumption: For a short name, TextUtilities.ShortenValue returns the original value.
            Assert.Equal(shortName, shortened);
            Assert.False(isShortened);
        }

        /// <summary>
        /// Tests the ShortenedName property when Name is null.
        /// Expected: ShortenedName returns null and IsNameShortened is false.
        /// </summary>
        [Fact]
        public void ShortenedName_WhenNameIsNull_ReturnsNullAndNotShortened()
        {
            // Arrange
            var node = new NamedNode { Name = null };

            // Act
            var shortened = node.ShortenedName;
            var isShortened = node.IsNameShortened;

            // Assert
            Assert.Null(shortened);
            Assert.False(isShortened);
        }

        /// <summary>
        /// Tests the ShortenedName property for a long Name value that is expected to be shortened.
        /// Expected: The ShortenedName value should differ from the original Name, and IsNameShortened is true.
        /// Note: This test assumes that TextUtilities.ShortenValue shortens excessively long values.
        /// </summary>
        [Fact]
        public void ShortenedName_WhenNameIsLong_ReturnsShortenedValueAndMarkedAsShortened()
        {
            // Arrange
            const string longName = "ThisIsAnExcessivelyLongNodeNameThatShouldBeShortenedForDisplayPurposes";
            var node = new NamedNode { Name = longName };

            // Act
            var shortened = node.ShortenedName;
            var isShortened = node.IsNameShortened;

            // Assert
            // For a long name, we expect the shortened value to differ from the original.
            Assert.NotEqual(longName, shortened);
            Assert.True(isShortened);
        }
    }
}

using System;
using Microsoft.Build.Logging.StructuredLogger;
using Xunit;

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="NameValueNode"/> class.
    /// </summary>
    public class NameValueNodeTests
    {
        private readonly NameValueNode _node;

        /// <summary>
        /// Initializes a new instance of the <see cref="NameValueNodeTests"/> class.
        /// </summary>
        public NameValueNodeTests()
        {
            _node = new NameValueNode();
        }

        /// <summary>
        /// Tests that the NameAndEquals property returns the concatenation of Name and " = ".
        /// </summary>
        [Fact]
        public void NameAndEquals_WhenNameIsSet_ReturnsNameConcatEquals()
        {
            // Arrange
            string testName = "TestName";
            _node.Name = testName;

            // Act
            string result = _node.NameAndEquals;

            // Assert
            Assert.Equal(testName + " = ", result);
        }

        /// <summary>
        /// Tests that the Title property returns the same value as Name.
        /// </summary>
        [Fact]
        public void Title_WhenNameIsSet_ReturnsSameAsName()
        {
            // Arrange
            string testName = "SampleTitle";
            _node.Name = testName;

            // Act
            string title = _node.Title;

            // Assert
            Assert.Equal(testName, title);
        }

        /// <summary>
        /// Tests that the TypeName property always returns "NameValueNode".
        /// </summary>
        [Fact]
        public void TypeName_Always_ReturnsNameValueNode()
        {
            // Act
            string typeName = _node.TypeName;

            // Assert
            Assert.Equal("NameValueNode", typeName);
        }

        /// <summary>
        /// Tests that the ToString method returns the correct concatenation of Name, " = ", and Value.
        /// </summary>
        [Fact]
        public void ToString_WhenNameAndValueAreSet_ReturnsConcatenatedString()
        {
            // Arrange
            string testName = "MyName";
            string testValue = "MyValue";
            _node.Name = testName;
            _node.Value = testValue;

            // Act
            string result = _node.ToString();

            // Assert
            Assert.Equal(testName + " = " + testValue, result);
        }

        /// <summary>
        /// Tests that the GetFullText method returns the same value as ToString.
        /// </summary>
        [Fact]
        public void GetFullText_WhenCalled_ReturnsSameAsToString()
        {
            // Arrange
            string testName = "FullTextName";
            string testValue = "FullTextValue";
            _node.Name = testName;
            _node.Value = testValue;
            string expected = _node.ToString();

            // Act
            string result = _node.GetFullText();

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Tests the ShortenedValue and IsValueShortened properties when Value is a short string.
        /// Assumes that TextUtilities.ShortenValue returns the original string when no shortening is needed.
        /// </summary>
        [Fact]
        public void ShortenedValue_WhenValueIsShort_ReturnsOriginalValueAndIsValueShortenedIsFalse()
        {
            // Arrange
            string shortValue = "Short";
            _node.Value = shortValue;

            // Act
            string shortened = _node.ShortenedValue;
            bool isShortened = _node.IsValueShortened;

            // Assert
            Assert.Equal(shortValue, shortened);
            Assert.False(isShortened);
        }

        /// <summary>
        /// Tests the ShortenedValue and IsValueShortened properties when Value is a long string.
        /// Since the behavior of TextUtilities.ShortenValue is external, this test verifies consistency:
        /// if the shortened value differs from the original value, then IsValueShortened should be true.
        /// </summary>
        [Fact]
        public void ShortenedValue_WhenValueIsLong_ChecksIsValueShortenedConsistency()
        {
            // Arrange
            // Using a long string for which shortening is expected to occur.
            string longValue = "This is a very long value that should be shortened by the utility method.";
            _node.Value = longValue;

            // Act
            string shortened = _node.ShortenedValue;
            bool isShortened = _node.IsValueShortened;

            // Assert
            if (longValue != shortened)
            {
                Assert.True(isShortened);
            }
            else
            {
                Assert.False(isShortened);
            }
        }

        /// <summary>
        /// Tests that the IsVisible property always returns true.
        /// </summary>
        [Fact]
        public void IsVisible_Always_ReturnsTrue()
        {
            // Act
            bool visible = _node.IsVisible;

            // Assert
            Assert.True(visible);
        }

        /// <summary>
        /// Tests that the IsExpanded property always returns true.
        /// </summary>
        [Fact]
        public void IsExpanded_Always_ReturnsTrue()
        {
            // Act
            bool expanded = _node.IsExpanded;

            // Assert
            Assert.True(expanded);
        }

        /// <summary>
        /// Tests that the ToString method handles a null Value gracefully.
        /// </summary>
        [Fact]
        public void ToString_WhenValueIsNull_ReturnsNameEqualsWithNoValue()
        {
            // Arrange
            string testName = "NullTest";
            _node.Name = testName;
            _node.Value = null;

            // Act
            string result = _node.ToString();

            // Assert
            Assert.Equal(testName + " = ", result);
        }

        /// <summary>
        /// Tests that the GetFullText method handles a null Value gracefully.
        /// </summary>
        [Fact]
        public void GetFullText_WhenValueIsNull_ReturnsNameEqualsWithNoValue()
        {
            // Arrange
            string testName = "NullFullTextTest";
            _node.Name = testName;
            _node.Value = null;

            // Act
            string result = _node.GetFullText();

            // Assert
            Assert.Equal(testName + " = ", result);
        }
    }
}

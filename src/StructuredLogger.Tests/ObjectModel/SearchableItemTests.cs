using Microsoft.Build.Logging.StructuredLogger;
using Moq;
using System;
using Xunit;

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="SearchableItem"/> class.
    /// </summary>
    public class SearchableItemTests
    {
        /// <summary>
        /// Tests that when an explicit search text is set, getting the SearchText property returns the explicit value,
        /// regardless of the base Text property value.
        /// </summary>
        [Fact]
        public void SearchText_GetExplicitValueSet_ReturnsExplicitValue()
        {
            // Arrange
            var searchableItem = new SearchableItem();
            const string explicitSearchText = "ExplicitValue";
            const string baseTextValue = "BaseTextValue";
            // Set the base Text property if available.
            searchableItem.Text = baseTextValue;
            searchableItem.SearchText = explicitSearchText;

            // Act
            string actual = searchableItem.SearchText;

            // Assert
            Assert.Equal(explicitSearchText, actual);
        }

        /// <summary>
        /// Tests that when search text is not explicitly set, the SearchText property returns the base Text property value.
        /// </summary>
        [Fact]
        public void SearchText_GetNotSet_ReturnsBaseTextValue()
        {
            // Arrange
            var searchableItem = new SearchableItem();
            const string baseTextValue = "BaseOnlyText";
            searchableItem.Text = baseTextValue;
            // Do not set SearchText so that the backing field remains null.

            // Act
            string actual = searchableItem.SearchText;

            // Assert
            Assert.Equal(baseTextValue, actual);
        }

        /// <summary>
        /// Tests that when the SearchText property is explicitly set to null, the getter returns the base Text property value.
        /// </summary>
        [Fact]
        public void SearchText_GetExplicitlySetToNull_ReturnsBaseTextValue()
        {
            // Arrange
            var searchableItem = new SearchableItem();
            const string baseTextValue = "FallbackBaseText";
            searchableItem.Text = baseTextValue;
            searchableItem.SearchText = "NonNullValue";
            // Now explicitly set SearchText to null.
            searchableItem.SearchText = null;

            // Act
            string actual = searchableItem.SearchText;

            // Assert
            Assert.Equal(baseTextValue, actual);
        }

        /// <summary>
        /// Tests that when both the backing field for SearchText and the base Text property are null,
        /// the SearchText getter returns null.
        /// </summary>
        [Fact]
        public void SearchText_WhenBothValuesAreNull_ReturnsNull()
        {
            // Arrange
            var searchableItem = new SearchableItem();
            // Ensure both SearchText backing field and base Text are null.
            searchableItem.SearchText = null;
            searchableItem.Text = null;

            // Act
            string actual = searchableItem.SearchText;

            // Assert
            Assert.Null(actual);
        }

        /// <summary>
        /// Tests that setting the SearchText property properly updates its backing field.
        /// </summary>
        [Fact]
        public void SearchText_SetValue_CanBeRetrievedCorrectly()
        {
            // Arrange
            var searchableItem = new SearchableItem();
            const string setValue = "TestSearchValue";

            // Act
            searchableItem.SearchText = setValue;
            string actual = searchableItem.SearchText;

            // Assert
            Assert.Equal(setValue, actual);
        }
    }
}

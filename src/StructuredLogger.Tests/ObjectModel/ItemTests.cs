using Microsoft.Build.Logging.StructuredLogger;
using Microsoft.Build.Logging.StructuredLogger.UnitTests;
using System;
using Xunit;

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref = "Item"/> class.
    /// </summary>
    public class ItemTests
    {
        private readonly Item _item;
        /// <summary>
        /// Initializes a new instance of the <see cref = "ItemTests"/> class.
        /// </summary>
        public ItemTests()
        {
            _item = new Item();
        }

        /// <summary>
        /// Tests that the TypeName property returns "Item" as expected.
        /// </summary>
//         [Fact] [Error] (31-43)CS1061 'Item' does not contain a definition for 'TypeName' and no accessible extension method 'TypeName' accepting a first argument of type 'Item' could be found (are you missing a using directive or an assembly reference?)
//         public void TypeName_Get_ReturnsItem()
//         {
//             // Arrange
//             string expectedTypeName = "Item";
//             // Act
//             string actualTypeName = _item.TypeName;
//             // Assert
//             Assert.Equal(expectedTypeName, actualTypeName);
//         }

        /// <summary>
        /// Tests that setting the Text property correctly updates the underlying Name property and returns the same value.
        /// </summary>
        /// <param name = "testValue">The value to set.</param>
        [Theory]
        [InlineData("TestValue")]
        [InlineData("")]
        [InlineData(null)]
        public void Text_SetAndGet_ReturnsSameValue(string testValue)
        {
            // Act
            _item.Text = testValue;
            string actualValue = _item.Text;
            // Assert
            Assert.Equal(testValue, actualValue);
        }
    }

    /// <summary>
    /// Unit tests for the <see cref = "FileCopy"/> class.
    /// </summary>
    public class FileCopyTests
    {
        private readonly FileCopy _fileCopy;
        /// <summary>
        /// Initializes a new instance of the <see cref = "FileCopyTests"/> class.
        /// </summary>
        public FileCopyTests()
        {
            _fileCopy = new FileCopy();
        }

        /// <summary>
        /// Tests that the Kind property can be set and retrieved correctly.
        /// </summary>
        /// <param name = "kindValue">The kind value to assign.</param>
        [Theory]
        [InlineData("Copy")]
        [InlineData("")]
        [InlineData(null)]
        public void Kind_SetAndGet_ReturnsSameValue(string kindValue)
        {
            // Act
            _fileCopy.Kind = kindValue;
            string actualValue = _fileCopy.Kind;
            // Assert
            Assert.Equal(kindValue, actualValue);
        }

        /// <summary>
        /// Tests that the inherited Text property behaves correctly when set and retrieved.
        /// </summary>
        /// <param name = "textValue">The text value to assign.</param>
        [Theory]
        [InlineData("FileText")]
        [InlineData("")]
        [InlineData(null)]
        public void Text_SetAndGet_InheritedFromItem_ReturnsSameValue(string textValue)
        {
            // Act
            _fileCopy.Text = textValue;
            string actualValue = _fileCopy.Text;
            // Assert
            Assert.Equal(textValue, actualValue);
        }

        /// <summary>
        /// Tests that the inherited TypeName property returns "Item" in the FileCopy instance.
        /// </summary>
        [Fact]
        public void TypeName_Get_Inherited_ReturnsItem()
        {
            // Arrange
            string expectedTypeName = "Item";
            // Act
            string actualTypeName = _fileCopy.TypeName;
            // Assert
            Assert.Equal(expectedTypeName, actualTypeName);
        }
    }
}
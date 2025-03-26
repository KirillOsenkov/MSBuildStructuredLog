using Microsoft.Build.Logging.StructuredLogger;
using System;
using System.Reflection;
using Xunit;

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="RemoveItem"/> class.
    /// </summary>
    public class RemoveItemTests
    {
        private readonly RemoveItem _removeItem;

        /// <summary>
        /// Initializes a new instance of the <see cref="RemoveItemTests"/> class.
        /// </summary>
        public RemoveItemTests()
        {
            _removeItem = new RemoveItem();
        }

        /// <summary>
        /// Tests that the constructor of <see cref="RemoveItem"/> sets the DisableChildrenCache property to true.
        /// This is verified by using reflection to access the property since it might be declared in the base class.
        /// Expected outcome is that the property value will be true after instantiation.
        /// </summary>
        [Fact]
        public void Constructor_WhenCalled_SetsDisableChildrenCacheToTrue()
        {
            // Arrange is performed during instantiation in the constructor

            // Act
            // Use reflection to get the "DisableChildrenCache" property.
            PropertyInfo disableChildrenCacheProperty = typeof(RemoveItem)
                .GetProperty("DisableChildrenCache", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(disableChildrenCacheProperty);

            object value = disableChildrenCacheProperty.GetValue(_removeItem);

            // Assert
            Assert.IsType<bool>(value);
            Assert.True((bool)value, "The DisableChildrenCache property should be set to true by the constructor.");
        }

        /// <summary>
        /// Tests that the TypeName property returns "RemoveItem".
        /// Expected outcome is that the getter returns the name of the class.
        /// </summary>
        [Fact]
        public void TypeName_Get_ReturnsRemoveItem()
        {
            // Arrange is already handled in the constructor

            // Act
            string typeName = _removeItem.TypeName;

            // Assert
            Assert.Equal("RemoveItem", typeName);
        }

        /// <summary>
        /// Tests that the LineNumber property initially returns null.
        /// Expected outcome is that the default value of LineNumber should be null.
        /// </summary>
        [Fact]
        public void LineNumber_DefaultValue_IsNull()
        {
            // Act
            int? lineNumber = _removeItem.LineNumber;

            // Assert
            Assert.Null(lineNumber);
        }

        /// <summary>
        /// Tests that the LineNumber property can be set to a valid integer value and that the same value is returned.
        /// Expected outcome is that after setting the property, the getter returns the assigned value.
        /// </summary>
        [Fact]
        public void LineNumber_SetValue_ReturnsSameValue()
        {
            // Arrange
            int testLineNumber = 42;

            // Act
            _removeItem.LineNumber = testLineNumber;

            // Assert
            Assert.Equal(testLineNumber, _removeItem.LineNumber);
        }

        /// <summary>
        /// Tests that the LineNumber property can be reset to null after being assigned a value.
        /// Expected outcome is that the property correctly handles null assignment.
        /// </summary>
        [Fact]
        public void LineNumber_SetToNull_ReturnsNull()
        {
            // Arrange
            _removeItem.LineNumber = 100;

            // Act
            _removeItem.LineNumber = null;

            // Assert
            Assert.Null(_removeItem.LineNumber);
        }
    }
}

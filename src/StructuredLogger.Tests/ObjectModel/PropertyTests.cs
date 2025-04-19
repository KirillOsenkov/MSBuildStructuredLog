using System;
using Microsoft.Build.Logging.StructuredLogger;
using Xunit;

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="Property"/> class.
    /// </summary>
    public class PropertyTests
    {
        private readonly Property _property;

        public PropertyTests()
        {
            // Arrange: Instantiate the Property class.
            _property = new Property();
        }

        /// <summary>
        /// Tests that the TypeName getter of Property returns the expected class name.
        /// </summary>
        [Fact]
        public void TypeName_WhenCalled_ReturnsProperty()
        {
            // Act
            string typeName = _property.TypeName;

            // Assert
            Assert.Equal(nameof(Property), typeName);
        }
    }

    /// <summary>
    /// Unit tests for the <see cref="TaskParameterProperty"/> class.
    /// </summary>
    public class TaskParameterPropertyTests
    {
        private readonly TaskParameterProperty _taskParameterProperty;

        public TaskParameterPropertyTests()
        {
            // Arrange: Instantiate TaskParameterProperty.
            _taskParameterProperty = new TaskParameterProperty();
        }

        /// <summary>
        /// Tests that the ParameterName property correctly stores and returns a non-null value.
        /// </summary>
        [Fact]
        public void ParameterName_SetToNonNullValue_ReturnsExpectedValue()
        {
            // Arrange
            string expectedValue = "TestParameter";

            // Act
            _taskParameterProperty.ParameterName = expectedValue;
            string actualValue = _taskParameterProperty.ParameterName;

            // Assert
            Assert.Equal(expectedValue, actualValue);
        }

        /// <summary>
        /// Tests that the ParameterName property can be set to null and returns null.
        /// </summary>
        [Fact]
        public void ParameterName_SetToNull_ReturnsNull()
        {
            // Arrange
            string expectedValue = null;

            // Act
            _taskParameterProperty.ParameterName = expectedValue;
            string actualValue = _taskParameterProperty.ParameterName;

            // Assert
            Assert.Null(actualValue);
        }

        /// <summary>
        /// Tests that the inherited TypeName property returns the expected value from the base Property class,
        /// ensuring proper inheritance behavior.
        /// </summary>
        [Fact]
        public void TypeName_WhenCalledOnTaskParameterProperty_ReturnsProperty()
        {
            // Act
            string typeName = _taskParameterProperty.TypeName;

            // Assert
            Assert.Equal(nameof(Property), typeName);
        }
    }
}

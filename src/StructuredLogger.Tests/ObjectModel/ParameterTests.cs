using Microsoft.Build.Logging.StructuredLogger;
using System;
using Xunit;

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="Parameter"/> class.
    /// </summary>
    public class ParameterTests
    {
        private readonly Parameter _parameter;

        /// <summary>
        /// Initializes a new instance of the <see cref="ParameterTests"/> class.
        /// </summary>
        public ParameterTests()
        {
            _parameter = new Parameter();
        }

        /// <summary>
        /// Tests that the TypeName property returns the expected value.
        /// </summary>
        [Fact]
        public void TypeName_WhenCalled_ReturnsParameter()
        {
            // Act
            string typeName = _parameter.TypeName;

            // Assert
            Assert.Equal("Parameter", typeName);
        }

        /// <summary>
        /// Tests that the default value of ParameterName property is null.
        /// </summary>
        [Fact]
        public void ParameterName_DefaultValue_IsNull()
        {
            // Act
            string parameterName = _parameter.ParameterName;

            // Assert
            Assert.Null(parameterName);
        }

        /// <summary>
        /// Tests that the ParameterName property can be set to a valid non-null string and retrieved correctly.
        /// </summary>
        [Fact]
        public void ParameterName_WhenSetWithValidValue_ReturnsSameValue()
        {
            // Arrange
            const string expectedName = "TestParameter";

            // Act
            _parameter.ParameterName = expectedName;
            string actualName = _parameter.ParameterName;

            // Assert
            Assert.Equal(expectedName, actualName);
        }

        /// <summary>
        /// Tests that the ParameterName property can be set to null without throwing an exception.
        /// </summary>
        [Fact]
        public void ParameterName_WhenSetToNull_DoesNotThrowAndReturnsNull()
        {
            // Arrange
            const string expectedName = null;

            // Act
            _parameter.ParameterName = expectedName;
            string actualName = _parameter.ParameterName;

            // Assert
            Assert.Null(actualName);
        }
    }
}

using Microsoft.Build.Logging.StructuredLogger;
using System;
using Xunit;

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="Warning"/> class.
    /// </summary>
    public class WarningTests
    {
        private readonly Warning _warning;

        /// <summary>
        /// Initializes a new instance of the <see cref="WarningTests"/> class.
        /// </summary>
        public WarningTests()
        {
            _warning = new Warning();
        }

        /// <summary>
        /// When the TypeName property is accessed, it should return "Warning".
        /// This test validates that the property getter returns the expected string value.
        /// </summary>
        [Fact]
        public void TypeName_WhenAccessed_ReturnsWarning()
        {
            // Act
            string typeName = _warning.TypeName;

            // Assert
            Assert.Equal("Warning", typeName);
        }

        /// <summary>
        /// When the TypeName property is accessed multiple times, it should consistently return "Warning".
        /// This test validates that successive accesses do not alter the state or value returned.
        /// </summary>
        [Fact]
        public void TypeName_MultipleAccesses_ReturnsConsistentValue()
        {
            // Act
            string firstAccess = _warning.TypeName;
            string secondAccess = _warning.TypeName;

            // Assert
            Assert.Equal("Warning", firstAccess);
            Assert.Equal("Warning", secondAccess);
        }
    }
}

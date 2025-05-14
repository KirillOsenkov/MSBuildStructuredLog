using Microsoft.Build.Logging.StructuredLogger;
using System;
using Xunit;

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="Metadata"/> class.
    /// </summary>
    public class MetadataTests
    {
        private readonly Metadata _metadata;

        /// <summary>
        /// Initializes a new instance of the <see cref="MetadataTests"/> class.
        /// </summary>
        public MetadataTests()
        {
            _metadata = new Metadata();
        }

        /// <summary>
        /// Tests that the TypeName property returns the expected type name "Metadata".
        /// </summary>
        [Fact]
        public void TypeName_Get_WhenCalled_ReturnsMetadata()
        {
            // Act
            string actualTypeName = _metadata.TypeName;

            // Assert
            Assert.Equal("Metadata", actualTypeName);
        }
    }
}

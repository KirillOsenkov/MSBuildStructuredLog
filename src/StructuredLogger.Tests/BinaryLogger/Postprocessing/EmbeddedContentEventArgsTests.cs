using System;
using System.IO;
using Microsoft.Build.Logging.StructuredLogger;
using Xunit;

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="EmbeddedContentEventArgs"/> class.
    /// </summary>
    public class EmbeddedContentEventArgsTests
    {
        /// <summary>
        /// Tests the constructor of <see cref="EmbeddedContentEventArgs"/> with valid non-null parameters.
        /// Verifies that the ContentKind and ContentStream properties are assigned correctly.
        /// </summary>
        [Fact]
        public void Constructor_WithValidParameters_AssignsProperties()
        {
            // Arrange
            // Using a valid integer cast as BinaryLogRecordKind. Replace with a valid enum value if available.
            var expectedContentKind = (BinaryLogRecordKind)1;
            using var expectedStream = new MemoryStream();

            // Act
            var eventArgs = new EmbeddedContentEventArgs(expectedContentKind, expectedStream);

            // Assert
            Assert.Equal(expectedContentKind, eventArgs.ContentKind);
            Assert.Equal(expectedStream, eventArgs.ContentStream);
        }

        /// <summary>
        /// Tests the constructor of <see cref="EmbeddedContentEventArgs"/> when a null stream is provided.
        /// Verifies that the ContentStream property is set to null and ContentKind is assigned correctly.
        /// </summary>
        [Fact]
        public void Constructor_WithNullStream_AllowsNullContentStream()
        {
            // Arrange
            var expectedContentKind = (BinaryLogRecordKind)2; // Using a different value for clarity.

            // Act
            var eventArgs = new EmbeddedContentEventArgs(expectedContentKind, null);

            // Assert
            Assert.Equal(expectedContentKind, eventArgs.ContentKind);
            Assert.Null(eventArgs.ContentStream);
        }
    }
}

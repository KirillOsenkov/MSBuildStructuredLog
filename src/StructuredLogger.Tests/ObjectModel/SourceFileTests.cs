using System;
using Microsoft.Build.Logging.StructuredLogger;
using Xunit;

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="SourceFile"/> class.
    /// </summary>
    public class SourceFileTests
    {
        private readonly SourceFile _sourceFile;

        /// <summary>
        /// Initializes a new instance of the <see cref="SourceFileTests"/> class.
        /// </summary>
        public SourceFileTests()
        {
            _sourceFile = new SourceFile();
        }

        /// <summary>
        /// Tests that the <see cref="SourceFile.SourceFilePath"/> property returns the expected value after being set.
        /// This test covers typical string values including regular strings, empty string and null.
        /// </summary>
        /// <param name="expectedPath">The path value to set and verify.</param>
        [Theory]
        [InlineData("C:\\temp\\file.txt")]
        [InlineData("")]
        [InlineData(null)]
        public void SourceFilePath_SetAndGet_ReturnsSetValue(string expectedPath)
        {
            // Arrange
            _sourceFile.SourceFilePath = expectedPath;

            // Act
            var actualPath = _sourceFile.SourceFilePath;

            // Assert
            Assert.Equal(expectedPath, actualPath);
        }

        /// <summary>
        /// Tests that the <see cref="SourceFile.TypeName"/> property always returns the string "SourceFile".
        /// </summary>
        [Fact]
        public void TypeName_Get_ReturnsSourceFile()
        {
            // Act
            var typeName = _sourceFile.TypeName;

            // Assert
            Assert.Equal("SourceFile", typeName);
        }
    }
}

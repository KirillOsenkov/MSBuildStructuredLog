using System;
using Xunit;
using Microsoft.Build.Logging.StructuredLogger;

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="Package"/> class.
    /// </summary>
    public class PackageTests
    {
        private readonly Package _package;

        /// <summary>
        /// Initializes a new instance of the <see cref="PackageTests"/> class.
        /// </summary>
        public PackageTests()
        {
            _package = new Package();
            // Assuming that the inherited property Name is publicly settable.
            _package.Name = "DefaultName";
        }

        /// <summary>
        /// Tests that the TypeName property returns "Package".
        /// </summary>
        [Fact]
        public void TypeName_WhenAccessed_ReturnsPackage()
        {
            // Arrange
            // Act
            string typeName = _package.TypeName;
            // Assert
            Assert.Equal("Package", typeName);
        }

        /// <summary>
        /// Tests that ToString returns only the Name when both Version and VersionSpec are null.
        /// </summary>
        [Fact]
        public void ToString_NoVersionOrVersionSpec_ReturnsNameOnly()
        {
            // Arrange
            _package.Name = "TestPackage";
            _package.Version = null;
            _package.VersionSpec = null;
            string expected = "TestPackage";
            // Act
            string result = _package.ToString();
            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Tests that ToString returns the correct string when only the Version is provided.
        /// </summary>
        [Fact]
        public void ToString_OnlyVersionProvided_ReturnsNameAndVersion()
        {
            // Arrange
            _package.Name = "TestPackage";
            _package.Version = "1.0";
            _package.VersionSpec = null;
            string expected = "TestPackage 1.0";
            // Act
            string result = _package.ToString();
            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Tests that ToString returns the correct string when only the VersionSpec is provided.
        /// </summary>
        [Fact]
        public void ToString_OnlyVersionSpecProvided_ReturnsNameAndVersionSpec()
        {
            // Arrange
            _package.Name = "TestPackage";
            _package.Version = null;
            _package.VersionSpec = ">=1.0";
            string expected = "TestPackage >=1.0";
            // Act
            string result = _package.ToString();
            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Tests that ToString returns the correct combined string when both Version and VersionSpec are provided.
        /// </summary>
        [Fact]
        public void ToString_BothVersionAndVersionSpecProvided_ReturnsCombinedString()
        {
            // Arrange
            _package.Name = "TestPackage";
            _package.Version = "1.0";
            _package.VersionSpec = ">=1.0";
            string expected = "TestPackage 1.0 >=1.0";
            // Act
            string result = _package.ToString();
            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Tests that ToString handles a null Name appropriately when Version and VersionSpec are provided.
        /// </summary>
        [Fact]
        public void ToString_NullNameWithVersionAndVersionSpec_ReturnsVersionInformationWithLeadingSpace()
        {
            // Arrange
            _package.Name = null;
            _package.Version = "1.0";
            _package.VersionSpec = ">=1.0";
            // Expected behavior: Since Name is null, the result of ToString starts with "null" as an empty string.
            // In the implementation, it will use the null value in the string interpolation,
            // resulting in " 1.0 >=1.0".
            string expected = " 1.0 >=1.0";
            // Act
            string result = _package.ToString();
            // Assert
            Assert.Equal(expected, result);
        }
    }
}

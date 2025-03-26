using System;
using System.IO;
using Microsoft.Build.Logging.StructuredLogger;
using Xunit;

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="PathUtils"/> class.
    /// </summary>
    public class PathUtilsTests
    {
        /// <summary>
        /// Validates that the RootPath and TempPath static fields are set correctly.
        /// </summary>
        [Fact]
        public void RootPathAndTempPath_ShouldHaveCorrectStructure()
        {
            // Arrange
            string expectedTempPath = Path.Combine(PathUtils.RootPath, "Temp");

            // Act
            string actualRootPath = PathUtils.RootPath;
            string actualTempPath = PathUtils.TempPath;

            // Assert
            Assert.False(string.IsNullOrWhiteSpace(actualRootPath));
            Assert.False(string.IsNullOrWhiteSpace(actualTempPath));
            Assert.Equal(expectedTempPath, actualTempPath);
        }

        /// <summary>
        /// Tests the IsExtended method with valid extended paths.
        /// </summary>
        /// <param name="path">The extended path to test.</param>
        [Theory]
        [InlineData(@"\\?\C:\Folder")]
        [InlineData(@"\\?\D:\")]
        public void IsExtended_ValidExtendedPath_ReturnsTrue(string path)
        {
            // Act
            bool result = PathUtils.IsExtended(path);

            // Assert
            Assert.True(result, $"Expected IsExtended to return true for path: {path}");
        }

        /// <summary>
        /// Tests the IsExtended method with non-extended paths.
        /// </summary>
        /// <param name="path">The path to test.</param>
        [Theory]
        [InlineData(@"C:\Folder")]
        [InlineData(@"\\.\C:\Folder")]
        [InlineData(@"//?/C:/Folder")]
        [InlineData("")]
        [InlineData(" ")]
        public void IsExtended_NonExtendedPath_ReturnsFalse(string path)
        {
            // Act
            bool result = PathUtils.IsExtended(path);

            // Assert
            Assert.False(result, $"Expected IsExtended to return false for path: {path}");
        }

        /// <summary>
        /// Tests the HasInvalidVolumeSeparator method with valid drive specifier paths.
        /// </summary>
        /// <param name="path">The path to test.</param>
        /// <param name="expected">The expected result.</param>
        [Theory]
        [InlineData(@"C:\Folder", false)]
        [InlineData(@"\\?\C:\Folder", false)]
        [InlineData(@"  C:\Folder", false)]
        public void HasInvalidVolumeSeparator_ValidPaths_ReturnsFalse(string path, bool expected)
        {
            // Act
            bool result = PathUtils.HasInvalidVolumeSeparator(path);

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Tests the HasInvalidVolumeSeparator method with invalid volume separator usage.
        /// </summary>
        /// <param name="path">The path to test.</param>
        /// <param name="expected">The expected result.</param>
        [Theory]
        [InlineData(@" :Folder", true)]   // Starts with a colon after spaces.
        [InlineData(@"1:\Folder", true)]   // Invalid drive letter ('1').
        [InlineData(@"C::\Folder", true)]   // Extra colon in the path.
        [InlineData(@"  :Folder", true)]    // Leading spaces followed by colon.
        public void HasInvalidVolumeSeparator_InvalidPaths_ReturnsTrue(string path, bool expected)
        {
            // Act
            bool result = PathUtils.HasInvalidVolumeSeparator(path);

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Tests the IsDirectorySeparator method with various characters.
        /// </summary>
        /// <param name="input">Character to test.</param>
        /// <param name="expected">Expected boolean outcome.</param>
        [Theory]
        [InlineData('\\', true)]
        [InlineData('/', true)]
        [InlineData('-', false)]
        [InlineData('A', false)]
        public void IsDirectorySeparator_VariousCharacters_ReturnsExpected(char input, bool expected)
        {
            // Act
            bool result = PathUtils.IsDirectorySeparator(input);

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Tests the IsValidDriveChar method with various characters.
        /// </summary>
        /// <param name="input">Character to test.</param>
        /// <param name="expected">Expected boolean outcome.</param>
        [Theory]
        [InlineData('C', true)]
        [InlineData('z', true)]
        [InlineData('1', false)]
        [InlineData('-', false)]
        public void IsValidDriveChar_VariousCharacters_ReturnsExpected(char input, bool expected)
        {
            // Act
            bool result = PathUtils.IsValidDriveChar(input);

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Tests the PathStartSkip method with various string inputs.
        /// </summary>
        /// <param name="input">Input path string.</param>
        /// <param name="expected">Expected starting index after spaces are skipped.</param>
        [Theory]
        [InlineData("  C:\\Folder", 2)] // Leading spaces, then valid drive letter and colon.
        [InlineData("Folder", 0)]       // No leading spaces.
        [InlineData("   ", 0)]          // All spaces should return 0.
        public void PathStartSkip_VariousInputs_ReturnsExpectedIndex(string input, int expected)
        {
            // Act
            int result = PathUtils.PathStartSkip(input);

            // Assert
            Assert.Equal(expected, result);
        }
    }
}

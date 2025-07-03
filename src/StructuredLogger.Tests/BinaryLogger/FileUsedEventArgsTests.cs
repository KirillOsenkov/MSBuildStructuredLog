using System;
using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.Build.Framework.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="FileUsedEventArgs"/> class.
    /// </summary>
    public class FileUsedEventArgsTests
    {
        /// <summary>
        /// Tests that the default constructor initializes FilePath to null.
        /// </summary>
        [Fact]
        public void Constructor_Default_InitializesFilePathToNull()
        {
            // Arrange & Act
            var eventArgs = new FileUsedEventArgs();

            // Assert
            Assert.Null(eventArgs.FilePath);
        }

        /// <summary>
        /// Tests that the parameterized constructor sets the FilePath property to the provided value.
        /// </summary>
        /// <param name="filePath">The file path to set.</param>
        [Theory]
        [InlineData("response.txt")]
        [InlineData("C:\\temp\\response.txt")]
        [InlineData("")]
        public void Constructor_WithFilePath_SetsFilePath(string filePath)
        {
            // Arrange & Act
            var eventArgs = new FileUsedEventArgs(filePath);

            // Assert
            Assert.Equal(filePath, eventArgs.FilePath);
        }

        /// <summary>
        /// Tests that the FilePath property can be updated post-construction.
        /// </summary>
        [Fact]
        public void FilePath_SetterGetter_UpdatesValueCorrectly()
        {
            // Arrange
            var eventArgs = new FileUsedEventArgs("initial.txt");

            // Act
            eventArgs.FilePath = "updated.txt";

            // Assert
            Assert.Equal("updated.txt", eventArgs.FilePath);
        }

        /// <summary>
        /// Tests that the parameterized constructor accepts a null value for FilePath.
        /// </summary>
        [Fact]
        public void Constructor_WithNullFilePath_SetsFilePathToNull()
        {
            // Arrange & Act
            var eventArgs = new FileUsedEventArgs(null);

            // Assert
            Assert.Null(eventArgs.FilePath);
        }
    }
}

using System;
using Microsoft.Build.Logging.StructuredLogger;
using Xunit;

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="FileCopyOperation"/> class.
    /// </summary>
    public class FileCopyOperationTests
    {
        /// <summary>
        /// Tests that the <see cref="FileCopyOperation.ToString"/> method returns the correctly formatted string 
        /// when both Source and Destination properties are non-null.
        /// Expected outcome: The returned string should be "{Source} ➔ {Destination}".
        /// </summary>
        [Fact]
        public void ToString_WithValidSourceAndDestination_ReturnsFormattedString()
        {
            // Arrange
            var fileCopy = new FileCopyOperation
            {
                Source = "C:\\Source.txt",
                Destination = "D:\\Destination.txt"
            };
            var expected = "C:\\Source.txt ➔ D:\\Destination.txt";

            // Act
            var result = fileCopy.ToString();

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Tests that the <see cref="FileCopyOperation.ToString"/> method handles a null Source property correctly.
        /// Expected outcome: The returned string should have an empty string in place of Source.
        /// </summary>
        [Fact]
        public void ToString_WithNullSource_ReturnsFormattedStringWithEmptySource()
        {
            // Arrange
            var fileCopy = new FileCopyOperation
            {
                Source = null,
                Destination = "D:\\Destination.txt"
            };
            var expected = " ➔ D:\\Destination.txt";

            // Act
            var result = fileCopy.ToString();

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Tests that the <see cref="FileCopyOperation.ToString"/> method handles a null Destination property correctly.
        /// Expected outcome: The returned string should have an empty string in place of Destination.
        /// </summary>
        [Fact]
        public void ToString_WithNullDestination_ReturnsFormattedStringWithEmptyDestination()
        {
            // Arrange
            var fileCopy = new FileCopyOperation
            {
                Source = "C:\\Source.txt",
                Destination = null
            };
            var expected = "C:\\Source.txt ➔ ";

            // Act
            var result = fileCopy.ToString();

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Tests that the <see cref="FileCopyOperation.ToString"/> method handles null values for both Source and Destination correctly.
        /// Expected outcome: The returned string should have empty strings for both Source and Destination.
        /// </summary>
        [Fact]
        public void ToString_WithBothSourceAndDestinationNull_ReturnsFormattedStringWithEmptyValues()
        {
            // Arrange
            var fileCopy = new FileCopyOperation
            {
                Source = null,
                Destination = null
            };
            var expected = " ➔ ";

            // Act
            var result = fileCopy.ToString();

            // Assert
            Assert.Equal(expected, result);
        }
    }
}

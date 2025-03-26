using System;
using Microsoft.Build.Logging.StructuredLogger;
using Xunit;

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="Span"/> struct.
    /// </summary>
    public class SpanTests
    {
        /// <summary>
        /// Tests that the constructor correctly initializes the Start, Length, and End properties.
        /// </summary>
        /// <param name="start">The start value to initialize the span with.</param>
        /// <param name="length">The length value to initialize the span with.</param>
        [Theory]
        [InlineData(0, 0)]
        [InlineData(5, 10)]
        [InlineData(-3, 7)]
        public void Constructor_ShouldInitializePropertiesAndEndCorrectly(int start, int length)
        {
            // Arrange & Act
            var span = new Span(start, length);

            // Assert
            Assert.Equal(start, span.Start);
            Assert.Equal(length, span.Length);
            Assert.Equal(start + length, span.End);
        }

        /// <summary>
        /// Tests that the ToString method returns the expected string format.
        /// </summary>
        /// <param name="start">The start value for the span.</param>
        /// <param name="length">The length value for the span.</param>
        /// <param name="expectedString">The expected string output.</param>
        [Theory]
        [InlineData(0, 0, "(0, 0)")]
        [InlineData(5, 10, "(5, 10)")]
        [InlineData(-3, 7, "(-3, 7)")]
        public void ToString_ReturnsExpectedFormat(int start, int length, string expectedString)
        {
            // Arrange
            var span = new Span(start, length);

            // Act
            var result = span.ToString();

            // Assert
            Assert.Equal(expectedString, result);
        }

        /// <summary>
        /// Tests that the Skip method returns an adjusted span when the skip value is less than the span's length.
        /// </summary>
        /// <param name="originalStart">Original start value of the span.</param>
        /// <param name="originalLength">Original length of the span.</param>
        /// <param name="skipValue">The amount to skip.</param>
        /// <param name="expectedStart">The expected start of the returned span.</param>
        /// <param name="expectedLength">The expected length of the returned span.</param>
        [Theory]
        [InlineData(5, 10, 3, 8, 7)]
        [InlineData(0, 5, 2, 2, 3)]
        public void Skip_WhenSkipLessThanLength_ReturnsAdjustedSpan(int originalStart, int originalLength, int skipValue, int expectedStart, int expectedLength)
        {
            // Arrange
            var span = new Span(originalStart, originalLength);

            // Act
            var skipped = span.Skip(skipValue);

            // Assert
            Assert.Equal(expectedStart, skipped.Start);
            Assert.Equal(expectedLength, skipped.Length);
            Assert.Equal(expectedStart + expectedLength, skipped.End);
        }

        /// <summary>
        /// Tests that the Skip method returns a span with zero length when the skip value equals the span's length.
        /// </summary>
        [Fact]
        public void Skip_WhenSkipEqualsLength_ReturnsSpanWithZeroLength()
        {
            // Arrange
            int start = 5;
            int length = 10;
            var span = new Span(start, length);

            // Act
            var skipped = span.Skip(length);

            // Assert
            Assert.Equal(start + length, skipped.Start);
            Assert.Equal(0, skipped.Length);
            Assert.Equal(start + length, skipped.End);
        }

        /// <summary>
        /// Tests that the Skip method returns an empty span when the skip value is greater than the span's length.
        /// </summary>
        /// <param name="originalStart">Original start value of the span.</param>
        /// <param name="originalLength">Original length of the span.</param>
        /// <param name="skipValue">The skip value greater than the span's length.</param>
        [Theory]
        [InlineData(5, 10, 11)]
        [InlineData(0, 0, 1)]
        public void Skip_WhenSkipGreaterThanLength_ReturnsEmptySpan(int originalStart, int originalLength, int skipValue)
        {
            // Arrange
            var span = new Span(originalStart, originalLength);

            // Act
            var skipped = span.Skip(skipValue);

            // Assert: An empty Span is equivalent to the default instance.
            Assert.Equal(0, skipped.Start);
            Assert.Equal(0, skipped.Length);
            Assert.Equal(0, skipped.End);
        }

        /// <summary>
        /// Tests that the ContainsEndInclusive method returns the expected result for various positions.
        /// </summary>
        /// <param name="start">The start value of the span.</param>
        /// <param name="length">The length value of the span.</param>
        /// <param name="testPosition">The position to test against the span.</param>
        /// <param name="expected">The expected result indicating whether the position is within the span (inclusive of end).</param>
        [Theory]
        [InlineData(5, 10, 5, true)]
        [InlineData(5, 10, 15, true)]
        [InlineData(5, 10, 4, false)]
        [InlineData(5, 10, 16, false)]
        public void ContainsEndInclusive_VariousValues_ReturnsExpectedResult(int start, int length, int testPosition, bool expected)
        {
            // Arrange
            var span = new Span(start, length);

            // Act
            var result = span.ContainsEndInclusive(testPosition);

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Tests that the Contains method returns the expected result for various positions.
        /// </summary>
        /// <param name="start">The start value of the span.</param>
        /// <param name="length">The length value of the span.</param>
        /// <param name="testPosition">The position to test against the span.</param>
        /// <param name="expected">The expected result indicating whether the position is within the span.</param>
        [Theory]
        [InlineData(5, 10, 5, true)]
        [InlineData(5, 10, 14, true)]
        [InlineData(5, 10, 15, false)]
        [InlineData(5, 10, 4, false)]
        [InlineData(5, 10, 16, false)]
        public void Contains_VariousValues_ReturnsExpectedResult(int start, int length, int testPosition, bool expected)
        {
            // Arrange
            var span = new Span(start, length);

            // Act
            var result = span.Contains(testPosition);

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Tests that the Empty static field returns a default Span.
        /// </summary>
        [Fact]
        public void Empty_ReturnsDefaultSpan()
        {
            // Act
            var emptySpan = Span.Empty;

            // Assert
            Assert.Equal(0, emptySpan.Start);
            Assert.Equal(0, emptySpan.Length);
            Assert.Equal(0, emptySpan.End);
        }
    }
}

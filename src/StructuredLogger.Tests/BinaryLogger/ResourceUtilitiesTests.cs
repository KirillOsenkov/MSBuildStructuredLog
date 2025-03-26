using System;
using Microsoft.Build.Shared;
using Xunit;

namespace Microsoft.Build.Shared.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="ResourceUtilities"/> class.
    /// </summary>
    public class ResourceUtilitiesTests
    {
        /// <summary>
        /// Tests that FormatResourceString returns the provided message and sets out parameters correctly when message is non-null.
        /// </summary>
        [Fact]
        public void FormatResourceString_WithNonNullMessage_ReturnsMessageAndSetsOutParameters()
        {
            // Arrange
            string expectedMessage = "TestMessage";
            string text = "dummyText";
            string filePath = "dummyFilePath";

            // Act
            string result = ResourceUtilities.FormatResourceString(out string errorCode, out string helpKeyword, text, filePath, expectedMessage);

            // Assert
            Assert.Equal("MSB0001", errorCode);
            Assert.Equal(string.Empty, helpKeyword);
            Assert.Equal(expectedMessage, result);
        }

        /// <summary>
        /// Tests that FormatResourceString returns null when the provided message is null, and still sets out parameters.
        /// </summary>
        [Fact]
        public void FormatResourceString_WithNullMessage_ReturnsNullAndSetsOutParameters()
        {
            // Arrange
            string expectedMessage = null;
            string text = "dummyText";
            string filePath = "dummyFilePath";

            // Act
            string result = ResourceUtilities.FormatResourceString(out string errorCode, out string helpKeyword, text, filePath, expectedMessage);

            // Assert
            Assert.Equal("MSB0001", errorCode);
            Assert.Equal(string.Empty, helpKeyword);
            Assert.Null(result);
        }

        /// <summary>
        /// Tests that FormatResourceStringStripCodeAndKeyword returns the provided message and sets out parameters correctly when message is non-null.
        /// </summary>
        [Fact]
        public void FormatResourceStringStripCodeAndKeyword_WithNonNullMessage_ReturnsMessageAndSetsOutParameters()
        {
            // Arrange
            string expectedMessage = "AnotherTestMessage";
            string text = "dummyText";
            string filePath = "dummyFilePath";

            // Act
            string result = ResourceUtilities.FormatResourceStringStripCodeAndKeyword(out string errorCode, out string helpKeyword, text, filePath, expectedMessage);

            // Assert
            Assert.Equal("MSB0001", errorCode);
            Assert.Equal(string.Empty, helpKeyword);
            Assert.Equal(expectedMessage, result);
        }

        /// <summary>
        /// Tests that FormatResourceStringStripCodeAndKeyword returns null when the provided message is null, and still sets out parameters.
        /// </summary>
        [Fact]
        public void FormatResourceStringStripCodeAndKeyword_WithNullMessage_ReturnsNullAndSetsOutParameters()
        {
            // Arrange
            string expectedMessage = null;
            string text = "dummyText";
            string filePath = "dummyFilePath";

            // Act
            string result = ResourceUtilities.FormatResourceStringStripCodeAndKeyword(out string errorCode, out string helpKeyword, text, filePath, expectedMessage);

            // Assert
            Assert.Equal("MSB0001", errorCode);
            Assert.Equal(string.Empty, helpKeyword);
            Assert.Null(result);
        }

        /// <summary>
        /// Tests that the overload FormatResourceString(string, string) returns the first parameter.
        /// </summary>
        /// <param name="v1">The first string value.</param>
        /// <param name="v2">The second string value.</param>
        [Theory]
        [InlineData("FirstValue", "SecondValue")]
        [InlineData("", "NonEmpty")]
        [InlineData(null, "Anything")]
        public void FormatResourceString_Overload_ReturnsFirstParameter(string v1, string v2)
        {
            // Act
            string result = ResourceUtilities.FormatResourceString(v1, v2);

            // Assert
            Assert.Equal(v1, result);
        }

        /// <summary>
        /// Tests that the overload FormatResourceStringStripCodeAndKeyword(string, string) returns the first parameter.
        /// </summary>
        /// <param name="v1">The first string value.</param>
        /// <param name="v2">The second string value.</param>
        [Theory]
        [InlineData("ValueOne", "ValueTwo")]
        [InlineData("", "AnotherValue")]
        [InlineData(null, "Something")]
        public void FormatResourceStringStripCodeAndKeyword_Overload_ReturnsFirstParameter(string v1, string v2)
        {
            // Act
            string result = ResourceUtilities.FormatResourceStringStripCodeAndKeyword(v1, v2);

            // Assert
            Assert.Equal(v1, result);
        }

        /// <summary>
        /// Tests that GetResourceString returns the same string that was provided.
        /// </summary>
        /// <param name="input">The resource key string.</param>
        [Theory]
        [InlineData("ResourceKey")]
        [InlineData("")]
        [InlineData(null)]
        public void GetResourceString_ReturnsInput(string input)
        {
            // Act
            string result = ResourceUtilities.GetResourceString(input);

            // Assert
            Assert.Equal(input, result);
        }
    }
}

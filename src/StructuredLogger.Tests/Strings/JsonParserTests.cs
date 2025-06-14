using System;
using System.Collections.Generic;
using TinyJson;
using Xunit;

namespace TinyJson.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="JSONParser"/> class.
    /// </summary>
    public class JSONParserTests
    {
        /// <summary>
        /// Tests that a valid JSON string literal is correctly parsed.
        /// </summary>
        /// <param name="json">The JSON input representing a string literal.</param>
        /// <param name="expected">The expected string after parsing.</param>
        [Theory]
        [InlineData("\"Hello\"", "Hello")]
        [InlineData("\"\"", "")]
        public void FromJson_WhenParsingString_ReturnsCorrectString(string json, string expected)
        {
            // Act
            string result = json.FromJson<string>();

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Tests that a valid JSON numeric literal is correctly parsed into an integer.
        /// </summary>
        /// <param name="json">The JSON input representing an integer.</param>
        /// <param name="expected">The expected integer value.</param>
        [Theory]
        [InlineData("123", 123)]
        [InlineData("-456", -456)]
        public void FromJson_WhenParsingInteger_ReturnsCorrectInteger(string json, int expected)
        {
            // Act
            int result = json.FromJson<int>();

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Tests that a valid JSON numeric literal with a floating point is correctly parsed into a double.
        /// </summary>
        /// <param name="json">The JSON input representing a floating point number.</param>
        /// <param name="expected">The expected double value.</param>
        [Theory]
        [InlineData("3.14", 3.14)]
        [InlineData("-2.718", -2.718)]
        public void FromJson_WhenParsingDouble_ReturnsCorrectDouble(string json, double expected)
        {
            // Act
            double result = json.FromJson<double>();

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Tests that a valid JSON boolean literal is correctly parsed.
        /// </summary>
        /// <param name="json">The JSON input representing a boolean.</param>
        /// <param name="expected">The expected boolean value.</param>
        [Theory]
        [InlineData("true", true)]
        [InlineData("false", false)]
        public void FromJson_WhenParsingBoolean_ReturnsCorrectBoolean(string json, bool expected)
        {
            // Act
            bool result = json.FromJson<bool>();

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Tests that parsing the JSON literal "null" returns null for reference types.
        /// </summary>
        [Fact]
        public void FromJson_WhenParsingNull_ReturnsNull()
        {
            // Act
            string resultString = "null".FromJson<string>();
            int? resultNullableInt = "null".FromJson<int?>();
            Dictionary<string, int> resultDictionary = "null".FromJson<Dictionary<string, int>>();

            // Assert
            Assert.Null(resultString);
            Assert.Null(resultNullableInt);
            Assert.Null(resultDictionary);
        }

        /// <summary>
        /// Tests that a valid JSON array is correctly parsed into an array of integers.
        /// </summary>
        [Fact]
        public void FromJson_WhenParsingJsonArrayToIntArray_ReturnsCorrectArray()
        {
            // Arrange
            string json = "[1,2,3]";

            // Act
            int[] result = json.FromJson<int[]>();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(new int[] { 1, 2, 3 }, result);
        }

        /// <summary>
        /// Tests that a valid JSON array is correctly parsed into a List of integers.
        /// </summary>
        [Fact]
        public void FromJson_WhenParsingJsonArrayToList_ReturnsCorrectList()
        {
            // Arrange
            string json = "[4,5,6]";

            // Act
            List<int> result = json.FromJson<List<int>>();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(new List<int> { 4, 5, 6 }, result);
        }

        /// <summary>
        /// Tests that a valid JSON object is correctly parsed into a Dictionary&lt;string, int&gt;.
        /// </summary>
        [Fact]
        public void FromJson_WhenParsingJsonObject_ReturnsCorrectDictionary()
        {
            // Arrange
            string json = "{\"a\":1,\"b\":2}";

            // Act
            Dictionary<string, int> result = json.FromJson<Dictionary<string, int>>();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.True(result.ContainsKey("a"));
            Assert.True(result.ContainsKey("b"));
            Assert.Equal(1, result["a"]);
            Assert.Equal(2, result["b"]);
        }

        /// <summary>
        /// An enum used to test the JSON parsing of enum values.
        /// </summary>
        public enum TestEnum
        {
            None,
            First,
            Second
        }

        /// <summary>
        /// Tests that a valid JSON string representing an enum is correctly parsed.
        /// </summary>
        /// <param name="json">The JSON input representing the enum. Can be with or without quotes.</param>
        /// <param name="expected">The expected enum value.</param>
        [Theory]
        [InlineData("\"First\"", TestEnum.First)]
        [InlineData("Second", TestEnum.Second)]
        public void FromJson_WhenParsingEnum_ReturnsCorrectEnum(string json, TestEnum expected)
        {
            // Act
            TestEnum result = json.FromJson<TestEnum>();

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// A test class representing a custom object for JSON parsing.
        /// </summary>
        public class Person
        {
            public string Name;
            public int Age;
        }

        /// <summary>
        /// Tests that a valid JSON object is correctly parsed into a custom object.
        /// </summary>
        [Fact]
        public void FromJson_WhenParsingCustomObject_ReturnsCorrectObject()
        {
            // Arrange
            string json = "{\"Name\":\"John\",\"Age\":30}";

            // Act
            Person result = json.FromJson<Person>();

            // Assert
            Assert.NotNull(result);
            Assert.Equal("John", result.Name);
            Assert.Equal(30, result.Age);
        }

        /// <summary>
        /// Tests that an invalid JSON input for an array returns null.
        /// </summary>
        [Fact]
        public void FromJson_WhenParsingInvalidArrayJson_ReturnsNull()
        {
            // Arrange
            string json = "1,2,3"; // Missing the surrounding brackets.

            // Act
            int[] result = json.FromJson<int[]>();

            // Assert
            Assert.Null(result);
        }

        /// <summary>
        /// Tests that an invalid JSON input for a dictionary returns null.
        /// </summary>
        [Fact]
        public void FromJson_WhenParsingInvalidDictionaryJson_ReturnsNull()
        {
            // Arrange
            string json = "{\"a\":1,\"b\":2"; // Missing closing brace.

            // Act
            Dictionary<string, int> result = json.FromJson<Dictionary<string, int>>();

            // Assert
            Assert.Null(result);
        }

        /// <summary>
        /// Tests that an empty JSON string returns null when parsing to non-string types.
        /// </summary>
        [Fact]
        public void FromJson_WhenParsingEmptyJson_ReturnsNullForNonStringTypes()
        {
            // Arrange
            string json = "";

            // Act
            int resultInt = json.FromJson<int>();
            object resultObject = json.FromJson<object>();

            // Assert
            Assert.Null(resultInt);
            Assert.Null(resultObject);
        }

        /// <summary>
        /// Tests that a JSON string with additional whitespace outside string literals is parsed correctly.
        /// </summary>
        [Fact]
        public void FromJson_WhenParsingJsonWithExtraWhitespace_ReturnsCorrectResult()
        {
            // Arrange
            string json = " {  \"Name\" : \"Alice\" , \"Age\" : 25 } ";

            // Act
            Person result = json.FromJson<Person>();

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Alice", result.Name);
            Assert.Equal(25, result.Age);
        }
    }
}

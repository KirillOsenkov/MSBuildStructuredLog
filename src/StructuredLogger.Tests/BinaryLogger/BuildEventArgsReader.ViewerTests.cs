using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Build.Logging.StructuredLogger;
using Xunit;

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="BuildEventArgsReader"/> class.
    /// </summary>
    public class BuildEventArgsReaderTests
    {
        /// <summary>
        /// Tests that GetStrings returns only strings from the internal records
        /// when the private field 'stringRecords' contains mixed types.
        /// Expected: Only string entries are returned.
        /// </summary>
//         [Fact] [Error] (25-30)CS7036 There is no argument given that corresponds to the required parameter 'binaryReader' of 'BuildEventArgsReader.BuildEventArgsReader(BinaryReader, int)'
//         public void GetStrings_WhenRecordsContainMixedTypes_ReturnsOnlyStrings()
//         {
//             // Arrange
//             var reader = new BuildEventArgsReader();
//             var mixedRecords = new ArrayList { "first", 100, "second", null, 3.14, "third" };
//             // Use reflection to set the private field "stringRecords"
//             FieldInfo field = typeof(BuildEventArgsReader).GetField("stringRecords", BindingFlags.Instance | BindingFlags.NonPublic);
//             Assert.NotNull(field);
//             field.SetValue(reader, mixedRecords);
// 
//             // Act
//             IEnumerable<string> result = reader.GetStrings();
// 
//             // Assert
//             var expected = new List<string> { "first", "second", "third" };
//             Assert.Equal(expected, result.ToList());
//         }

        /// <summary>
        /// Tests the FormatResourceStringIgnoreCodeAndKeyword overload with a single argument.
        /// Expected: The string is properly formatted using string.Format.
        /// </summary>
        [Theory]
        [InlineData("Hello {0}", "World", "Hello World")]
        public void FormatResourceStringIgnoreCodeAndKeyword_OneArg_ReturnsFormattedString(string resource, string arg, string expected)
        {
            // Act
            string result = BuildEventArgsReader.FormatResourceStringIgnoreCodeAndKeyword(resource, arg);

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Tests the FormatResourceStringIgnoreCodeAndKeyword overload with two arguments.
        /// Expected: The string is properly formatted using string.Format.
        /// </summary>
        [Theory]
        [InlineData("Sum of {0} and {1} equals", "2", "3", "Sum of 2 and 3 equals")]
        public void FormatResourceStringIgnoreCodeAndKeyword_TwoArgs_ReturnsFormattedString(string resource, string arg0, string arg1, string expected)
        {
            // Act
            string result = BuildEventArgsReader.FormatResourceStringIgnoreCodeAndKeyword(resource, arg0, arg1);

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Tests the FormatResourceStringIgnoreCodeAndKeyword overload with three arguments.
        /// Expected: The string is properly formatted using string.Format.
        /// </summary>
        [Theory]
        [InlineData("Coordinates: ({0}, {1}, {2})", "10", "20", "30", "Coordinates: (10, 20, 30)")]
        public void FormatResourceStringIgnoreCodeAndKeyword_ThreeArgs_ReturnsFormattedString(string resource, string arg0, string arg1, string arg2, string expected)
        {
            // Act
            string result = BuildEventArgsReader.FormatResourceStringIgnoreCodeAndKeyword(resource, arg0, arg1, arg2);

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Tests the FormatResourceStringIgnoreCodeAndKeyword overload that accepts a params array.
        /// Expected: The string is properly formatted using string.Format.
        /// </summary>
        [Theory]
        [InlineData("Values: {0}, {1}, {2}", "A", "B", "C", "Values: A, B, C")]
        public void FormatResourceStringIgnoreCodeAndKeyword_Params_ReturnsFormattedString(string resource, string arg0, string arg1, string arg2, string expected)
        {
            // Act
            string result = BuildEventArgsReader.FormatResourceStringIgnoreCodeAndKeyword(resource, new string[] { arg0, arg1, arg2 });

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Tests that FormatResourceStringIgnoreCodeAndKeyword overloads throw ArgumentNullException when the resource is null.
        /// Expected: An ArgumentNullException is thrown for each overload variant.
        /// </summary>
        [Fact]
        public void FormatResourceStringIgnoreCodeAndKeyword_NullResource_ThrowsArgumentNullException()
        {
            // Arrange
            string nullResource = null;
            string sampleArg = "Test";

            // Act & Assert for one-argument overload.
            Assert.Throws<ArgumentNullException>(() => BuildEventArgsReader.FormatResourceStringIgnoreCodeAndKeyword(nullResource, sampleArg));

            // Act & Assert for two-argument overload.
            Assert.Throws<ArgumentNullException>(() => BuildEventArgsReader.FormatResourceStringIgnoreCodeAndKeyword(nullResource, sampleArg, sampleArg));

            // Act & Assert for three-argument overload.
            Assert.Throws<ArgumentNullException>(() => BuildEventArgsReader.FormatResourceStringIgnoreCodeAndKeyword(nullResource, sampleArg, sampleArg, sampleArg));

            // Act & Assert for params overload.
            Assert.Throws<ArgumentNullException>(() => BuildEventArgsReader.FormatResourceStringIgnoreCodeAndKeyword(nullResource, new string[] { sampleArg, sampleArg }));
        }
    }
}

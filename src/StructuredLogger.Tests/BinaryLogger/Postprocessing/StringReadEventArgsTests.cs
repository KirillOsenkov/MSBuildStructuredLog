using System;
using Microsoft.Build.Logging.StructuredLogger;
using Xunit;

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="StringReadEventArgs"/> class.
    /// </summary>
    public class StringReadEventArgsTests
    {
        /// <summary>
        /// Tests that the constructor initializes both OriginalString and StringToBeUsed properties correctly.
        /// This test covers various input cases including non-empty string, empty string, and null.
        /// </summary>
        /// <param name="input">The input string to be passed to the constructor.</param>
//         [Theory] [Error] (27-43)CS1061 'StringReadEventArgs' does not contain a definition for 'OriginalString' and no accessible extension method 'OriginalString' accepting a first argument of type 'StringReadEventArgs' could be found (are you missing a using directive or an assembly reference?)
//         [InlineData("TestValue")]
//         [InlineData("")]
//         [InlineData(null)]
//         public void Constructor_WithVariousInputs_PropertiesSetCorrectly(string input)
//         {
//             // Arrange & Act
//             var eventArgs = new StringReadEventArgs(input);
// 
//             // Assert
//             Assert.Equal(input, eventArgs.OriginalString);
//             Assert.Equal(input, eventArgs.StringToBeUsed);
//         }

        /// <summary>
        /// Tests that the Reuse method correctly updates the OriginalString and StringToBeUsed properties when given a new non-null value.
        /// This follows the Arrange-Act-Assert pattern to verify the side effects of Reuse.
        /// </summary>
//         [Fact] [Error] (44-23)CS1061 'StringReadEventArgs' does not contain a definition for 'Reuse' and no accessible extension method 'Reuse' accepting a first argument of type 'StringReadEventArgs' could be found (are you missing a using directive or an assembly reference?) [Error] (47-46)CS1061 'StringReadEventArgs' does not contain a definition for 'OriginalString' and no accessible extension method 'OriginalString' accepting a first argument of type 'StringReadEventArgs' could be found (are you missing a using directive or an assembly reference?)
//         public void Reuse_WithNonNullValue_UpdatesProperties()
//         {
//             // Arrange
//             string initialValue = "Initial";
//             string newValue = "Updated";
//             var eventArgs = new StringReadEventArgs(initialValue);
// 
//             // Act
//             eventArgs.Reuse(newValue);
// 
//             // Assert
//             Assert.Equal(newValue, eventArgs.OriginalString);
//             Assert.Equal(newValue, eventArgs.StringToBeUsed);
//         }

        /// <summary>
        /// Tests that the Reuse method correctly updates the properties to null when provided with a null value.
        /// It verifies that passing null does not throw and the properties become null.
        /// </summary>
//         [Fact] [Error] (64-23)CS1061 'StringReadEventArgs' does not contain a definition for 'Reuse' and no accessible extension method 'Reuse' accepting a first argument of type 'StringReadEventArgs' could be found (are you missing a using directive or an assembly reference?) [Error] (67-35)CS1061 'StringReadEventArgs' does not contain a definition for 'OriginalString' and no accessible extension method 'OriginalString' accepting a first argument of type 'StringReadEventArgs' could be found (are you missing a using directive or an assembly reference?)
//         public void Reuse_WithNullValue_UpdatesPropertiesToNull()
//         {
//             // Arrange
//             string initialValue = "Initial";
//             string newValue = null;
//             var eventArgs = new StringReadEventArgs(initialValue);
// 
//             // Act
//             eventArgs.Reuse(newValue);
// 
//             // Assert
//             Assert.Null(eventArgs.OriginalString);
//             Assert.Null(eventArgs.StringToBeUsed);
//         }

        /// <summary>
        /// Tests that the Reuse method correctly updates the properties when given an empty string.
        /// This ensures that boundary conditions with empty string values are handled.
        /// </summary>
//         [Fact] [Error] (84-23)CS1061 'StringReadEventArgs' does not contain a definition for 'Reuse' and no accessible extension method 'Reuse' accepting a first argument of type 'StringReadEventArgs' could be found (are you missing a using directive or an assembly reference?) [Error] (87-46)CS1061 'StringReadEventArgs' does not contain a definition for 'OriginalString' and no accessible extension method 'OriginalString' accepting a first argument of type 'StringReadEventArgs' could be found (are you missing a using directive or an assembly reference?)
//         public void Reuse_WithEmptyString_UpdatesPropertiesToEmpty()
//         {
//             // Arrange
//             string initialValue = "NonEmpty";
//             string newValue = "";
//             var eventArgs = new StringReadEventArgs(initialValue);
// 
//             // Act
//             eventArgs.Reuse(newValue);
// 
//             // Assert
//             Assert.Equal(newValue, eventArgs.OriginalString);
//             Assert.Equal(newValue, eventArgs.StringToBeUsed);
//         }
    }
}

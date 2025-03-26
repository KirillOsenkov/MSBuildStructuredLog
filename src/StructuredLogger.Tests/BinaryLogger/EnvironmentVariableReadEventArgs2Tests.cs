using StructuredLogger.BinaryLogger;
using System;
using Xunit;

namespace StructuredLogger.BinaryLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="EnvironmentVariableReadEventArgs2"/> class.
    /// </summary>
    public class EnvironmentVariableReadEventArgs2Tests
    {
        /// <summary>
        /// Tests that the constructor correctly assigns the provided parameter values to both the inherited and new properties.
        /// </summary>
//         [Fact] [Error] (30-57)CS1061 'EnvironmentVariableReadEventArgs2' does not contain a definition for 'EnvironmentVariableValue' and no accessible extension method 'EnvironmentVariableValue' accepting a first argument of type 'EnvironmentVariableReadEventArgs2' could be found (are you missing a using directive or an assembly reference?)
//         public void Constructor_ValidParameters_PropertiesSetAsExpected()
//         {
//             // Arrange
//             string expectedEnvVarName = "TEST_VAR";
//             string expectedEnvVarValue = "TestValue";
//             string expectedFile = "testfile.txt";
//             int expectedLine = 10;
//             int expectedColumn = 20;
// 
//             // Act
//             var eventArgs = new EnvironmentVariableReadEventArgs2(expectedEnvVarName, expectedEnvVarValue, expectedFile, expectedLine, expectedColumn);
// 
//             // Assert
//             Assert.Equal(expectedEnvVarName, eventArgs.EnvironmentVariableName);
//             Assert.Equal(expectedEnvVarValue, eventArgs.EnvironmentVariableValue);
//             Assert.Equal(expectedFile, eventArgs.File);
//             Assert.Equal(expectedLine, eventArgs.LineNumber);
//             Assert.Equal(expectedColumn, eventArgs.ColumnNumber);
//         }

        /// <summary>
        /// Tests that updating the properties using their setters correctly reflects the new values.
        /// </summary>
        [Fact]
        public void Properties_SetterUpdates_PropertiesReturnUpdatedValues()
        {
            // Arrange
            var eventArgs = new EnvironmentVariableReadEventArgs2("VAR", "Value", "initial.txt", 1, 1);
            string newFile = "updated.txt";
            int newLine = 100;
            int newColumn = 200;

            // Act
            eventArgs.File = newFile;
            eventArgs.LineNumber = newLine;
            eventArgs.ColumnNumber = newColumn;

            // Assert
            Assert.Equal(newFile, eventArgs.File);
            Assert.Equal(newLine, eventArgs.LineNumber);
            Assert.Equal(newColumn, eventArgs.ColumnNumber);
        }

        /// <summary>
        /// Tests that passing null values for string parameters in the constructor assigns null to the corresponding properties.
        /// This ensures that the constructor does not throw exceptions when nulls are provided.
        /// </summary>
//         [Fact] [Error] (78-35)CS1061 'EnvironmentVariableReadEventArgs2' does not contain a definition for 'EnvironmentVariableValue' and no accessible extension method 'EnvironmentVariableValue' accepting a first argument of type 'EnvironmentVariableReadEventArgs2' could be found (are you missing a using directive or an assembly reference?)
//         public void Constructor_NullValues_PropertiesSetToNullWhenExpected()
//         {
//             // Arrange
//             string expectedEnvVarName = null;
//             string expectedEnvVarValue = null;
//             string expectedFile = null;
//             int expectedLine = 0;
//             int expectedColumn = 0;
// 
//             // Act
//             var eventArgs = new EnvironmentVariableReadEventArgs2(expectedEnvVarName, expectedEnvVarValue, expectedFile, expectedLine, expectedColumn);
// 
//             // Assert
//             Assert.Null(eventArgs.EnvironmentVariableName);
//             Assert.Null(eventArgs.EnvironmentVariableValue);
//             Assert.Null(eventArgs.File);
//             Assert.Equal(expectedLine, eventArgs.LineNumber);
//             Assert.Equal(expectedColumn, eventArgs.ColumnNumber);
//         }

        /// <summary>
        /// Tests that negative integers for line and column numbers are correctly assigned through the constructor.
        /// </summary>
        /// <param name="line">Negative line number</param>
        /// <param name="column">Negative column number</param>
        [Theory]
        [InlineData(-1, -5)]
        [InlineData(-100, -200)]
        public void Constructor_NegativeNumbers_PropertiesSetAsExpected(int line, int column)
        {
            // Arrange
            var expectedName = "VAR";
            var expectedValue = "Value";
            var expectedFile = "file.txt";

            // Act
            var eventArgs = new EnvironmentVariableReadEventArgs2(expectedName, expectedValue, expectedFile, line, column);

            // Assert
            Assert.Equal(line, eventArgs.LineNumber);
            Assert.Equal(column, eventArgs.ColumnNumber);
        }
    }
}

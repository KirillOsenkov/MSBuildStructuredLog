using System;
using Microsoft.Build.Logging.StructuredLogger;
using Xunit;

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="Import"/> class.
    /// </summary>
    public class ImportTests
    {
        private readonly string _sampleProjectFilePath = "C:\\Projects\\SampleProject.csproj";
        private readonly string _sampleImportedProjectFilePath = "C:\\Projects\\ImportedProject.csproj";
        private readonly int _sampleLine = 42;
        private readonly int _sampleColumn = 10;

        /// <summary>
        /// Tests that the parameterized constructor correctly sets all properties and inherited Text.
        /// </summary>
        [Fact]
        public void Constructor_WithValidParameters_SetsPropertiesCorrectly()
        {
            // Arrange
            string expectedProjectFilePath = _sampleProjectFilePath;
            string expectedImportedProjectFilePath = _sampleImportedProjectFilePath;
            int expectedLine = _sampleLine;
            int expectedColumn = _sampleColumn;
            string expectedLocation = $" at ({expectedLine};{expectedColumn})";

            // Act
            var import = new Import(expectedProjectFilePath, expectedImportedProjectFilePath, expectedLine, expectedColumn);

            // Assert
            Assert.Equal(expectedProjectFilePath, import.ProjectFilePath);
            Assert.Equal(expectedImportedProjectFilePath, import.ImportedProjectFilePath);
            Assert.Equal(expectedLine, import.Line);
            Assert.Equal(expectedColumn, import.Column);
            // The constructor sets Text to importedProjectFilePath.
            Assert.Equal(expectedImportedProjectFilePath, import.Text);
            Assert.Equal(expectedLocation, import.Location);
            Assert.Equal(nameof(Import), import.TypeName);
        }

        /// <summary>
        /// Tests that the default constructor initializes properties to default values.
        /// </summary>
        [Fact]
        public void DefaultConstructor_InitializesPropertiesToDefaultValues()
        {
            // Arrange & Act
            var import = new Import();

            // Assert
            Assert.Null(import.ProjectFilePath);
            Assert.Null(import.ImportedProjectFilePath);
            Assert.Equal(0, import.Line);
            Assert.Equal(0, import.Column);
            Assert.Null(import.Text);
            Assert.Equal(" at (0;0)", import.Location);
            Assert.Equal(nameof(Import), import.TypeName);
        }

        /// <summary>
        /// Tests that the ToString method returns the expected formatted string.
        /// </summary>
        [Fact]
        public void ToString_WithConstructorInitializedValues_ReturnsFormattedString()
        {
            // Arrange
            var import = new Import(_sampleProjectFilePath, _sampleImportedProjectFilePath, _sampleLine, _sampleColumn);
            string expected = $"Import: {_sampleImportedProjectFilePath} at ({_sampleLine};{_sampleColumn})";

            // Act
            var actual = import.ToString();

            // Assert
            Assert.Equal(expected, actual);
        }

        /// <summary>
        /// Tests the explicit interface implementations for IPreprocessable, IHasSourceFile, and IHasLineNumber.
        /// </summary>
        [Fact]
        public void ExplicitInterfaceImplementations_ReturnExpectedValues()
        {
            // Arrange
            var import = new Import(_sampleProjectFilePath, _sampleImportedProjectFilePath, _sampleLine, _sampleColumn);

            // Act
            var preprocessable = (IPreprocessable)import;
            var hasSourceFile = (IHasSourceFile)import;
            var hasLineNumber = (IHasLineNumber)import;

            // Assert
            Assert.Equal(_sampleImportedProjectFilePath, preprocessable.RootFilePath);
            Assert.Equal(_sampleProjectFilePath, hasSourceFile.SourceFilePath);
            Assert.Equal(_sampleLine, hasLineNumber.LineNumber);
        }

        /// <summary>
        /// Tests the IsLowRelevance property setter and getter assuming default IsSelected is false.
        /// </summary>
        [Fact]
        public void IsLowRelevance_SetterAndGetter_ReturnsExpectedValue()
        {
            // Arrange
            var import = new Import();

            // Act & Assert
            import.IsLowRelevance = true;
            Assert.True(import.IsLowRelevance);

            import.IsLowRelevance = false;
            Assert.False(import.IsLowRelevance);
        }

        /// <summary>
        /// Tests that the TypeName property returns the expected value.
        /// </summary>
        [Fact]
        public void TypeName_ReturnsClassName()
        {
            // Arrange
            var import = new Import();

            // Act
            var typeName = import.TypeName;

            // Assert
            Assert.Equal("Import", typeName);
        }
    }
}

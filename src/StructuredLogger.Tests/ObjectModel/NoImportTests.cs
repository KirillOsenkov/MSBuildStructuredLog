using System;
using Microsoft.Build.Logging.StructuredLogger;
using Xunit;

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="NoImport"/> class.
    /// </summary>
    public class NoImportTests
    {
        private readonly string _testProjectFilePath;
        private readonly string _testImportedFileSpec;
        private readonly int _testLine;
        private readonly int _testColumn;
        private readonly string _testReason;

        public NoImportTests()
        {
            _testProjectFilePath = "C:\\Project\\TestProject.csproj";
            _testImportedFileSpec = "ImportedFile.targets";
            _testLine = 10;
            _testColumn = 5;
            _testReason = "File not found";
        }

        /// <summary>
        /// Tests that the default constructor sets properties to default values.
        /// Expected outcome: All string properties are null and integer properties are zero.
        /// </summary>
        [Fact]
        public void Constructor_Default_SetsDefaultValues()
        {
            // Arrange & Act
            var instance = new NoImport();

            // Assert
            Assert.Null(instance.ProjectFilePath);
            // Since the base Text property is set via the parameterized constructor,
            // we check its value via ToString() pattern.
            string expectedLocation = $" at ({instance.Line};{instance.Column})";
            string expectedToString = $"NoImport: {instance.Text}{expectedLocation} {instance.Reason}";
            Assert.Equal(expectedToString, instance.ToString());
            Assert.Equal(0, instance.Line);
            Assert.Equal(0, instance.Column);
            // Reason is not set by default.
            Assert.Null(instance.Reason);

            // TypeName should return the class name.
            Assert.Equal("NoImport", instance.TypeName);
        }

        /// <summary>
        /// Tests that the parameterized constructor correctly sets all properties.
        /// Expected outcome: Properties match the initialization parameters and dependent computed properties are correct.
        /// </summary>
        [Fact]
        public void Constructor_Parameterized_SetsProperties()
        {
            // Arrange & Act
            var instance = new NoImport(_testProjectFilePath, _testImportedFileSpec, _testLine, _testColumn, _testReason);

            // Assert
            Assert.Equal(_testProjectFilePath, instance.ProjectFilePath);
            // The parameter importedFileSpec is assigned to the Text property.
            Assert.Equal(_testImportedFileSpec, instance.Text);
            Assert.Equal(_testLine, instance.Line);
            Assert.Equal(_testColumn, instance.Column);
            Assert.Equal(_testReason, instance.Reason);

            string expectedLocation = $" at ({_testLine};{_testColumn})";
            Assert.Equal(expectedLocation, instance.Location);

            string expectedToString = $"NoImport: {_testImportedFileSpec}{expectedLocation} {_testReason}";
            Assert.Equal(expectedToString, instance.ToString());
        }

        /// <summary>
        /// Tests that the TypeName property always returns "NoImport".
        /// Expected outcome: TypeName equals "NoImport".
        /// </summary>
        [Fact]
        public void TypeName_Property_ReturnsNoImport()
        {
            // Arrange
            var instance = new NoImport();

            // Act
            var typeName = instance.TypeName;

            // Assert
            Assert.Equal("NoImport", typeName);
        }

        /// <summary>
        /// Tests that the Location property returns the correct formatted string.
        /// Expected outcome: Location is formatted as " at (Line;Column)".
        /// </summary>
        [Fact]
        public void Location_Property_ReturnsCorrectFormat()
        {
            // Arrange
            var instance = new NoImport
            {
                Line = 20,
                Column = 15
            };

            // Act
            var location = instance.Location;

            // Assert
            Assert.Equal(" at (20;15)", location);
        }

        /// <summary>
        /// Tests the getter and setter of the IsLowRelevance property.
        /// Expected outcome: When set to true, the getter returns true (given default IsSelected is false),
        /// and when set to false, the getter returns false.
        /// </summary>
        [Fact]
        public void IsLowRelevance_GetAndSet_WorksCorrectly()
        {
            // Arrange
            var instance = new NoImport();

            // Initially, assuming no flags have been set, IsLowRelevance should be false.
            Assert.False(instance.IsLowRelevance);

            // Act
            instance.IsLowRelevance = true;

            // Assert
            Assert.True(instance.IsLowRelevance);

            // Act
            instance.IsLowRelevance = false;

            // Assert
            Assert.False(instance.IsLowRelevance);
        }

        /// <summary>
        /// Tests the interface implementations for IHasSourceFile and IHasLineNumber.
        /// Expected outcome: Casting the instance to these interfaces returns the proper values.
        /// </summary>
        [Fact]
        public void InterfaceImplementations_IHasSourceFileAndIHasLineNumber_ReturnCorrectData()
        {
            // Arrange
            var instance = new NoImport(_testProjectFilePath, _testImportedFileSpec, _testLine, _testColumn, _testReason);

            // Act
            var sourceFile = ((IHasSourceFile)instance).SourceFilePath;
            var lineNumber = ((IHasLineNumber)instance).LineNumber;

            // Assert
            Assert.Equal(_testProjectFilePath, sourceFile);
            Assert.Equal(_testLine, lineNumber);
        }
    }
}

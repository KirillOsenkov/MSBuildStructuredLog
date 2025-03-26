using System;
using Xunit;
using Microsoft.Build.Logging.StructuredLogger;

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="AbstractDiagnostic"/> class.
    /// </summary>
    public class AbstractDiagnosticTests
    {
        /// <summary>
        /// Tests the ToString method when no file, position, or code is provided.
        /// Expects that if File is null (and thus becomes empty) and no line, column or code is provided,
        /// the output is simply the text without a leading space.
        /// </summary>
        [Fact]
        public void ToString_NoFileNoPositionNoCode_ReturnsTextOnly()
        {
            // Arrange
            var diagnostic = new AbstractDiagnostic
            {
                Text = "message",
                File = null,
                LineNumber = 0,
                ColumnNumber = 0,
                Code = null,
                ProjectFile = null
            };

            // Act
            var result = diagnostic.ToString();

            // Assert
            Assert.Equal("message", result);
        }

        /// <summary>
        /// Tests the ToString method when a file is provided but no position or code information.
        /// Expects the text to be prepended with a space due to the presence of file information.
        /// </summary>
        [Fact]
        public void ToString_WithFileButNoPositionOrCode_ReturnsFileAndText()
        {
            // Arrange
            var diagnostic = new AbstractDiagnostic
            {
                Text = "msg",
                File = "file.cs",
                LineNumber = 0,
                ColumnNumber = 0,
                Code = null,
                ProjectFile = null
            };

            // Act
            var result = diagnostic.ToString();

            // Assert
            Assert.Equal("file.cs msg", result);
        }

        /// <summary>
        /// Tests the ToString method when only a line number is provided.
        /// Expects the output to include the position formatted with the line number.
        /// </summary>
        [Fact]
        public void ToString_WithLineNumberOnly_ReturnsPositionInOutput()
        {
            // Arrange
            var diagnostic = new AbstractDiagnostic
            {
                Text = "err",
                File = "",
                LineNumber = 10,
                ColumnNumber = 0,
                Code = null,
                ProjectFile = null
            };

            // Act
            var result = diagnostic.ToString();

            // Assert
            Assert.Equal("(10): err", result);
        }

        /// <summary>
        /// Tests the ToString method when file, position, code, and project file are all provided.
        /// Expects the output to correctly format each section into a single, concatenated string.
        /// </summary>
        [Fact]
        public void ToString_WithFilePositionCodeAndProjectFile_ReturnsFormattedString()
        {
            // Arrange
            var diagnostic = new AbstractDiagnostic
            {
                Text = "warning message",
                File = "file.cs",
                LineNumber = 5,
                ColumnNumber = 3,
                Code = "W123",
                ProjectFile = "proj.csproj"
            };

            // Act
            var result = diagnostic.ToString();

            // Assert
            var expected = "file.cs(5,3): abstractdiagnostic W123: warning message [proj.csproj]";
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Tests the ToString method when the Code property contains only whitespace.
        /// Expects that the code segment is omitted from the output.
        /// </summary>
        [Fact]
        public void ToString_WithWhitespaceCode_DoesNotIncludeCodeSection()
        {
            // Arrange
            var diagnostic = new AbstractDiagnostic
            {
                Text = "info",
                File = "file.cs",
                LineNumber = 0,
                ColumnNumber = 0,
                Code = "   ",
                ProjectFile = null
            };

            // Act
            var result = diagnostic.ToString();

            // Assert
            Assert.Equal("file.cs info", result);
        }

        /// <summary>
        /// Tests the TypeName property to confirm it returns the expected type name.
        /// </summary>
        [Fact]
        public void TypeName_ReturnsExpectedValue()
        {
            // Arrange
            var diagnostic = new AbstractDiagnostic();

            // Act
            var typeName = diagnostic.TypeName;

            // Assert
            Assert.Equal("AbstractDiagnostic", typeName);
        }

        /// <summary>
        /// Tests the Title property to ensure it returns the same value as the ToString method.
        /// </summary>
        [Fact]
        public void Title_ReturnsSameAsToString()
        {
            // Arrange
            var diagnostic = new AbstractDiagnostic
            {
                Text = "sample text",
                File = "sample.cs",
                LineNumber = 2,
                ColumnNumber = 1,
                Code = "E001",
                ProjectFile = "project.csproj"
            };
            var expected = diagnostic.ToString();

            // Act
            var title = diagnostic.Title;

            // Assert
            Assert.Equal(expected, title);
        }
    }
}

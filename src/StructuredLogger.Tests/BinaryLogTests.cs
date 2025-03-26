using Microsoft.Build.Logging.StructuredLogger;
using Microsoft.Build.Logging.StructuredLogger.UnitTests;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref = "BinaryLog"/> class.
    /// </summary>
    public class BinaryLogTests
    {
        /// <summary>
        /// Tests that calling ReadRecords with a null string argument throws an exception.
        /// </summary>
        [Fact]
        public void ReadRecords_String_NullInput_ThrowsException()
        {
            // Arrange
            string nullPath = null;
            // Act & Assert
            Assert.ThrowsAny<Exception>(() => BinaryLog.ReadRecords(nullPath));
        }

        /// <summary>
        /// Tests that calling ReadRecords with a non-existent file path throws an exception.
        /// </summary>
        [Fact]
        public void ReadRecords_String_NonExistentFile_ThrowsException()
        {
            // Arrange
            string nonExistentFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".binlog");
            // Act & Assert
            Assert.ThrowsAny<Exception>(() => BinaryLog.ReadRecords(nonExistentFilePath));
        }

        /// <summary>
        /// Tests that calling ReadRecords with a null Stream argument throws an exception.
        /// </summary>
        [Fact]
        public void ReadRecords_Stream_NullInput_ThrowsException()
        {
            // Arrange
            Stream nullStream = null;
            // Act & Assert
            Assert.ThrowsAny<Exception>(() => BinaryLog.ReadRecords(nullStream));
        }

        /// <summary>
        /// Tests that calling ReadRecords with a null byte array argument throws an exception.
        /// </summary>
        [Fact]
        public void ReadRecords_ByteArray_NullInput_ThrowsException()
        {
            // Arrange
            byte[] nullBytes = null;
            // Act & Assert
            Assert.ThrowsAny<Exception>(() => BinaryLog.ReadRecords(nullBytes));
        }

        /// <summary>
        /// Tests that calling ReadBuild with a non-existent file path throws a FileNotFoundException.
        /// </summary>
        [Fact]
        public void ReadBuild_String_NonExistentFile_ThrowsFileNotFoundException()
        {
            // Arrange
            string nonExistentFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".binlog");
            // Act & Assert
            Assert.Throws<FileNotFoundException>(() => BinaryLog.ReadBuild(nonExistentFilePath));
        }

        /// <summary>
        /// Tests that calling ReadBuild with a non-existent file path using the overload with Progress throws a FileNotFoundException.
        /// </summary>
//         [Fact] [Error] (87-97)CS1503 Argument 2: cannot convert from 'Microsoft.Build.Logging.StructuredLogger.UnitTests.Progress' to 'Microsoft.Build.Logging.StructuredLogger.Progress'
//         public void ReadBuild_StringWithProgress_NonExistentFile_ThrowsFileNotFoundException()
//         {
//             // Arrange
//             string nonExistentFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".binlog");
//             Progress progress = null;
//             // Act & Assert
//             Assert.Throws<FileNotFoundException>(() => BinaryLog.ReadBuild(nonExistentFilePath, progress));
//         }

        /// <summary>
        /// Tests that calling ReadBuild with a non-existent file path using the overload with ReaderSettings throws a FileNotFoundException.
        /// </summary>
        [Fact]
        public void ReadBuild_StringWithReaderSettings_NonExistentFile_ThrowsFileNotFoundException()
        {
            // Arrange
            string nonExistentFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".binlog");
            ReaderSettings settings = ReaderSettings.Default;
            // Act & Assert
            Assert.Throws<FileNotFoundException>(() => BinaryLog.ReadBuild(nonExistentFilePath, settings));
        }

        /// <summary>
        /// Tests that calling ReadBuild with a non-existent file path using the full overload throws a FileNotFoundException.
        /// </summary>
//         [Fact] [Error] (114-97)CS1503 Argument 2: cannot convert from 'Microsoft.Build.Logging.StructuredLogger.UnitTests.Progress' to 'Microsoft.Build.Logging.StructuredLogger.Progress'
//         public void ReadBuild_StringWithProgressAndReaderSettings_NonExistentFile_ThrowsFileNotFoundException()
//         {
//             // Arrange
//             string nonExistentFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".binlog");
//             Progress progress = null;
//             ReaderSettings settings = ReaderSettings.Default;
//             // Act & Assert
//             Assert.Throws<FileNotFoundException>(() => BinaryLog.ReadBuild(nonExistentFilePath, progress, settings));
//         }

        /// <summary>
        /// Tests that calling ReadBuild with a null Stream throws an exception.
        /// </summary>
        [Fact]
        public void ReadBuild_Stream_NullInput_ThrowsException()
        {
            // Arrange
            Stream nullStream = null;
            // Act & Assert
            Assert.ThrowsAny<Exception>(() => BinaryLog.ReadBuild(nullStream));
        }

        /// <summary>
        /// Tests that calling ReadBuild with an empty stream returns a Build instance indicating failure.
        /// Expected outcome is a Build with Succeeded set to false.
        /// </summary>
//         [Fact] [Error] (139-27)CS0029 Cannot implicitly convert type 'Microsoft.Build.Logging.StructuredLogger.Build' to 'Microsoft.Build.Logging.StructuredLogger.UnitTests.Build' [Error] (142-32)CS1061 'Build' does not contain a definition for 'Succeeded' and no accessible extension method 'Succeeded' accepting a first argument of type 'Build' could be found (are you missing a using directive or an assembly reference?) [Error] (146-31)CS1061 'Build' does not contain a definition for 'LogFilePath' and no accessible extension method 'LogFilePath' accepting a first argument of type 'Build' could be found (are you missing a using directive or an assembly reference?)
//         public void ReadBuild_Stream_EmptyStream_ReturnsBuildWithError()
//         {
//             // Arrange
//             using var emptyStream = new MemoryStream();
//             // Act
//             Build build = BinaryLog.ReadBuild(emptyStream);
//             // Assert
//             Assert.NotNull(build);
//             Assert.False(build.Succeeded);
//             // Check if an error message about opening the file is present among the children, if available.
//             // Since the structure of Build.Children is not defined here, we check the LogFilePath remains null.
//             // This indicates that the file-based overload was not used.
//             Assert.Null(build.LogFilePath);
//         }

        /// <summary>
        /// Tests that calling ReadBuild with a null Stream along with additional parameters throws an exception.
        /// </summary>
//         [Fact] [Error] (161-79)CS1503 Argument 2: cannot convert from 'Microsoft.Build.Logging.StructuredLogger.UnitTests.Progress' to 'Microsoft.Build.Logging.StructuredLogger.Progress'
//         public void ReadBuild_StreamWithProgressAndArchiveAndReaderSettings_NullStream_ThrowsException()
//         {
//             // Arrange
//             Stream nullStream = null;
//             Progress progress = null;
//             byte[] archive = null;
//             ReaderSettings settings = ReaderSettings.Default;
//             // Act & Assert
//             Assert.ThrowsAny<Exception>(() => BinaryLog.ReadBuild(nullStream, progress, archive, settings));
//         }
    }
}
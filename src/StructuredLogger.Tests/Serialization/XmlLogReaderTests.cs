using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Build.Logging.StructuredLogger;
using Xunit;

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="XmlLogReader"/> class.
    /// </summary>
    public class XmlLogReaderTests
    {
        private readonly string validXmlContent;
        private readonly string invalidXmlContent;

        public XmlLogReaderTests()
        {
            // A minimal valid XML that represents a Build element with attributes.
            // Note: The actual processing of these attributes depends on the Serialization and AttributeNames implementations.
            // For testing purposes, we supply valid attribute values.
            validXmlContent = @"<?xml version='1.0' encoding='utf-8'?>
<Build Succeeded='true' IsAnalyzed='true'></Build>";
            invalidXmlContent = "This is not a valid XML content";
        }

        /// <summary>
        /// Tests the static ReadFromXml(Stream) method with valid XML.
        /// Expects that a Build instance is returned without a LogFilePath.
        /// </summary>
        [Fact]
        public void ReadFromXml_Stream_ValidXml_ReturnsBuild()
        {
            // Arrange
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(validXmlContent));

            // Act
            var build = XmlLogReader.ReadFromXml(stream);

            // Assert
            Assert.NotNull(build);
            // As the version invoked is the stream overload, LogFilePath is not set.
            var logFilePathProperty = build.GetType().GetProperty("LogFilePath");
            Assert.NotNull(logFilePathProperty);
            var logFilePathValue = logFilePathProperty.GetValue(build) as string;
            Assert.Null(logFilePathValue);
        }

        /// <summary>
        /// Tests the static ReadFromXml(string) method with a valid XML file.
        /// Expects that a Build instance is returned with LogFilePath set to the input file path.
        /// </summary>
        [Fact]
        public void ReadFromXml_FilePath_ValidXml_ReturnsBuildWithLogFilePath()
        {
            // Arrange
            string tempFilePath = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempFilePath, validXmlContent);

                // Act
                var build = XmlLogReader.ReadFromXml(tempFilePath);

                // Assert
                Assert.NotNull(build);
                var logFilePathProperty = build.GetType().GetProperty("LogFilePath");
                Assert.NotNull(logFilePathProperty);
                var logFilePathValue = logFilePathProperty.GetValue(build) as string;
                Assert.Equal(tempFilePath, logFilePathValue);
            }
            finally
            {
                File.Delete(tempFilePath);
            }
        }

        /// <summary>
        /// Tests the instance Read(Stream) method with invalid XML content.
        /// Expects that the method catches the exception and returns a Build instance with Succeeded set to false and two errors added.
        /// </summary>
        [Fact]
        public void Read_Stream_InvalidXml_ReturnsBuildWithErrors()
        {
            // Arrange
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(invalidXmlContent));
            var xmlLogReader = new XmlLogReader();

            // Act
            var build = xmlLogReader.Read(stream);

            // Assert
            Assert.NotNull(build);
            // Check that Succeeded is false due to error handling in catch block.
            var succeededProperty = build.GetType().GetProperty("Succeeded");
            Assert.NotNull(succeededProperty);
            bool succeededValue = (bool)succeededProperty.GetValue(build);
            Assert.False(succeededValue);

            // Since two Error children are added in error scenario, attempt to verify the child count if available.
            var childrenProperty = build.GetType().GetProperty("Children");
            if (childrenProperty != null)
            {
                var children = childrenProperty.GetValue(build) as IEnumerable<object>;
                Assert.NotNull(children);
                int count = 0;
                foreach (var child in children)
                {
                    count++;
                }
                Assert.True(count >= 2, "Expected at least two errors to be added to the build.");
            }
        }

        /// <summary>
        /// Tests the instance Read(string) method when the XML file does not exist.
        /// Expects that a FileNotFoundException is thrown.
        /// </summary>
        [Fact]
        public void Read_FilePath_NonExistentFile_ThrowsFileNotFoundException()
        {
            // Arrange
            string nonExistentFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".xml");
            var xmlLogReader = new XmlLogReader();

            // Act & Assert
            Assert.Throws<FileNotFoundException>(() => xmlLogReader.Read(nonExistentFilePath));
        }

        /// <summary>
        /// Tests the instance Read(string) method with a null file path.
        /// Expects that an ArgumentNullException is thrown.
        /// </summary>
        [Fact]
        public void Read_FilePath_Null_ThrowsArgumentNullException()
        {
            // Arrange
            var xmlLogReader = new XmlLogReader();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => xmlLogReader.Read((string)null));
        }

        /// <summary>
        /// Tests the ReadFromXml(Stream) method when a null stream is passed.
        /// Expects that an ArgumentNullException is thrown.
        /// </summary>
        [Fact]
        public void ReadFromXml_Stream_NullStream_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => XmlLogReader.ReadFromXml((Stream)null));
        }
    }
}

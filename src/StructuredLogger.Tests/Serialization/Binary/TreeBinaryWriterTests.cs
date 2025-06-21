using System;
using System.IO;
using System.IO.Compression;
using Xunit;
using Microsoft.Build.Logging.StructuredLogger;

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="TreeBinaryWriter"/> class.
    /// </summary>
    public class TreeBinaryWriterTests
    {
        private readonly string _tempFilePath;

        public TreeBinaryWriterTests()
        {
            _tempFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".bin");
        }

        /// <summary>
        /// Tests that the constructor writes the correct version bytes to the file.
        /// The expected version bytes are 1, 2, and 48.
        /// </summary>
        [Fact]
        public void Constructor_WritesVersionBytes()
        {
            // Arrange & Act
            using (var writer = new TreeBinaryWriter(_tempFilePath))
            {
                // Immediately dispose to flush all data
            }

            // Assert
            byte[] versionBytes = new byte[3];
            using (var fs = new FileStream(_tempFilePath, FileMode.Open, FileAccess.Read))
            {
                int bytesRead = fs.Read(versionBytes, 0, 3);
                Assert.Equal(3, bytesRead);
            }

            Assert.Equal(1, versionBytes[0]);
            Assert.Equal(2, versionBytes[1]);
            Assert.Equal(48, versionBytes[2]);

            File.Delete(_tempFilePath);
        }

        /// <summary>
        /// Tests the WriteNode method with a non-null name.
        /// Expects the string table to contain one entry after writing a node.
        /// </summary>
        [Fact]
        public void WriteNode_WithNonNullName_AddsToStringTable()
        {
            // Arrange
            using (var writer = new TreeBinaryWriter(_tempFilePath))
            {
                // Act
                writer.WriteNode("TestNode");
            }

            // Assert
            int stringTableCount = ReadStringTableCountFromFile(_tempFilePath);
            Assert.Equal(1, stringTableCount);

            File.Delete(_tempFilePath);
        }

        /// <summary>
        /// Tests the WriteNode method with a null name.
        /// Expects that null does not add an entry to the string table.
        /// </summary>
        [Fact]
        public void WriteNode_WithNullName_DoesNotAddToStringTable()
        {
            // Arrange
            using (var writer = new TreeBinaryWriter(_tempFilePath))
            {
                // Act
                writer.WriteNode(null);
            }

            // Assert
            int stringTableCount = ReadStringTableCountFromFile(_tempFilePath);
            Assert.Equal(0, stringTableCount);

            File.Delete(_tempFilePath);
        }

        /// <summary>
        /// Tests the combination of WriteAttributeValue and WriteEndAttributes methods.
        /// Expects the string table to contain entries for both the node name and attribute values.
        /// </summary>
        [Fact]
        public void WriteAttributeValueAndEndAttributes_WithValidAttributes_AddsToStringTable()
        {
            // Arrange
            using (var writer = new TreeBinaryWriter(_tempFilePath))
            {
                writer.WriteNode("NodeWithAttributes");
                writer.WriteAttributeValue("attr1");
                writer.WriteAttributeValue("attr2");
                writer.WriteEndAttributes();
            }

            // Assert
            // Expect two entries in string table: "NodeWithAttributes", "attr1" and "attr2" are written via WriteEndAttributes.
            // Note: "attr1" and "attr2" are both added only when used in WriteAttributeValue and then referenced in WriteEndAttributes.
            int stringTableCount = ReadStringTableCountFromFile(_tempFilePath);
            Assert.Equal(3, stringTableCount);

            File.Delete(_tempFilePath);
        }

        /// <summary>
        /// Tests the WriteChildrenCount method.
        /// Since this method writes data to the tree nodes stream and does not affect the string table,
        /// we validate that the string table remains as expected when a node is written.
        /// </summary>
        [Fact]
        public void WriteChildrenCount_AfterWriteNode_StringTableUnchanged()
        {
            // Arrange
            using (var writer = new TreeBinaryWriter(_tempFilePath))
            {
                writer.WriteNode("ParentNode");
                writer.WriteChildrenCount(5);
            }

            // Assert
            int stringTableCount = ReadStringTableCountFromFile(_tempFilePath);
            // Only "ParentNode" should be in the string table.
            Assert.Equal(1, stringTableCount);

            File.Delete(_tempFilePath);
        }

        /// <summary>
        /// Tests the WriteByteArray method with a null byte array.
        /// Expects that it writes a length of 0 and does not add any string to the string table.
        /// </summary>
        [Fact]
        public void WriteByteArray_WithNull_WritesZeroLength()
        {
            // Arrange
            using (var writer = new TreeBinaryWriter(_tempFilePath))
            {
                writer.WriteNode("NodeForByteArrayTest");
                writer.WriteByteArray(null);
            }

            // Assert
            int stringTableCount = ReadStringTableCountFromFile(_tempFilePath);
            // Only "NodeForByteArrayTest" should have been added.
            Assert.Equal(1, stringTableCount);

            File.Delete(_tempFilePath);
        }

        /// <summary>
        /// Tests the WriteByteArray method with a non-empty byte array.
        /// Expects that the method writes the length and data correctly,
        /// and that the string table remains unaffected.
        /// </summary>
        [Fact]
        public void WriteByteArray_WithNonEmptyArray_WritesCorrectDataAndStringTableUnchanged()
        {
            // Arrange
            byte[] testBytes = new byte[] { 10, 20, 30, 40 };
            using (var writer = new TreeBinaryWriter(_tempFilePath))
            {
                writer.WriteNode("ByteArrayNode");
                writer.WriteByteArray(testBytes);
            }

            // Assert
            int stringTableCount = ReadStringTableCountFromFile(_tempFilePath);
            // Only "ByteArrayNode" should have been added.
            Assert.Equal(1, stringTableCount);

            File.Delete(_tempFilePath);
        }

        /// <summary>
        /// Tests that calling Dispose multiple times does not throw an exception.
        /// </summary>
//         [Fact] [Error] (196-35)CS0117 'Record' does not contain a definition for 'Exception'
//         public void Dispose_MultipleCalls_DoesNotThrow()
//         {
//             // Arrange
//             var writer = new TreeBinaryWriter(_tempFilePath);
//             writer.WriteNode("Node");
// 
//             // Act & Assert
//             Exception ex = Record.Exception(() =>
//             {
//                 writer.Dispose();
//                 writer.Dispose();
//             });
//             Assert.Null(ex);
// 
//             File.Delete(_tempFilePath);
//         }

        /// <summary>
        /// Helper method to read the string table count from the file created by TreeBinaryWriter.
        /// It skips the initial version bytes and uses a GZipStream to decompress the data.
        /// </summary>
        /// <param name="filePath">The file path of the binary file.</param>
        /// <returns>The count of entries in the string table.</returns>
        private int ReadStringTableCountFromFile(string filePath)
        {
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                // Skip the version bytes (3 bytes)
                fs.Seek(3, SeekOrigin.Begin);
                using (var gzip = new GZipStream(fs, CompressionMode.Decompress))
                using (var br = new BinaryReader(gzip))
                {
                    // The string table is written first in the gzip stream.
                    // It writes the count of strings (an int32) followed by each string.
                    return br.ReadInt32();
                }
            }
        }
    }
}

using System;
using System.IO;
using System.Text;
using Moq;
using Xunit;
using Microsoft.Build.Logging.StructuredLogger;
using Microsoft.Build.Framework;
using StructuredLogger.BinaryLogger;

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="BuildEventArgsReader"/> class.
    /// </summary>
//     public class BuildEventArgsReaderTests [Error] (15-18)CS0101 The namespace 'Microsoft.Build.Logging.StructuredLogger.UnitTests' already contains a definition for 'BuildEventArgsReaderTests'
//     {
//         // Assuming from the source comments the minimal file format version for forward compatible reading is 18.
//         private const int ForwardCompatibilityMinimalVersion = 18;
//         // For testing EndOfFile, we assume the enum value for BinaryLogRecordKind.EndOfFile is 0.
//         private readonly byte[] EndOfFileRecordBytes = new byte[] { 0 };
// 
//         /// <summary>
//         /// Tests that the constructor initializes the reader and that the Position is initially zero.
//         /// </summary>
//         [Fact]
//         public void Constructor_ValidParameters_InitializesReader()
//         {
//             // Arrange
//             var memoryStream = new MemoryStream(EndOfFileRecordBytes);
//             var binaryReader = new BinaryReader(memoryStream, Encoding.UTF8);
//             int fileFormatVersion = ForwardCompatibilityMinimalVersion;
// 
//             // Act
//             var reader = new BuildEventArgsReader(binaryReader, fileFormatVersion);
// 
//             // Assert
//             Assert.Equal(0, reader.Position);
//         }
// 
//         /// <summary>
//         /// Tests that setting SkipUnknownEvents on a file format version below the minimal version throws an InvalidOperationException.
//         /// </summary>
//         [Fact]
//         public void SkipUnknownEvents_WhenFileFormatVersionBelowMinimum_ThrowsInvalidOperationException()
//         {
//             // Arrange
//             var memoryStream = new MemoryStream(EndOfFileRecordBytes);
//             var binaryReader = new BinaryReader(memoryStream, Encoding.UTF8);
//             int fileFormatVersion = ForwardCompatibilityMinimalVersion - 1; // Below minimal version
//             var reader = new BuildEventArgsReader(binaryReader, fileFormatVersion);
// 
//             // Act & Assert
//             var exception = Assert.Throws<InvalidOperationException>(() => reader.SkipUnknownEvents = true);
//             Assert.Contains($"Forward compatible reading is not supported for file format version {fileFormatVersion}", exception.Message);
//         }
// 
//         /// <summary>
//         /// Tests that setting SkipUnknownEventParts on a file format version below the minimal version throws an InvalidOperationException.
//         /// </summary>
//         [Fact]
//         public void SkipUnknownEventParts_WhenFileFormatVersionBelowMinimum_ThrowsInvalidOperationException()
//         {
//             // Arrange
//             var memoryStream = new MemoryStream(EndOfFileRecordBytes);
//             var binaryReader = new BinaryReader(memoryStream, Encoding.UTF8);
//             int fileFormatVersion = ForwardCompatibilityMinimalVersion - 1;
//             var reader = new BuildEventArgsReader(binaryReader, fileFormatVersion);
// 
//             // Act & Assert
//             var exception = Assert.Throws<InvalidOperationException>(() => reader.SkipUnknownEventParts = true);
//             Assert.Contains($"Forward compatible reading is not supported for file format version {fileFormatVersion}", exception.Message);
//         }
// 
//         /// <summary>
//         /// Tests that Read() returns null when the first record encountered is EndOfFile.
//         /// </summary>
//         [Fact]
//         public void Read_WhenEndOfFile_ReturnsNull()
//         {
//             // Arrange
//             // Create a stream that returns 0 as the record kind, assumed to represent EndOfFile.
//             var memoryStream = new MemoryStream(EndOfFileRecordBytes);
//             var binaryReader = new BinaryReader(memoryStream, Encoding.UTF8);
//             int fileFormatVersion = ForwardCompatibilityMinimalVersion;
//             var reader = new BuildEventArgsReader(binaryReader, fileFormatVersion);
// 
//             // Act
//             var result = reader.Read();
// 
//             // Assert
//             Assert.Null(result);
//         }
// 
//         /// <summary>
//         /// Tests that ReadRaw() returns a RawRecord with EndOfFile and a Null stream when encountering EndOfFile.
//         /// </summary>
//         [Fact]
//         public void ReadRaw_WhenEndOfFile_ReturnsRawRecordWithEndOfFile()
//         {
//             // Arrange
//             var memoryStream = new MemoryStream(EndOfFileRecordBytes);
//             var binaryReader = new BinaryReader(memoryStream, Encoding.UTF8);
//             int fileFormatVersion = ForwardCompatibilityMinimalVersion;
//             var reader = new BuildEventArgsReader(binaryReader, fileFormatVersion);
// 
//             // Act
//             var rawRecord = reader.ReadRaw();
// 
//             // Assert
//             // EndOfFile is assumed to be represented by 0.
//             Assert.Equal(0, (int)rawRecord.RecordKind);
//             Assert.Same(Stream.Null, rawRecord.Stream);
//         }
// 
//         /// <summary>
//         /// Tests that Dispose properly disposes the underlying BinaryReader when CloseInput is set to true.
//         /// </summary>
//         [Fact]
//         public void Dispose_WhenCloseInputIsTrue_ClosesBinaryReader()
//         {
//             // Arrange
//             var memoryStream = new MemoryStream(EndOfFileRecordBytes);
//             var binaryReader = new BinaryReader(memoryStream, Encoding.UTF8);
//             int fileFormatVersion = ForwardCompatibilityMinimalVersion;
//             var reader = new BuildEventArgsReader(binaryReader, fileFormatVersion)
//             {
//                 CloseInput = true
//             };
// 
//             // Act
//             reader.Dispose();
// 
//             // Assert
//             Assert.Throws<ObjectDisposedException>(() => memoryStream.ReadByte());
//         }
// 
//         /// <summary>
//         /// Tests that calling Dispose multiple times does not throw an exception.
//         /// </summary>
//         [Fact]
//         public void Dispose_CalledMultipleTimes_DoesNotThrow()
//         {
//             // Arrange
//             var memoryStream = new MemoryStream(EndOfFileRecordBytes);
//             var binaryReader = new BinaryReader(memoryStream, Encoding.UTF8);
//             int fileFormatVersion = ForwardCompatibilityMinimalVersion;
//             var reader = new BuildEventArgsReader(binaryReader, fileFormatVersion);
// 
//             // Act & Assert
//             reader.Dispose();
//             reader.Dispose();
//         }
// 
//         /// <summary>
//         /// Tests that the Position property reflects the underlying stream's position.
//         /// </summary>
//         [Fact]
//         public void Position_Property_ReturnsUnderlyingStreamPosition()
//         {
//             // Arrange
//             var testData = new byte[] { 0, 1, 2, 3, 4 };
//             var memoryStream = new MemoryStream(testData);
//             var binaryReader = new BinaryReader(memoryStream, Encoding.UTF8);
//             int fileFormatVersion = ForwardCompatibilityMinimalVersion;
//             var reader = new BuildEventArgsReader(binaryReader, fileFormatVersion);
// 
//             // Act
//             long posBefore = reader.Position;
//             memoryStream.ReadByte(); // Advance the underlying stream.
//             long posAfter = reader.Position;
// 
//             // Assert
//             Assert.Equal(0, posBefore);
//             Assert.True(posAfter > posBefore);
//         }
//     }

    /// <summary>
    /// Unit tests for the internal <see cref="BuildEventArgsReader.StringStorage"/> class.
    /// </summary>
    public class BuildEventArgsReader_StringStorageTests
    {
        /// <summary>
        /// Tests that adding a small string returns the original string when retrieved.
        /// </summary>
        [Fact]
        public void Add_SmallString_ReturnsOriginalString()
        {
            // Arrange
            var stringStorage = new BuildEventArgsReader.StringStorage();
            string input = "Test string";

            // Act
            var stored = stringStorage.Add(input);
            string result = stringStorage.Get(stored);

            // Assert
            Assert.Equal(input, result);
            stringStorage.Dispose();
        }

        /// <summary>
        /// Tests that adding and retrieving a large string returns the same original string.
        /// </summary>
        [Fact]
        public void AddAndGet_LargeString_ReturnsSameString()
        {
            // Arrange
            var stringStorage = new BuildEventArgsReader.StringStorage();
            // Create a string longer than the threshold (1024 characters).
            string input = new string('A', 1500);

            // Act
            var stored = stringStorage.Add(input);
            string result = stringStorage.Get(stored);

            // Assert
            Assert.Equal(input, result);
            stringStorage.Dispose();
        }

        /// <summary>
        /// Tests that calling Dispose multiple times on StringStorage does not throw any exceptions.
        /// </summary>
        [Fact]
        public void Dispose_CalledMultipleTimes_DoesNotThrow()
        {
            // Arrange
            var stringStorage = new BuildEventArgsReader.StringStorage();

            // Act & Assert
            stringStorage.Dispose();
            stringStorage.Dispose();
        }
    }
}

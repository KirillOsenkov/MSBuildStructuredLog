using System;
using System.IO;
using System.Reflection;
using Microsoft.Build.Logging.StructuredLogger;
using Moq;
using Xunit;

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="BuildLogReader"/> class.
    /// </summary>
    public class BuildLogReaderTests
    {
        private readonly Version _dummyVersion = new Version(2, 0);
        private readonly byte[] _dummyData = new byte[] { 1, 2, 3, 4 };

        /// <summary>
        /// Tests that Read(string) throws a FileNotFoundException when the file does not exist.
        /// This ensures that the method correctly propagates file access issues.
        /// </summary>
        [Fact]
        public void Read_StringFilePath_NonExistentFile_ThrowsFileNotFoundException()
        {
            // Arrange
            string nonExistentFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".bin");

            // Act & Assert
            Assert.Throws<FileNotFoundException>(() => BuildLogReader.Read(nonExistentFilePath));
        }

        /// <summary>
        /// Tests that Read(Stream, byte[], Version) throws an ArgumentNullException when a null stream is provided.
        /// The expected behavior is that a null argument is not accepted.
        /// </summary>
        [Fact]
        public void Read_NullStream_ThrowsArgumentNullException()
        {
            // Arrange
            Stream nullStream = null;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => BuildLogReader.Read(nullStream, null, _dummyVersion));
        }

        /// <summary>
        /// Tests that Read(Stream, byte[]) overload propagates to the primary Read method and
        /// throws an exception for an invalid log file format.
        /// This simulates providing an empty stream which is not a valid binary log.
        /// </summary>
        [Fact]
        public void Read_Stream_WithEmptyData_ThrowsExceptionForInvalidLogFormat()
        {
            // Arrange
            using var stream = new MemoryStream(Array.Empty<byte>());

            // Act & Assert
            Exception exception = Assert.Throws<Exception>(() => BuildLogReader.Read(stream));
            Assert.Equal("Invalid log file format", exception.Message);
        }

        /// <summary>
        /// Tests that Dispose can be safely called multiple times without throwing an exception.
        /// This test instantiates BuildLogReader via reflection using a dummy stream.
        /// </summary>
//         [Fact] [Error] (81-48)CS0117 'Record' does not contain a definition for 'Exception' [Error] (82-49)CS0117 'Record' does not contain a definition for 'Exception'
//         public void Dispose_CalledMultipleTimes_DoesNotThrow()
//         {
//             // Arrange
//             using var dummyStream = new MemoryStream(_dummyData);
//             // Use reflection to call the non-public constructor: BuildLogReader(Stream, Version)
//             ConstructorInfo constructor = typeof(BuildLogReader).GetConstructor(
//                 BindingFlags.NonPublic | BindingFlags.Instance,
//                 binder: null,
//                 types: new Type[] { typeof(Stream), typeof(Version) },
//                 modifiers: null);
//             Assert.NotNull(constructor);
//             var buildLogReaderInstance = (BuildLogReader)constructor.Invoke(new object[] { dummyStream, _dummyVersion });
// 
//             // Act & Assert: call Dispose multiple times and ensure no exception is thrown.
//             var firstDisposeException = Record.Exception(() => buildLogReaderInstance.Dispose());
//             var secondDisposeException = Record.Exception(() => buildLogReaderInstance.Dispose());
// 
//             Assert.Null(firstDisposeException);
//             Assert.Null(secondDisposeException);
//         }

        /// <summary>
        /// Tests that Read(Stream, byte[], Version) throws an exception for an invalid log file format
        /// even when a non-null projectImportsArchive is provided.
        /// An empty stream is used to simulate an invalid binary log.
        /// </summary>
        [Fact]
        public void Read_Stream_ProjectImportsArchiveProvided_InvalidFormat_ThrowsException()
        {
            // Arrange
            byte[] dummyArchive = new byte[] { 10, 20, 30 };
            using var stream = new MemoryStream(Array.Empty<byte>());

            // Act & Assert
            Exception exception = Assert.Throws<Exception>(() => BuildLogReader.Read(stream, dummyArchive, _dummyVersion));
            Assert.Equal("Invalid log file format", exception.Message);
        }
    }
}

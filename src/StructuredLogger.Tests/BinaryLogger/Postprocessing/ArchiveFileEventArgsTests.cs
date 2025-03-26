// using Microsoft.Build.Logging.StructuredLogger;
// using Moq;
// using System;
// using Xunit;
// 
// namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
// {
//     /// <summary>
//     /// Unit tests for the <see cref="ArchiveFileEventArgs"/> class.
//     /// </summary>
//     public class ArchiveFileEventArgsTests
//     {
//         /// <summary>
//         /// Verifies that the constructor sets the ArchiveFile property when a non-null ArchiveFile is provided.
//         /// </summary>
//         [Fact]
//         public void Constructor_WithNonNullArchiveFile_SetsArchiveFileProperty()
//         {
//             // Arrange
//             var dummyArchiveFile = new DummyArchiveFile();
// 
//             // Act
//             var eventArgs = new ArchiveFileEventArgs(dummyArchiveFile);
// 
//             // Assert
//             Assert.Equal(dummyArchiveFile, eventArgs.ArchiveFile);
//         }
// 
//         /// <summary>
//         /// Verifies that the constructor correctly handles a null ArchiveFile.
//         /// </summary>
//         [Fact]
//         public void Constructor_WithNullArchiveFile_SetsArchiveFilePropertyToNull()
//         {
//             // Arrange & Act
//             var eventArgs = new ArchiveFileEventArgs(null);
// 
//             // Assert
//             Assert.Null(eventArgs.ArchiveFile);
//         }
// 
//         /// <summary>
//         /// Verifies that the ArchiveFile property setter updates the property to a new non-null value.
//         /// </summary>
//         [Fact]
//         public void ArchiveFile_Setter_WithNewNonNullValue_UpdatesArchiveFileProperty()
//         {
//             // Arrange
//             var initialArchiveFile = new DummyArchiveFile();
//             var newArchiveFile = new DummyArchiveFile();
//             var eventArgs = new ArchiveFileEventArgs(initialArchiveFile);
// 
//             // Act
//             eventArgs.ArchiveFile = newArchiveFile;
// 
//             // Assert
//             Assert.Equal(newArchiveFile, eventArgs.ArchiveFile);
//         }
// 
//         /// <summary>
//         /// Verifies that the ArchiveFile property setter updates the property to null.
//         /// </summary>
//         [Fact]
//         public void ArchiveFile_Setter_WithNullValue_UpdatesArchiveFilePropertyToNull()
//         {
//             // Arrange
//             var initialArchiveFile = new DummyArchiveFile();
//             var eventArgs = new ArchiveFileEventArgs(initialArchiveFile);
// 
//             // Act
//             eventArgs.ArchiveFile = null;
// 
//             // Assert
//             Assert.Null(eventArgs.ArchiveFile);
//         }
//     }
// 
//     /// <summary>
//     /// A dummy implementation of ArchiveFile for unit testing purposes.
//     /// </summary>
// //     internal class DummyArchiveFile : ArchiveFile [Error] (81-20)CS7036 There is no argument given that corresponds to the required parameter 'fullPath' of 'ArchiveFile.ArchiveFile(string, string)'
// //     {
// //         // This dummy class serves as a stub for ArchiveFile.
// //         // It can be expanded with additional members to simulate real scenarios if necessary.
// //     }
// }

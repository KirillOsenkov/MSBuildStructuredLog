using System;
using System.IO;
using System.IO.Compression;
using Microsoft.Build.Logging.StructuredLogger;
using Moq;
using Xunit;

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="ArchiveFile"/> class.
    /// </summary>
    public class ArchiveFileTests
    {
        /// <summary>
        /// Tests that the constructor correctly sets the FullPath and Text properties.
        /// </summary>
        [Fact]
        public void Constructor_WithValidParameters_SetsProperties()
        {
            // Arrange
            string expectedFullPath = "dummyPath.txt";
            string expectedText = "dummy content";

            // Act
            ArchiveFile archiveFile = new ArchiveFile(expectedFullPath, expectedText);

            // Assert
            Assert.Equal(expectedFullPath, archiveFile.FullPath);
            Assert.Equal(expectedText, archiveFile.Text);
        }

        /// <summary>
        /// Tests that GetText returns the entire contents of the ZipArchiveEntry stream.
        /// </summary>
//         [Fact] [Error] (42-20)CS1069 The type name 'ZipArchive' could not be found in the namespace 'System.IO.Compression'. This type has been forwarded to assembly 'System.IO.Compression, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089' Consider adding a reference to that assembly. [Error] (42-45)CS1069 The type name 'ZipArchive' could not be found in the namespace 'System.IO.Compression'. This type has been forwarded to assembly 'System.IO.Compression, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089' Consider adding a reference to that assembly. [Error] (42-67)CS0103 The name 'ZipArchiveMode' does not exist in the current context [Error] (44-17)CS1069 The type name 'ZipArchiveEntry' could not be found in the namespace 'System.IO.Compression'. This type has been forwarded to assembly 'System.IO.Compression, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089' Consider adding a reference to that assembly. [Error] (52-20)CS1069 The type name 'ZipArchive' could not be found in the namespace 'System.IO.Compression'. This type has been forwarded to assembly 'System.IO.Compression, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089' Consider adding a reference to that assembly. [Error] (52-45)CS1069 The type name 'ZipArchive' could not be found in the namespace 'System.IO.Compression'. This type has been forwarded to assembly 'System.IO.Compression, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089' Consider adding a reference to that assembly. [Error] (52-67)CS0103 The name 'ZipArchiveMode' does not exist in the current context [Error] (54-17)CS1069 The type name 'ZipArchiveEntry' could not be found in the namespace 'System.IO.Compression'. This type has been forwarded to assembly 'System.IO.Compression, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089' Consider adding a reference to that assembly. [Error] (58-52)CS0117 'ArchiveFile' does not contain a definition for 'GetText'
//         public void GetText_WithValidZipEntry_ReturnsCompleteText()
//         {
//             // Arrange
//             string expectedContent = "This is test content for the zip entry.";
//             using MemoryStream zipStream = new MemoryStream();
//             using (ZipArchive archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
//             {
//                 ZipArchiveEntry entry = archive.CreateEntry("test.txt");
//                 using (StreamWriter writer = new StreamWriter(entry.Open()))
//                 {
//                     writer.Write(expectedContent);
//                 }
//             }
// 
//             zipStream.Seek(0, SeekOrigin.Begin);
//             using (ZipArchive archive = new ZipArchive(zipStream, ZipArchiveMode.Read))
//             {
//                 ZipArchiveEntry entry = archive.GetEntry("test.txt");
//                 Assert.NotNull(entry);
// 
//                 // Act
//                 string actualContent = ArchiveFile.GetText(entry);
// 
//                 // Assert
//                 Assert.Equal(expectedContent, actualContent);
//             }
//         }

        /// <summary>
        /// Tests that From(ZipArchiveEntry, bool) returns an ArchiveFile with adjusted path when adjustPath is true.
        /// </summary>
//         [Fact] [Error] (75-20)CS1069 The type name 'ZipArchive' could not be found in the namespace 'System.IO.Compression'. This type has been forwarded to assembly 'System.IO.Compression, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089' Consider adding a reference to that assembly. [Error] (75-45)CS1069 The type name 'ZipArchive' could not be found in the namespace 'System.IO.Compression'. This type has been forwarded to assembly 'System.IO.Compression, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089' Consider adding a reference to that assembly. [Error] (75-67)CS0103 The name 'ZipArchiveMode' does not exist in the current context [Error] (77-17)CS1069 The type name 'ZipArchiveEntry' could not be found in the namespace 'System.IO.Compression'. This type has been forwarded to assembly 'System.IO.Compression, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089' Consider adding a reference to that assembly. [Error] (84-20)CS1069 The type name 'ZipArchive' could not be found in the namespace 'System.IO.Compression'. This type has been forwarded to assembly 'System.IO.Compression, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089' Consider adding a reference to that assembly. [Error] (84-45)CS1069 The type name 'ZipArchive' could not be found in the namespace 'System.IO.Compression'. This type has been forwarded to assembly 'System.IO.Compression, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089' Consider adding a reference to that assembly. [Error] (84-67)CS0103 The name 'ZipArchiveMode' does not exist in the current context [Error] (86-17)CS1069 The type name 'ZipArchiveEntry' could not be found in the namespace 'System.IO.Compression'. This type has been forwarded to assembly 'System.IO.Compression, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089' Consider adding a reference to that assembly. [Error] (90-55)CS0117 'ArchiveFile' does not contain a definition for 'From' [Error] (91-51)CS0117 'ArchiveFile' does not contain a definition for 'CalculateArchivePath'
//         public void From_WithAdjustPathTrue_ReturnsArchiveFileWithAdjustedPathAndText()
//         {
//             // Arrange
//             string originalEntryName = "C\\folder\\file.txt";
//             string entryContent = "Test content";
//             using MemoryStream zipStream = new MemoryStream();
//             using (ZipArchive archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
//             {
//                 ZipArchiveEntry entry = archive.CreateEntry(originalEntryName);
//                 using (StreamWriter writer = new StreamWriter(entry.Open()))
//                 {
//                     writer.Write(entryContent);
//                 }
//             }
//             zipStream.Seek(0, SeekOrigin.Begin);
//             using (ZipArchive archive = new ZipArchive(zipStream, ZipArchiveMode.Read))
//             {
//                 ZipArchiveEntry entry = archive.GetEntry(originalEntryName);
//                 Assert.NotNull(entry);
// 
//                 // Act
//                 ArchiveFile archiveFile = ArchiveFile.From(entry, adjustPath: true);
//                 string expectedPath = ArchiveFile.CalculateArchivePath(originalEntryName);
// 
//                 // Assert
//                 Assert.Equal(expectedPath, archiveFile.FullPath);
//                 Assert.Equal(entryContent, archiveFile.Text);
//             }
//         }

        /// <summary>
        /// Tests that From(ZipArchiveEntry, bool) returns an ArchiveFile with unadjusted path when adjustPath is false.
        /// </summary>
//         [Fact] [Error] (109-20)CS1069 The type name 'ZipArchive' could not be found in the namespace 'System.IO.Compression'. This type has been forwarded to assembly 'System.IO.Compression, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089' Consider adding a reference to that assembly. [Error] (109-45)CS1069 The type name 'ZipArchive' could not be found in the namespace 'System.IO.Compression'. This type has been forwarded to assembly 'System.IO.Compression, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089' Consider adding a reference to that assembly. [Error] (109-67)CS0103 The name 'ZipArchiveMode' does not exist in the current context [Error] (111-17)CS1069 The type name 'ZipArchiveEntry' could not be found in the namespace 'System.IO.Compression'. This type has been forwarded to assembly 'System.IO.Compression, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089' Consider adding a reference to that assembly. [Error] (118-20)CS1069 The type name 'ZipArchive' could not be found in the namespace 'System.IO.Compression'. This type has been forwarded to assembly 'System.IO.Compression, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089' Consider adding a reference to that assembly. [Error] (118-45)CS1069 The type name 'ZipArchive' could not be found in the namespace 'System.IO.Compression'. This type has been forwarded to assembly 'System.IO.Compression, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089' Consider adding a reference to that assembly. [Error] (118-67)CS0103 The name 'ZipArchiveMode' does not exist in the current context [Error] (120-17)CS1069 The type name 'ZipArchiveEntry' could not be found in the namespace 'System.IO.Compression'. This type has been forwarded to assembly 'System.IO.Compression, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089' Consider adding a reference to that assembly. [Error] (124-55)CS0117 'ArchiveFile' does not contain a definition for 'From'
//         public void From_WithAdjustPathFalse_ReturnsArchiveFileWithOriginalPathAndText()
//         {
//             // Arrange
//             string originalEntryName = "C\\folder\\file.txt";
//             string entryContent = "Sample text";
//             using MemoryStream zipStream = new MemoryStream();
//             using (ZipArchive archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
//             {
//                 ZipArchiveEntry entry = archive.CreateEntry(originalEntryName);
//                 using (StreamWriter writer = new StreamWriter(entry.Open()))
//                 {
//                     writer.Write(entryContent);
//                 }
//             }
//             zipStream.Seek(0, SeekOrigin.Begin);
//             using (ZipArchive archive = new ZipArchive(zipStream, ZipArchiveMode.Read))
//             {
//                 ZipArchiveEntry entry = archive.GetEntry(originalEntryName);
//                 Assert.NotNull(entry);
// 
//                 // Act
//                 ArchiveFile archiveFile = ArchiveFile.From(entry, adjustPath: false);
// 
//                 // Assert
//                 Assert.Equal(originalEntryName, archiveFile.FullPath);
//                 Assert.Equal(entryContent, archiveFile.Text);
//             }
//         }

        /// <summary>
        /// Tests that the From(ZipArchiveEntry) overload calls the underlying method with adjustPath set to true.
        /// </summary>
//         [Fact] [Error] (142-20)CS1069 The type name 'ZipArchive' could not be found in the namespace 'System.IO.Compression'. This type has been forwarded to assembly 'System.IO.Compression, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089' Consider adding a reference to that assembly. [Error] (142-45)CS1069 The type name 'ZipArchive' could not be found in the namespace 'System.IO.Compression'. This type has been forwarded to assembly 'System.IO.Compression, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089' Consider adding a reference to that assembly. [Error] (142-67)CS0103 The name 'ZipArchiveMode' does not exist in the current context [Error] (144-17)CS1069 The type name 'ZipArchiveEntry' could not be found in the namespace 'System.IO.Compression'. This type has been forwarded to assembly 'System.IO.Compression, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089' Consider adding a reference to that assembly. [Error] (151-20)CS1069 The type name 'ZipArchive' could not be found in the namespace 'System.IO.Compression'. This type has been forwarded to assembly 'System.IO.Compression, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089' Consider adding a reference to that assembly. [Error] (151-45)CS1069 The type name 'ZipArchive' could not be found in the namespace 'System.IO.Compression'. This type has been forwarded to assembly 'System.IO.Compression, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089' Consider adding a reference to that assembly. [Error] (151-67)CS0103 The name 'ZipArchiveMode' does not exist in the current context [Error] (153-17)CS1069 The type name 'ZipArchiveEntry' could not be found in the namespace 'System.IO.Compression'. This type has been forwarded to assembly 'System.IO.Compression, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089' Consider adding a reference to that assembly. [Error] (157-55)CS0117 'ArchiveFile' does not contain a definition for 'From' [Error] (160-51)CS0117 'ArchiveFile' does not contain a definition for 'CalculateArchivePath'
//         public void From_Overload_ReturnsArchiveFileWithAdjustedPathAndText()
//         {
//             // Arrange
//             string originalEntryName = "C\\folder\\file.txt";
//             string entryContent = "Overload test content";
//             using MemoryStream zipStream = new MemoryStream();
//             using (ZipArchive archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
//             {
//                 ZipArchiveEntry entry = archive.CreateEntry(originalEntryName);
//                 using (StreamWriter writer = new StreamWriter(entry.Open()))
//                 {
//                     writer.Write(entryContent);
//                 }
//             }
//             zipStream.Seek(0, SeekOrigin.Begin);
//             using (ZipArchive archive = new ZipArchive(zipStream, ZipArchiveMode.Read))
//             {
//                 ZipArchiveEntry entry = archive.GetEntry(originalEntryName);
//                 Assert.NotNull(entry);
// 
//                 // Act
//                 ArchiveFile archiveFile = ArchiveFile.From(entry);
// 
//                 // Assert
//                 string expectedPath = ArchiveFile.CalculateArchivePath(originalEntryName);
//                 Assert.Equal(expectedPath, archiveFile.FullPath);
//                 Assert.Equal(entryContent, archiveFile.Text);
//             }
//         }

        /// <summary>
        /// Tests that CalculateArchivePath adjusts the drive letter path when the condition is met.
        /// </summary>
//         [Fact] [Error] (179-45)CS0117 'ArchiveFile' does not contain a definition for 'CalculateArchivePath'
//         public void CalculateArchivePath_WithDriveLetterAdjustment_ReturnsAdjustedPath()
//         {
//             // Arrange
//             string inputPath = "C\\folder\\file.txt";
//             // Expected adjustment: "C:" + substring(1) then normalized.
//             // Assuming TextUtilities.NormalizeFilePath acts as an identity function for this test scenario.
//             string expectedAdjustedPath = "C:" + inputPath.Substring(1);
// 
//             // Act
//             string actualPath = ArchiveFile.CalculateArchivePath(inputPath);
// 
//             // Assert
//             Assert.Equal(expectedAdjustedPath, actualPath);
//         }

        /// <summary>
        /// Tests that CalculateArchivePath does not adjust the path when the condition is not met.
        /// </summary>
//         [Fact] [Error] (198-45)CS0117 'ArchiveFile' does not contain a definition for 'CalculateArchivePath'
//         public void CalculateArchivePath_WithoutDriveLetterAdjustment_ReturnsNormalizedPath()
//         {
//             // Arrange
//             string inputPath = "folder\\file.txt";
//             // Since the condition is not met, the path should be normalized only.
//             // Assuming TextUtilities.NormalizeFilePath acts as identity.
//             string expectedPath = inputPath;
// 
//             // Act
//             string actualPath = ArchiveFile.CalculateArchivePath(inputPath);
// 
//             // Assert
//             Assert.Equal(expectedPath, actualPath);
//         }

        /// <summary>
        /// Tests that CalculateArchivePath throws a NullReferenceException when the input is null.
        /// </summary>
//         [Fact] [Error] (214-69)CS0117 'ArchiveFile' does not contain a definition for 'CalculateArchivePath'
//         public void CalculateArchivePath_NullInput_ThrowsNullReferenceException()
//         {
//             // Arrange
//             string inputPath = null;
// 
//             // Act & Assert
//             Assert.Throws<NullReferenceException>(() => ArchiveFile.CalculateArchivePath(inputPath));
//         }
    }
}

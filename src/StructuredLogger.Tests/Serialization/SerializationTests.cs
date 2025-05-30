using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using Moq;
using Xunit;
using Microsoft.Build.Logging.StructuredLogger;

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="Serialization"/> class.
    /// </summary>
    public class SerializationTests
    {
        /// <summary>
        /// Tests that IsValidXmlElementName returns true for valid XML element names.
        /// </summary>
        /// <param name="name">The XML element name.</param>
        [Theory]
        [InlineData("Element1")]
        [InlineData("A")]
        [InlineData("Test123")]
        public void IsValidXmlElementName_ValidName_ReturnsTrue(string name)
        {
            // Act
            bool result = Serialization.IsValidXmlElementName(name);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that IsValidXmlElementName returns false for invalid XML element names.
        /// </summary>
        /// <param name="name">The XML element name.</param>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("1Invalid")]
        [InlineData("Invalid-Name")]
        public void IsValidXmlElementName_InvalidName_ReturnsFalse(string name)
        {
            // Act
            bool result = Serialization.IsValidXmlElementName(name);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that GetBoolean returns the correct boolean value for valid string inputs.
        /// </summary>
        /// <param name="text">The boolean string value.</param>
        /// <param name="expected">The expected boolean result.</param>
//         [Theory] [Error] (66-41)CS0117 'Serialization' does not contain a definition for 'GetBoolean'
//         [InlineData("true", true)]
//         [InlineData("TrUe", true)]
//         [InlineData("false", false)]
//         [InlineData("False", false)]
//         public void GetBoolean_ValidText_ReturnsParsedValue(string text, bool expected)
//         {
//             // Act
//             bool result = Serialization.GetBoolean(text);
// 
//             // Assert
//             Assert.Equal(expected, result);
//         }

        /// <summary>
        /// Tests that GetBoolean returns false for null or invalid string inputs.
        /// </summary>
//         [Fact] [Error] (83-45)CS0117 'Serialization' does not contain a definition for 'GetBoolean' [Error] (84-48)CS0117 'Serialization' does not contain a definition for 'GetBoolean'
//         public void GetBoolean_NullOrInvalidText_ReturnsFalse()
//         {
//             // Arrange
//             string nullText = null;
//             string invalidText = "notabool";
// 
//             // Act
//             bool resultNull = Serialization.GetBoolean(nullText);
//             bool resultInvalid = Serialization.GetBoolean(invalidText);
// 
//             // Assert
//             Assert.False(resultNull);
//             Assert.False(resultInvalid);
//         }

        /// <summary>
        /// Tests that GetDateTime returns the correct DateTime for valid date strings.
        /// </summary>
//         [Fact] [Error] (102-45)CS0117 'Serialization' does not contain a definition for 'GetDateTime'
//         public void GetDateTime_ValidText_ReturnsParsedDateTime()
//         {
//             // Arrange
//             string dateText = "2020-01-01T10:20:30";
//             DateTime expected = DateTime.Parse(dateText);
// 
//             // Act
//             DateTime result = Serialization.GetDateTime(dateText);
// 
//             // Assert
//             Assert.Equal(expected, result);
//         }

        /// <summary>
        /// Tests that GetDateTime returns default DateTime for null or invalid input.
        /// </summary>
//         [Theory] [Error] (117-45)CS0117 'Serialization' does not contain a definition for 'GetDateTime'
//         [InlineData(null)]
//         [InlineData("Not a date")]
//         public void GetDateTime_NullOrInvalidText_ReturnsDefaultDateTime(string text)
//         {
//             // Act
//             DateTime result = Serialization.GetDateTime(text);
// 
//             // Assert
//             Assert.Equal(default(DateTime), result);
//         }

        /// <summary>
        /// Tests that GetInteger returns the correct integer value for valid numeric strings.
        /// </summary>
//         [Theory] [Error] (132-40)CS0117 'Serialization' does not contain a definition for 'GetInteger'
//         [InlineData("123", 123)]
//         [InlineData("-45", -45)]
//         public void GetInteger_ValidText_ReturnsParsedInteger(string text, int expected)
//         {
//             // Act
//             int result = Serialization.GetInteger(text);
// 
//             // Assert
//             Assert.Equal(expected, result);
//         }

        /// <summary>
        /// Tests that GetInteger returns 0 for null or invalid input.
        /// </summary>
//         [Theory] [Error] (147-40)CS0117 'Serialization' does not contain a definition for 'GetInteger'
//         [InlineData(null)]
//         [InlineData("abc")]
//         public void GetInteger_NullOrInvalidText_ReturnsZero(string text)
//         {
//             // Act
//             int result = Serialization.GetInteger(text);
// 
//             // Assert
//             Assert.Equal(0, result);
//         }

        /// <summary>
        /// Tests the Write7BitEncodedInt and Read7BitEncodedInt extension methods for correct encoding and decoding.
        /// </summary>
        /// <param name="value">The integer value to test.</param>
//         [Theory] [Error] (172-31)CS0117 'Serialization' does not contain a definition for 'Write7BitEncodedInt' [Error] (180-45)CS0117 'Serialization' does not contain a definition for 'Read7BitEncodedInt'
//         [InlineData(0)]
//         [InlineData(1)]
//         [InlineData(127)]
//         [InlineData(128)]
//         [InlineData(16384)]
//         [InlineData(int.MaxValue)]
//         [InlineData(-1)]
//         public void WriteAndRead7BitEncodedInt_Value_ReturnsSameValue(int value)
//         {
//             // Arrange
//             using var ms = new MemoryStream();
//             using (var writer = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
//             {
//                 // Act
//                 Serialization.Write7BitEncodedInt(writer, value);
//             }
// 
//             ms.Position = 0;
// 
//             using (var reader = new BinaryReader(ms, System.Text.Encoding.UTF8, leaveOpen: true))
//             {
//                 // Act
//                 int decoded = Serialization.Read7BitEncodedInt(reader);
// 
//                 // Assert
//                 Assert.Equal(value, decoded);
//             }
//         }

        /// <summary>
        /// Tests WriteStringsToFile and ReadStringsFromFile to ensure roundtrip integrity of string arrays.
        /// </summary>
//         [Fact] [Error] (199-31)CS0117 'Serialization' does not contain a definition for 'WriteStringsToFile' [Error] (200-51)CS0117 'Serialization' does not contain a definition for 'ReadStringsFromFile' [Error] (203-54)CS1503 Argument 2: cannot convert from 'method group' to 'int'
//         public void WriteAndReadStrings_Roundtrip_ReturnsSameStrings()
//         {
//             // Arrange
//             string[] expectedStrings = new[] { "first", "second", "third" };
//             string tempFile = Path.GetTempFileName();
//             try
//             {
//                 // Act
//                 Serialization.WriteStringsToFile(tempFile, expectedStrings);
//                 var actualStrings = Serialization.ReadStringsFromFile(tempFile);
// 
//                 // Assert
//                 Assert.Equal(expectedStrings.Length, actualStrings.Count);
//                 Assert.True(expectedStrings.SequenceEqual(actualStrings));
//             }
//             finally
//             {
//                 if (File.Exists(tempFile))
//                 {
//                     File.Delete(tempFile);
//                 }
//             }
//         }

        /// <summary>
        /// Tests DetectLogFormat with a file that has fewer than 4 bytes.
        /// </summary>
//         [Fact] [Error] (227-47)CS0117 'Serialization' does not contain a definition for 'DetectLogFormat'
//         public void DetectLogFormat_FileTooShort_ReturnsNull()
//         {
//             // Arrange
//             string tempFile = Path.GetTempFileName();
//             try
//             {
//                 File.WriteAllBytes(tempFile, new byte[] { 0x1 });
//                 // Act
//                 string format = Serialization.DetectLogFormat(tempFile);
//                 // Assert
//                 Assert.Null(format);
//             }
//             finally
//             {
//                 if (File.Exists(tempFile))
//                 {
//                     File.Delete(tempFile);
//                 }
//             }
//         }

        /// <summary>
        /// Tests DetectLogFormat with a file that starts with the GZip signature.
        /// </summary>
//         [Fact] [Error] (253-47)CS0117 'Serialization' does not contain a definition for 'DetectLogFormat'
//         public void DetectLogFormat_WithGZipSignature_ReturnsBinlog()
//         {
//             // Arrange
//             string tempFile = Path.GetTempFileName();
//             byte[] bytes = new byte[] { 0x1F, 0x8B, 0, 0 };
//             try
//             {
//                 File.WriteAllBytes(tempFile, bytes);
//                 // Act
//                 string format = Serialization.DetectLogFormat(tempFile);
//                 // Assert
//                 Assert.Equal(".binlog", format);
//             }
//             finally
//             {
//                 if (File.Exists(tempFile))
//                 {
//                     File.Delete(tempFile);
//                 }
//             }
//         }

        /// <summary>
        /// Tests DetectLogFormat with a file that starts with version bytes 1 and 2.
        /// </summary>
//         [Fact] [Error] (279-47)CS0117 'Serialization' does not contain a definition for 'DetectLogFormat'
//         public void DetectLogFormat_WithVersion1_2_ReturnsVersionString()
//         {
//             // Arrange
//             string tempFile = Path.GetTempFileName();
//             byte[] bytes = new byte[] { 1, 2, 0, 0 };
//             try
//             {
//                 File.WriteAllBytes(tempFile, bytes);
//                 // Act
//                 string format = Serialization.DetectLogFormat(tempFile);
//                 // Assert
//                 Assert.Equal("1.2", format);
//             }
//             finally
//             {
//                 if (File.Exists(tempFile))
//                 {
//                     File.Delete(tempFile);
//                 }
//             }
//         }

        /// <summary>
        /// Tests DetectLogFormat with a file that starts with 0x1 (indicating .buildlog).
        /// </summary>
//         [Fact] [Error] (305-47)CS0117 'Serialization' does not contain a definition for 'DetectLogFormat'
//         public void DetectLogFormat_WithBuildlogSignature_ReturnsBuildlog()
//         {
//             // Arrange
//             string tempFile = Path.GetTempFileName();
//             byte[] bytes = new byte[] { 0x1, 0, 0, 0 };
//             try
//             {
//                 File.WriteAllBytes(tempFile, bytes);
//                 // Act
//                 string format = Serialization.DetectLogFormat(tempFile);
//                 // Assert
//                 Assert.Equal(".buildlog", format);
//             }
//             finally
//             {
//                 if (File.Exists(tempFile))
//                 {
//                     File.Delete(tempFile);
//                 }
//             }
//         }

        /// <summary>
        /// Tests that Read returns null when called with an unrecognized file extension.
        /// </summary>
//         [Fact] [Error] (325-40)CS0117 'Serialization' does not contain a definition for 'Read'
//         public void Read_UnrecognizedExtension_ReturnsNull()
//         {
//             // Act
//             var result = Serialization.Read("test.unknown");
// 
//             // Assert
//             Assert.Null(result);
//         }

        /// <summary>
        /// Tests GetNodeName to ensure it returns the Folder's Name when it is a valid XML element name.
        /// </summary>
//         [Fact] [Error] (356-57)CS1503 Argument 1: cannot convert from 'Microsoft.Build.Logging.StructuredLogger.UnitTests.Folder' to 'Microsoft.Build.Logging.StructuredLogger.BaseNode'
//         public void GetNodeName_FolderWithValidName_ReturnsName()
//         {
//             // Arrange
//             // Creating an instance of Folder and setting its Name property via reflection since we assume Folder exists in the production assembly.
//             var folder = (Folder)Activator.CreateInstance(typeof(Folder));
//             PropertyInfo nameProperty = typeof(Folder).GetProperty("Name");
//             if(nameProperty != null && nameProperty.CanWrite)
//             {
//                 nameProperty.SetValue(folder, "ValidName");
//             }
//             else
//             {
//                 // If Name property is not available, use reflection to set the field if possible.
//                 FieldInfo nameField = typeof(Folder).GetField("Name");
//                 if(nameField != null)
//                 {
//                     nameField.SetValue(folder, "ValidName");
//                 }
//             }
// 
//             // Act
//             string nodeName = Serialization.GetNodeName(folder);
// 
//             // Assert
//             Assert.Equal("ValidName", nodeName);
//         }

        /// <summary>
        /// Tests GetNodeName to ensure it returns the node type name when Folder's Name is not a valid XML element name.
        /// </summary>
//         [Fact] [Error] (385-57)CS1503 Argument 1: cannot convert from 'Microsoft.Build.Logging.StructuredLogger.UnitTests.Folder' to 'Microsoft.Build.Logging.StructuredLogger.BaseNode'
//         public void GetNodeName_FolderWithInvalidName_ReturnsTypeName()
//         {
//             // Arrange
//             var folder = (Folder)Activator.CreateInstance(typeof(Folder));
//             PropertyInfo nameProperty = typeof(Folder).GetProperty("Name");
//             if(nameProperty != null && nameProperty.CanWrite)
//             {
//                 nameProperty.SetValue(folder, "123Invalid");
//             }
//             else
//             {
//                 FieldInfo nameField = typeof(Folder).GetField("Name");
//                 if(nameField != null)
//                 {
//                     nameField.SetValue(folder, "123Invalid");
//                 }
//             }
// 
//             // Act
//             string nodeName = Serialization.GetNodeName(folder);
// 
//             // Assert
//             Assert.Equal(typeof(Folder).Name, nodeName);
//         }

        /// <summary>
        /// Tests CreateNode to ensure it returns an instance of Folder when the name is not found in ObjectModelTypes.
        /// </summary>
//         [Fact] [Error] (401-43)CS0117 'Serialization' does not contain a definition for 'CreateNode'
//         public void CreateNode_UnknownType_ReturnsFolderInstance()
//         {
//             // Arrange
//             string typeName = "NonExistentType";
// 
//             // Act
//             BaseNode node = Serialization.CreateNode(typeName);
// 
//             // Assert
//             Assert.NotNull(node);
//             Assert.IsType<Folder>(node);
//         }

        /// <summary>
        /// Tests CreateNode to ensure it returns an instance corresponding to a known type name.
        /// Note: This test assumes that "Folder" exists in the ObjectModelTypes dictionary.
        /// </summary>
//         [Fact] [Error] (419-43)CS0117 'Serialization' does not contain a definition for 'CreateNode'
//         public void CreateNode_KnownType_ReturnsCorrectInstance()
//         {
//             // Arrange
//             string typeName = "Folder";
// 
//             // Act
//             BaseNode node = Serialization.CreateNode(typeName);
// 
//             // Assert
//             Assert.NotNull(node);
//             Assert.IsType<Folder>(node);
//         }
    }
}

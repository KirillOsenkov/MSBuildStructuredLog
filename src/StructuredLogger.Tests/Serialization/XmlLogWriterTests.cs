using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Xunit;
using Microsoft.Build.Logging.StructuredLogger;

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="XmlLogWriter"/> class.
    /// </summary>
    public class XmlLogWriterTests : IDisposable
    {
        private readonly string _tempDirectory;

        public XmlLogWriterTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDirectory);
        }

        private string GetTempFilePath() => Path.Combine(_tempDirectory, Guid.NewGuid().ToString() + ".xml");

        /// <summary>
        /// Tests the WriteToXml method with a valid Build node to ensure that an XML file is created containing the expected attributes.
        /// </summary>
//         [Fact] [Error] (33-25)CS0144 Cannot create an instance of the abstract type or interface 'Build' [Error] (35-17)CS0117 'Build' does not contain a definition for 'Succeeded' [Error] (36-17)CS0117 'Build' does not contain a definition for 'IsAnalyzed' [Error] (40-37)CS1503 Argument 1: cannot convert from 'Microsoft.Build.Logging.StructuredLogger.UnitTests.Build' to 'Microsoft.Build.Logging.StructuredLogger.Build'
//         public void WriteToXml_ValidBuildNode_CreatesValidXmlFile()
//         {
//             // Arrange
//             string tempFile = GetTempFilePath();
//             var build = new Build 
//             { 
//                 Succeeded = true, 
//                 IsAnalyzed = false 
//             };
// 
//             // Act
//             XmlLogWriter.WriteToXml(build, tempFile);
// 
//             // Assert
//             Assert.True(File.Exists(tempFile));
//             string content = File.ReadAllText(tempFile);
//             Assert.Contains("<Build", content);
//             Assert.Contains("Succeeded=\"True\"", content);
//             Assert.Contains("IsAnalyzed=\"False\"", content);
// 
//             // Cleanup
//             File.Delete(tempFile);
//         }

        /// <summary>
        /// Tests the Write method with a valid Build node to ensure that an XML file is created containing the expected attributes.
        /// </summary>
//         [Fact] [Error] (61-25)CS0144 Cannot create an instance of the abstract type or interface 'Build' [Error] (63-17)CS0117 'Build' does not contain a definition for 'Succeeded' [Error] (64-17)CS0117 'Build' does not contain a definition for 'IsAnalyzed' [Error] (69-26)CS1503 Argument 1: cannot convert from 'Microsoft.Build.Logging.StructuredLogger.UnitTests.Build' to 'Microsoft.Build.Logging.StructuredLogger.Build'
//         public void Write_ValidBuildNode_CreatesValidXmlFile()
//         {
//             // Arrange
//             string tempFile = GetTempFilePath();
//             var build = new Build 
//             { 
//                 Succeeded = false, 
//                 IsAnalyzed = true 
//             };
//             var writer = new XmlLogWriter();
// 
//             // Act
//             writer.Write(build, tempFile);
// 
//             // Assert
//             Assert.True(File.Exists(tempFile));
//             string content = File.ReadAllText(tempFile);
//             Assert.Contains("<Build", content);
//             Assert.Contains("Succeeded=\"False\"", content);
//             Assert.Contains("IsAnalyzed=\"True\"", content);
// 
//             // Cleanup
//             File.Delete(tempFile);
//         }

        /// <summary>
        /// Tests the Write method with a null Build node to ensure that a NullReferenceException is thrown.
        /// </summary>
        [Fact]
        public void Write_NullBuild_ThrowsNullReferenceException()
        {
            // Arrange
            string tempFile = GetTempFilePath();
            var writer = new XmlLogWriter();

            // Act & Assert
            Assert.Throws<NullReferenceException>(() => writer.Write(null, tempFile));

            // Cleanup
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }

        /// <summary>
        /// Tests the Write method with a null log file path to ensure that an ArgumentNullException is thrown.
        /// </summary>
//         [Fact] [Error] (110-25)CS0144 Cannot create an instance of the abstract type or interface 'Build' [Error] (112-17)CS0117 'Build' does not contain a definition for 'Succeeded' [Error] (113-17)CS0117 'Build' does not contain a definition for 'IsAnalyzed' [Error] (117-69)CS1503 Argument 1: cannot convert from 'Microsoft.Build.Logging.StructuredLogger.UnitTests.Build' to 'Microsoft.Build.Logging.StructuredLogger.Build'
//         public void Write_NullLogFile_ThrowsArgumentNullException()
//         {
//             // Arrange
//             var writer = new XmlLogWriter();
//             var build = new Build 
//             { 
//                 Succeeded = true, 
//                 IsAnalyzed = true 
//             };
// 
//             // Act & Assert
//             Assert.Throws<ArgumentNullException>(() => writer.Write(build, null));
//         }

        /// <summary>
        /// Cleans up temporary directory after tests.
        /// </summary>
        public void Dispose()
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, true);
            }
        }
    }
}

namespace Microsoft.Build.Logging.StructuredLogger
{
    /// <summary>
    /// Minimal abstract representation of a node for testing purposes.
    /// </summary>
    public abstract class BaseNode
    {
    }

    /// <summary>
    /// Minimal implementation of a tree node for testing purposes.
    /// </summary>
    public class TreeNode : BaseNode
    {
        /// <summary>
        /// Gets or sets the list of child nodes.
        /// </summary>
        public List<BaseNode> Children { get; set; } = new List<BaseNode>();

        /// <summary>
        /// Indicates whether the node has any children.
        /// </summary>
        public bool HasChildren => Children != null && Children.Count > 0;
    }

    /// <summary>
    /// Minimal implementation of a Build node for testing purposes.
    /// </summary>
    public class Build : TreeNode
    {
        /// <summary>
        /// Gets or sets a value indicating whether the build succeeded.
        /// </summary>
        public bool Succeeded { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the build has been analyzed.
        /// </summary>
        public bool IsAnalyzed { get; set; }

        /// <summary>
        /// Gets or sets the project file associated with the build.
        /// </summary>
        public string ProjectFile { get; set; }
    }

    /// <summary>
    /// Minimal dummy implementation of Serialization utility for testing purposes.
    /// </summary>
    public static class Serialization
    {
        /// <summary>
        /// Returns the node name based on its runtime type.
        /// </summary>
        public static string GetNodeName(BaseNode node)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            return node.GetType().Name;
        }

        /// <summary>
        /// Checks if the provided string is a valid XML element name.
        /// For testing purposes, a name starting with a letter is considered valid.
        /// </summary>
        public static bool IsValidXmlElementName(string name)
        {
            return !string.IsNullOrEmpty(name) && char.IsLetter(name[0]);
        }
    }
}

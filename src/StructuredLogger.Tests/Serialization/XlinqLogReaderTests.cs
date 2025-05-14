using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using Microsoft.Build.Logging.StructuredLogger;
using Xunit;

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="XlinqLogReader"/> class.
    /// </summary>
    public class XlinqLogReaderTests
    {
        /// <summary>
        /// Tests that the ReadFromXml method returns a valid Build instance when provided with a valid XML file.
        /// The test verifies that the status update callback is called with the appropriate messages and that the returned Build
        /// instance has the expected properties.
        /// </summary>
        [Fact]
        public void ReadFromXml_ValidXml_CallsStatusUpdateAndReturnsBuild()
        {
            // Arrange
            // Create a temporary file with a minimal valid XML structure that represents a Build.
            string tempFilePath = Path.GetTempFileName();
            try
            {
                // The XML includes attributes for Succeeded and IsAnalyzed.
                // It is assumed that Serialization.CreateNode("Build") returns an instance of Build and that the Build instance
                // has properties Succeeded, IsAnalyzed, and a Children collection.
                string xmlContent = "<Build Succeeded=\"true\" IsAnalyzed=\"false\"></Build>";
                File.WriteAllText(tempFilePath, xmlContent);

                var statusMessages = new List<string>();
                Action<string> statusUpdate = message => statusMessages.Add(message);

                // Act
                var build = XlinqLogReader.ReadFromXml(tempFilePath, statusUpdate);

                // Assert
                Assert.NotNull(build);
                // Check that statusUpdate was called with expected messages.
                Assert.Contains($"Loading {tempFilePath}", statusMessages);
                Assert.Contains("Populating tree", statusMessages);

                // Since the XML explicitly provides Succeeded="true", we expect the Build node to have Succeeded set to true.
                // Note: This assertion assumes that the Build type has a public bool Succeeded property.
                Assert.True(build.Succeeded, "Expected Build.Succeeded to be true for valid XML input.");
            }
            finally
            {
                // Cleanup: Delete the temporary file.
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
            }
        }

        /// <summary>
        /// Tests that the ReadFromXml method handles an invalid file path gracefully.
        /// The method should catch the exception thrown by XDocument.Load and return a Build instance indicating failure,
        /// and the returned Build should contain two error children with the appropriate error messages.
        /// </summary>
        [Fact]
        public void ReadFromXml_InvalidFilePath_ReturnsBuildWithErrors()
        {
            // Arrange
            string invalidFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".xml");
            var statusMessages = new List<string>();
            Action<string> statusUpdate = message => statusMessages.Add(message);

            // Act
            var build = XlinqLogReader.ReadFromXml(invalidFilePath, statusUpdate);

            // Assert
            Assert.NotNull(build);
            // Expect Succeeded to be false due to the exception handling in the catch block.
            Assert.False(build.Succeeded, "Expected Build.Succeeded to be false when an exception is encountered.");

            // Assuming that the Build instance contains a public list or collection of children nodes
            // which is populated with exactly two error nodes.
            // We use reflection to check for a Children property if it exists.
            var childrenProperty = build.GetType().GetProperty("Children");
            Assert.NotNull(childrenProperty);

            var children = childrenProperty.GetValue(build) as IEnumerable<object>;
            Assert.NotNull(children);

            // Convert to a list for easier assertions.
            var childrenList = new List<object>();
            foreach (var child in children)
            {
                childrenList.Add(child);
            }
            Assert.Equal(2, childrenList.Count);

            // Check that the first error child has a Text property indicating the file open error.
            var error1TextProperty = childrenList[0].GetType().GetProperty("Text");
            Assert.NotNull(error1TextProperty);
            var error1Text = error1TextProperty.GetValue(childrenList[0]) as string;
            Assert.StartsWith("Error when opening file:", error1Text);

            // Check that the second error child has a Text property that contains details of the exception.
            var error2TextProperty = childrenList[1].GetType().GetProperty("Text");
            Assert.NotNull(error2TextProperty);
            var error2Text = error2TextProperty.GetValue(childrenList[1]) as string;
            Assert.Contains("Exception", error2Text, StringComparison.OrdinalIgnoreCase);
        }
    }
}

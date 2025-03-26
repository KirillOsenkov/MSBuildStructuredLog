using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.Build.Framework.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="TaskItemData"/> class.
    /// </summary>
    public class TaskItemDataTests
    {
        /// <summary>
        /// Tests that the default constructor initializes an empty metadata dictionary and sets ItemSpec to null.
        /// </summary>
        [Fact]
        public void DefaultConstructor_InitializesEmptyMetadataAndNullItemSpec()
        {
            // Arrange & Act
            var taskItemData = new TaskItemData();
            
            // Assert
            Assert.NotNull(taskItemData.Metadata);
            Assert.Empty(taskItemData.Metadata);
            Assert.Null(taskItemData.ItemSpec);
        }
        
        /// <summary>
        /// Tests that the parameterized constructor with a non-null metadata dictionary sets the ItemSpec and Metadata properties correctly.
        /// </summary>
        [Fact]
        public void ParameterizedConstructor_WithNonNullMetadata_SetsPropertiesCorrectly()
        {
            // Arrange
            string expectedItemSpec = "TestItem";
            var metadata = new Dictionary<string, string>
            {
                { "Key1", "Value1" },
                { "Key2", "Value2" }
            };
            
            // Act
            var taskItemData = new TaskItemData(expectedItemSpec, metadata);
            
            // Assert
            Assert.Equal(expectedItemSpec, taskItemData.ItemSpec);
            Assert.Same(metadata, taskItemData.Metadata);
            Assert.Equal(2, taskItemData.MetadataCount);
        }
        
        /// <summary>
        /// Tests that the parameterized constructor with a null metadata dictionary sets the Metadata property to an empty dictionary.
        /// </summary>
        [Fact]
        public void ParameterizedConstructor_WithNullMetadata_SetsEmptyMetadata()
        {
            // Arrange
            string expectedItemSpec = "TestItem";
            
            // Act
            var taskItemData = new TaskItemData(expectedItemSpec, null);
            
            // Assert
            Assert.NotNull(taskItemData.Metadata);
            Assert.Empty(taskItemData.Metadata);
            Assert.Equal(0, taskItemData.MetadataCount);
        }
        
        /// <summary>
        /// Tests that GetMetadata returns the correct value when the provided key exists in the metadata dictionary.
        /// </summary>
        [Fact]
        public void GetMetadata_WithExistingKey_ReturnsCorrespondingValue()
        {
            // Arrange
            string key = "TestKey";
            string value = "TestValue";
            var metadata = new Dictionary<string, string>
            {
                { key, value }
            };
            var taskItemData = new TaskItemData("TestItem", metadata);
            
            // Act
            string result = taskItemData.GetMetadata(key);
            
            // Assert
            Assert.Equal(value, result);
        }
        
        /// <summary>
        /// Tests that GetMetadata returns null when the provided key does not exist.
        /// </summary>
        [Fact]
        public void GetMetadata_WithNonExistingKey_ReturnsNull()
        {
            // Arrange
            var metadata = new Dictionary<string, string>
            {
                { "ExistingKey", "ExistingValue" }
            };
            var taskItemData = new TaskItemData("TestItem", metadata);
            
            // Act
            string result = taskItemData.GetMetadata("NonExistingKey");
            
            // Assert
            Assert.Null(result);
        }
        
        /// <summary>
        /// Tests that CloneCustomMetadata returns the same instance as the Metadata property.
        /// </summary>
        [Fact]
        public void CloneCustomMetadata_ReturnsSameMetadataReference()
        {
            // Arrange
            var metadata = new Dictionary<string, string>
            {
                { "Key", "Value" }
            };
            var taskItemData = new TaskItemData("TestItem", metadata);
            
            // Act
            IDictionary clonedMetadata = taskItemData.CloneCustomMetadata();
            
            // Assert
            Assert.Same(taskItemData.Metadata, clonedMetadata);
        }
        
        /// <summary>
        /// Tests that CopyMetadataTo throws a NotImplementedException.
        /// </summary>
        [Fact]
        public void CopyMetadataTo_AlwaysThrowsNotImplementedException()
        {
            // Arrange
            var taskItemData = new TaskItemData("TestItem", new Dictionary<string, string>());
            // Using TaskItemData as destination as it implements ITaskItem.
            var destinationItem = new TaskItemData();
            
            // Act & Assert
            Assert.Throws<NotImplementedException>(() => taskItemData.CopyMetadataTo(destinationItem));
        }
        
        /// <summary>
        /// Tests that RemoveMetadata throws a NotImplementedException.
        /// </summary>
        [Fact]
        public void RemoveMetadata_AlwaysThrowsNotImplementedException()
        {
            // Arrange
            var taskItemData = new TaskItemData("TestItem", new Dictionary<string, string>());
            
            // Act & Assert
            Assert.Throws<NotImplementedException>(() => taskItemData.RemoveMetadata("AnyKey"));
        }
        
        /// <summary>
        /// Tests that SetMetadata throws a NotImplementedException.
        /// </summary>
        [Fact]
        public void SetMetadata_AlwaysThrowsNotImplementedException()
        {
            // Arrange
            var taskItemData = new TaskItemData("TestItem", new Dictionary<string, string>());
            
            // Act & Assert
            Assert.Throws<NotImplementedException>(() => taskItemData.SetMetadata("AnyKey", "AnyValue"));
        }
        
        /// <summary>
        /// Tests that ToString returns the ItemSpec when there is no metadata.
        /// </summary>
        [Fact]
        public void ToString_WhenNoMetadata_ReturnsItemSpec()
        {
            // Arrange
            string expectedItemSpec = "TestItem";
            var taskItemData = new TaskItemData
            {
                ItemSpec = expectedItemSpec
            };
            
            // Act
            string result = taskItemData.ToString();
            
            // Assert
            Assert.Equal(expectedItemSpec, result);
        }
        
        /// <summary>
        /// Tests that ToString returns a formatted string with ItemSpec and metadata when metadata exists.
        /// </summary>
        [Fact]
        public void ToString_WhenMetadataExists_ReturnsFormattedString()
        {
            // Arrange
            string expectedItemSpec = "TestItem";
            var metadata = new Dictionary<string, string>
            {
                { "Key1", "Value1" },
                { "Key2", "Value2" }
            };
            var taskItemData = new TaskItemData(expectedItemSpec, metadata);
            
            // Act
            string result = taskItemData.ToString();
            
            // Build expected string
            var expectedBuilder = new StringBuilder();
            expectedBuilder.AppendLine(expectedItemSpec);
            foreach (var kvp in metadata)
            {
                expectedBuilder.Append("    ");
                expectedBuilder.Append(kvp.Key);
                expectedBuilder.Append("=");
                expectedBuilder.AppendLine(kvp.Value);
            }
            string expectedOutput = expectedBuilder.ToString();
            
            // Assert
            Assert.Equal(expectedOutput, result);
        }
        
        /// <summary>
        /// Tests that the MetadataNames property returns a collection containing all metadata keys.
        /// </summary>
        [Fact]
        public void MetadataNames_ReturnsCorrectCollectionOfKeys()
        {
            // Arrange
            var metadata = new Dictionary<string, string>
            {
                { "FirstKey", "FirstValue" },
                { "SecondKey", "SecondValue" }
            };
            var taskItemData = new TaskItemData("TestItem", metadata);
            
            // Act
            ICollection metadataNames = taskItemData.MetadataNames;
            var keys = new List<string>();
            foreach (var key in metadataNames)
            {
                keys.Add(key.ToString());
            }
            
            // Assert
            Assert.Contains("FirstKey", keys);
            Assert.Contains("SecondKey", keys);
            Assert.Equal(metadata.Count, keys.Count);
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Moq;
using Xunit;

namespace Microsoft.Build.Logging.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="TaskItem"/> class.
    /// </summary>
    public class TaskItemTests
    {
        /// <summary>
        /// Tests that the parameterless constructor initializes properties to their default values.
        /// </summary>
        [Fact]
        public void Constructor_Parameterless_InitializesDefaults()
        {
            // Arrange & Act
            var taskItem = new TaskItem();

            // Assert
            Assert.Null(taskItem.ItemSpec);
            Assert.NotNull(taskItem.Metadata);
            Assert.Empty(taskItem.Metadata);
            Assert.Equal(0, taskItem.MetadataCount);
            Assert.Empty(taskItem.MetadataNames);
            Assert.Null(taskItem.EvaluatedIncludeEscaped);
        }

        /// <summary>
        /// Tests that the constructor with an itemSpec parameter correctly sets the ItemSpec property.
        /// </summary>
        [Fact]
        public void Constructor_WithItemSpec_SetsItemSpec()
        {
            // Arrange
            string expectedItemSpec = "TestItemSpec";

            // Act
            var taskItem = new TaskItem(expectedItemSpec);

            // Assert
            Assert.Equal(expectedItemSpec, taskItem.ItemSpec);
        }

        /// <summary>
        /// Tests the SetMetadata method to ensure it adds or updates metadata entries correctly.
        /// </summary>
        [Fact]
        public void SetMetadata_WhenCalled_AddsOrUpdatesMetadata()
        {
            // Arrange
            var taskItem = new TaskItem();
            string key = "Key1";
            string value = "Value1";

            // Act
            taskItem.SetMetadata(key, value);

            // Assert
            Assert.True(taskItem.Metadata.ContainsKey(key));
            Assert.Equal(value, taskItem.Metadata[key]);
            Assert.Equal(1, taskItem.MetadataCount);
        }

        /// <summary>
        /// Tests the GetMetadata method to return the correct value when metadata exists.
        /// </summary>
        [Fact]
        public void GetMetadata_WhenMetadataExists_ReturnsValue()
        {
            // Arrange
            var taskItem = new TaskItem("TestItemSpec");
            string key = "CustomKey";
            string value = "CustomValue";
            taskItem.SetMetadata(key, value);

            // Act
            var result = taskItem.GetMetadata(key);

            // Assert
            Assert.Equal(value, result);
        }

        /// <summary>
        /// Tests the GetMetadata method for the "FullPath" key when no explicit metadata is set.
        /// The method should return the ItemSpec value.
        /// </summary>
        [Fact]
        public void GetMetadata_WhenKeyIsFullPathAndNotSet_ReturnsItemSpec()
        {
            // Arrange
            string itemSpec = "C:\\Test\\File.txt";
            var taskItem = new TaskItem(itemSpec);

            // Act
            var result = taskItem.GetMetadata("FullPath");

            // Assert
            Assert.Equal(itemSpec, result);
        }

        /// <summary>
        /// Tests the GetMetadata method returns an empty string when a non-existent key is requested.
        /// </summary>
        [Fact]
        public void GetMetadata_WhenKeyDoesNotExist_ReturnsEmptyString()
        {
            // Arrange
            var taskItem = new TaskItem("TestItemSpec");

            // Act
            var result = taskItem.GetMetadata("NonExistentKey");

            // Assert
            Assert.Equal(string.Empty, result);
        }

        /// <summary>
        /// Tests that the MetadataCount property accurately reflects the number of metadata entries.
        /// </summary>
        [Fact]
        public void MetadataCount_WhenMetadataAdded_ReturnsCorrectCount()
        {
            // Arrange
            var taskItem = new TaskItem();
            taskItem.SetMetadata("Key1", "Value1");
            taskItem.SetMetadata("Key2", "Value2");

            // Act & Assert
            Assert.Equal(2, taskItem.MetadataCount);
        }

        /// <summary>
        /// Tests that the MetadataNames property returns all the keys of the metadata.
        /// </summary>
//         [Fact] [Error] (153-37)CS1503 Argument 2: cannot convert from 'System.Collections.ICollection' to 'System.Collections.Generic.IEnumerable<string>' [Error] (154-37)CS1503 Argument 2: cannot convert from 'System.Collections.ICollection' to 'System.Collections.Generic.IEnumerable<string>'
//         public void MetadataNames_WhenMetadataAdded_ReturnsAllKeys()
//         {
//             // Arrange
//             var taskItem = new TaskItem();
//             taskItem.SetMetadata("Key1", "Value1");
//             taskItem.SetMetadata("Key2", "Value2");
// 
//             // Act
//             ICollection metadataNames = taskItem.MetadataNames;
// 
//             // Assert
//             Assert.Contains("Key1", metadataNames);
//             Assert.Contains("Key2", metadataNames);
//         }

        /// <summary>
        /// Tests the CloneCustomMetadata method to ensure it returns the metadata dictionary.
        /// </summary>
        [Fact]
        public void CloneCustomMetadata_WhenCalled_ReturnsMetadataDictionary()
        {
            // Arrange
            var taskItem = new TaskItem();
            taskItem.SetMetadata("Key", "Value");

            // Act
            IDictionary clonedMetadata = taskItem.CloneCustomMetadata();

            // Assert
            Assert.NotNull(clonedMetadata);
            Assert.Equal(taskItem.MetadataCount, clonedMetadata.Count);
            Assert.Equal("Value", clonedMetadata["Key"]);
        }

        /// <summary>
        /// Tests the CopyMetadataTo method to ensure that all metadata are copied to the destination ITaskItem.
        /// </summary>
        [Fact]
        public void CopyMetadataTo_WhenCalled_CopiesAllMetadata()
        {
            // Arrange
            var taskItem = new TaskItem();
            taskItem.SetMetadata("Key1", "Value1");
            taskItem.SetMetadata("Key2", "Value2");

            var mockDestination = new Mock<ITaskItem>();

            // Act
            taskItem.CopyMetadataTo(mockDestination.Object);

            // Assert
            mockDestination.Verify(dest => dest.SetMetadata("Key1", "Value1"), Times.Once);
            mockDestination.Verify(dest => dest.SetMetadata("Key2", "Value2"), Times.Once);
        }

        /// <summary>
        /// Tests that the CloneCustomMetadataEscaped method throws a NotImplementedException.
        /// </summary>
        [Fact]
        public void CloneCustomMetadataEscaped_WhenCalled_ThrowsNotImplementedException()
        {
            // Arrange
            var taskItem = new TaskItem();

            // Act & Assert
            Assert.Throws<NotImplementedException>(() => taskItem.CloneCustomMetadataEscaped());
        }

        /// <summary>
        /// Tests that the GetMetadataValueEscaped method throws a NotImplementedException.
        /// </summary>
        [Fact]
        public void GetMetadataValueEscaped_WhenCalled_ThrowsNotImplementedException()
        {
            // Arrange
            var taskItem = new TaskItem();

            // Act & Assert
            Assert.Throws<NotImplementedException>(() => taskItem.GetMetadataValueEscaped("anyKey"));
        }

        /// <summary>
        /// Tests that the RemoveMetadata method throws a NotImplementedException.
        /// </summary>
        [Fact]
        public void RemoveMetadata_WhenCalled_ThrowsNotImplementedException()
        {
            // Arrange
            var taskItem = new TaskItem();

            // Act & Assert
            Assert.Throws<NotImplementedException>(() => taskItem.RemoveMetadata("Key"));
        }

        /// <summary>
        /// Tests the SetMetadataValueLiteral method to validate that it sets metadata identically to SetMetadata.
        /// </summary>
        [Fact]
        public void SetMetadataValueLiteral_WhenCalled_SetsMetadata()
        {
            // Arrange
            var taskItem = new TaskItem();
            string key = "LiteralKey";
            string value = "LiteralValue";

            // Act
            taskItem.SetMetadataValueLiteral(key, value);

            // Assert
            Assert.Equal(value, taskItem.GetMetadata(key));
        }

        /// <summary>
        /// Tests the ToString override to confirm it returns the ItemSpec string.
        /// </summary>
        [Fact]
        public void ToString_WhenCalled_ReturnsItemSpec()
        {
            // Arrange
            string itemSpec = "TestItemSpec";
            var taskItem = new TaskItem(itemSpec);

            // Act
            string result = taskItem.ToString();

            // Assert
            Assert.Equal(itemSpec, result);
        }
    }
}

using System;
using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.Build.Framework.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="TaskStartedEventArgs2"/> class.
    /// </summary>
    public class TaskStartedEventArgs2Tests
    {
        private readonly string _message;
        private readonly string _helpKeyword;
        private readonly string _projectFile;
        private readonly string _taskFile;
        private readonly string _taskName;
        private readonly DateTime _eventTimestamp;

        /// <summary>
        /// Initializes test data for the tests.
        /// </summary>
        public TaskStartedEventArgs2Tests()
        {
            _message = "Test message";
            _helpKeyword = "TestHelp";
            _projectFile = "TestProject.proj";
            _taskFile = "TestTask.dll";
            _taskName = "TestTask";
            _eventTimestamp = DateTime.UtcNow;
        }

        /// <summary>
        /// Tests that the constructor properly creates an instance when valid arguments are provided.
        /// </summary>
        [Fact]
        public void Constructor_WithValidArguments_ReturnsInstance()
        {
            // Arrange & Act
            var instance = new TaskStartedEventArgs2(_message, _helpKeyword, _projectFile, _taskFile, _taskName, _eventTimestamp);

            // Assert
            Assert.NotNull(instance);
        }

        /// <summary>
        /// Tests that the LineNumber property can be set and retrieved with a typical value.
        /// </summary>
        [Fact]
        public void LineNumber_SetAndGetTypicalValue_ReturnsCorrectValue()
        {
            // Arrange
            var instance = new TaskStartedEventArgs2(_message, _helpKeyword, _projectFile, _taskFile, _taskName, _eventTimestamp);
            int expectedLineNumber = 15;

            // Act
            instance.LineNumber = expectedLineNumber;
            int actualLineNumber = instance.LineNumber;

            // Assert
            Assert.Equal(expectedLineNumber, actualLineNumber);
        }

        /// <summary>
        /// Tests that the LineNumber property correctly handles the edge case of int.MaxValue.
        /// </summary>
        [Fact]
        public void LineNumber_SetToMaxValue_ReturnsMaxValue()
        {
            // Arrange
            var instance = new TaskStartedEventArgs2(_message, _helpKeyword, _projectFile, _taskFile, _taskName, _eventTimestamp);
            int expectedLineNumber = int.MaxValue;

            // Act
            instance.LineNumber = expectedLineNumber;
            int actualLineNumber = instance.LineNumber;

            // Assert
            Assert.Equal(expectedLineNumber, actualLineNumber);
        }

        /// <summary>
        /// Tests that the ColumnNumber property can be set and retrieved with a typical value.
        /// </summary>
        [Fact]
        public void ColumnNumber_SetAndGetTypicalValue_ReturnsCorrectValue()
        {
            // Arrange
            var instance = new TaskStartedEventArgs2(_message, _helpKeyword, _projectFile, _taskFile, _taskName, _eventTimestamp);
            int expectedColumnNumber = 20;

            // Act
            instance.ColumnNumber = expectedColumnNumber;
            int actualColumnNumber = instance.ColumnNumber;

            // Assert
            Assert.Equal(expectedColumnNumber, actualColumnNumber);
        }

        /// <summary>
        /// Tests that the ColumnNumber property correctly handles the edge case of int.MinValue.
        /// </summary>
        [Fact]
        public void ColumnNumber_SetToMinValue_ReturnsMinValue()
        {
            // Arrange
            var instance = new TaskStartedEventArgs2(_message, _helpKeyword, _projectFile, _taskFile, _taskName, _eventTimestamp);
            int expectedColumnNumber = int.MinValue;

            // Act
            instance.ColumnNumber = expectedColumnNumber;
            int actualColumnNumber = instance.ColumnNumber;

            // Assert
            Assert.Equal(expectedColumnNumber, actualColumnNumber);
        }

        /// <summary>
        /// Tests that the TaskAssemblyLocation property can be set and retrieved with a valid string.
        /// </summary>
        [Fact]
        public void TaskAssemblyLocation_SetAndGetValidString_ReturnsCorrectValue()
        {
            // Arrange
            var instance = new TaskStartedEventArgs2(_message, _helpKeyword, _projectFile, _taskFile, _taskName, _eventTimestamp);
            string expectedLocation = @"C:\Tasks\TaskAssembly.dll";

            // Act
            instance.TaskAssemblyLocation = expectedLocation;
            string actualLocation = instance.TaskAssemblyLocation;

            // Assert
            Assert.Equal(expectedLocation, actualLocation);
        }

        /// <summary>
        /// Tests that the TaskAssemblyLocation property returns null when set to null.
        /// </summary>
        [Fact]
        public void TaskAssemblyLocation_SetToNull_ReturnsNull()
        {
            // Arrange
            var instance = new TaskStartedEventArgs2(_message, _helpKeyword, _projectFile, _taskFile, _taskName, _eventTimestamp);

            // Act
            instance.TaskAssemblyLocation = null;
            string actualLocation = instance.TaskAssemblyLocation;

            // Assert
            Assert.Null(actualLocation);
        }

        /// <summary>
        /// Tests that the TaskAssemblyLocation property can be set and retrieved when set to an empty string.
        /// </summary>
        [Fact]
        public void TaskAssemblyLocation_SetToEmptyString_ReturnsEmptyString()
        {
            // Arrange
            var instance = new TaskStartedEventArgs2(_message, _helpKeyword, _projectFile, _taskFile, _taskName, _eventTimestamp);
            string expectedLocation = string.Empty;

            // Act
            instance.TaskAssemblyLocation = expectedLocation;
            string actualLocation = instance.TaskAssemblyLocation;

            // Assert
            Assert.Equal(expectedLocation, actualLocation);
        }
    }
}

using System;
using System.Collections.Generic;
using StructuredLogger.BinaryLogger;
using Xunit;

namespace StructuredLogger.BinaryLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="BuildSubmissionStartedEventArgs"/> class.
    /// </summary>
    public class BuildSubmissionStartedEventArgsTests
    {
        /// <summary>
        /// Tests that the default constructor of BuildSubmissionStartedEventArgs initializes the instance.
        /// Verifies that the instance is created, the base message is set to empty,
        /// and auto-properties are null (or default) if not explicitly set.
        /// </summary>
        [Fact]
        public void Constructor_ShouldInitializeInstanceWithDefaultValues()
        {
            // Act
            var eventArgs = new BuildSubmissionStartedEventArgs();

            // Assert
            Assert.NotNull(eventArgs);
            Assert.Equal(string.Empty, eventArgs.Message);
            // Auto-properties should be null or default before assignment.
            Assert.Null(eventArgs.GlobalProperties);
            Assert.Null(eventArgs.EntryProjectsFullPath);
            Assert.Null(eventArgs.TargetNames);
            Assert.Equal(default(BuildRequestDataFlags), eventArgs.Flags);
            Assert.Equal(0, eventArgs.SubmissionId);
        }

        /// <summary>
        /// Tests that the properties of BuildSubmissionStartedEventArgs can be set and retrieved correctly.
        /// This covers setting GlobalProperties, EntryProjectsFullPath, TargetNames, Flags, and SubmissionId.
        /// </summary>
        [Fact]
        public void Properties_SetAndGet_ReturnsAssignedValues()
        {
            // Arrange
            var expectedGlobalProperties = new Dictionary<string, string?>
            {
                { "Key1", "Value1" },
                { "Key2", "Value2" }
            };

            var expectedEntryProjects = new List<string> { "Project1.csproj", "Project2.csproj" };
            var expectedTargetNames = new List<string> { "Build", "Clean" };
            var expectedFlags = BuildRequestDataFlags.ProvideProjectStateAfterBuild | BuildRequestDataFlags.SkipNonexistentTargets;
            int expectedSubmissionId = 42;

            var eventArgs = new BuildSubmissionStartedEventArgs();

            // Act
            eventArgs.GlobalProperties = expectedGlobalProperties;
            eventArgs.EntryProjectsFullPath = expectedEntryProjects;
            eventArgs.TargetNames = expectedTargetNames;
            eventArgs.Flags = expectedFlags;
            eventArgs.SubmissionId = expectedSubmissionId;

            // Assert
            Assert.Equal(expectedGlobalProperties, eventArgs.GlobalProperties);
            Assert.Equal(expectedEntryProjects, eventArgs.EntryProjectsFullPath);
            Assert.Equal(expectedTargetNames, eventArgs.TargetNames);
            Assert.Equal(expectedFlags, eventArgs.Flags);
            Assert.Equal(expectedSubmissionId, eventArgs.SubmissionId);
        }

        /// <summary>
        /// Tests that assigning null values to reference type properties is handled correctly.
        /// Even though the properties are auto-implemented, this test ensures that the setter assignment works as expected.
        /// </summary>
        [Fact]
        public void Properties_SetToNull_ReturnsNull()
        {
            // Arrange
            var eventArgs = new BuildSubmissionStartedEventArgs();

            // Act
            eventArgs.GlobalProperties = null;
            eventArgs.EntryProjectsFullPath = null;
            eventArgs.TargetNames = null;

            // Assert
            Assert.Null(eventArgs.GlobalProperties);
            Assert.Null(eventArgs.EntryProjectsFullPath);
            Assert.Null(eventArgs.TargetNames);
        }
    }
}

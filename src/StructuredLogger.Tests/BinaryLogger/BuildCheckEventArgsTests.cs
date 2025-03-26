using System;
using System.Collections.Generic;
using StructuredLogger.BinaryLogger;
using Xunit;

namespace StructuredLogger.BinaryLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="BuildCheckTracingEventArgs"/> class.
    /// </summary>
    public class BuildCheckTracingEventArgsTests
    {
        private readonly Dictionary<string, TimeSpan> _sampleTracingData;

        /// <summary>
        /// Initializes a new instance of the <see cref="BuildCheckTracingEventArgsTests"/> class.
        /// </summary>
        public BuildCheckTracingEventArgsTests()
        {
            _sampleTracingData = new Dictionary<string, TimeSpan>
            {
                { "Step1", TimeSpan.FromSeconds(1) },
                { "Step2", TimeSpan.FromSeconds(2) }
            };
        }

        /// <summary>
        /// Tests that the constructor sets the TracingData property with the provided non-null dictionary.
        /// Expected outcome: The TracingData property mirrors the input dictionary.
        /// </summary>
        [Fact]
        public void Constructor_WithValidTracingData_SetsTracingDataProperty()
        {
            // Arrange
            var expectedData = new Dictionary<string, TimeSpan>(_sampleTracingData);

            // Act
            var eventArgs = new BuildCheckTracingEventArgs(expectedData);

            // Assert
            Assert.Equal(expectedData, eventArgs.TracingData);
        }

        /// <summary>
        /// Tests that the constructor sets the TracingData property to null when provided a null argument.
        /// Expected outcome: The TracingData property is null.
        /// </summary>
        [Fact]
        public void Constructor_WithNullTracingData_SetsTracingDataPropertyToNull()
        {
            // Act
            var eventArgs = new BuildCheckTracingEventArgs(null);

            // Assert
            Assert.Null(eventArgs.TracingData);
        }
    }

    /// <summary>
    /// Unit tests for the <see cref="BuildCheckAcquisitionEventArgs"/> class.
    /// </summary>
    public class BuildCheckAcquisitionEventArgsTests
    {
        private readonly string _sampleAcquisitionPath;
        private readonly string _sampleProjectPath;

        /// <summary>
        /// Initializes a new instance of the <see cref="BuildCheckAcquisitionEventArgsTests"/> class.
        /// </summary>
        public BuildCheckAcquisitionEventArgsTests()
        {
            _sampleAcquisitionPath = @"C:\temp\acquisition.log";
            _sampleProjectPath = @"C:\temp\project.csproj";
        }

        /// <summary>
        /// Tests that the constructor correctly assigns the AcquisitionPath and ProjectPath properties when provided valid strings.
        /// Expected outcome: The properties match the input values.
        /// </summary>
        [Fact]
        public void Constructor_WithValidPaths_SetsProperties()
        {
            // Act
            var eventArgs = new BuildCheckAcquisitionEventArgs(_sampleAcquisitionPath, _sampleProjectPath);

            // Assert
            Assert.Equal(_sampleAcquisitionPath, eventArgs.AcquisitionPath);
            Assert.Equal(_sampleProjectPath, eventArgs.ProjectPath);
        }

        /// <summary>
        /// Tests that the constructor assigns null to the AcquisitionPath and ProjectPath properties when provided null arguments.
        /// Expected outcome: Both properties are null.
        /// </summary>
        [Fact]
        public void Constructor_WithNullPaths_SetsPropertiesToNull()
        {
            // Act
            var eventArgs = new BuildCheckAcquisitionEventArgs(null, null);

            // Assert
            Assert.Null(eventArgs.AcquisitionPath);
            Assert.Null(eventArgs.ProjectPath);
        }
    }

    /// <summary>
    /// Unit tests for the <see cref="BuildCheckResultMessage"/> class.
    /// </summary>
    public class BuildCheckResultMessageTests
    {
        private readonly string _sampleMessage;

        /// <summary>
        /// Initializes a new instance of the <see cref="BuildCheckResultMessageTests"/> class.
        /// </summary>
        public BuildCheckResultMessageTests()
        {
            _sampleMessage = "This is a sample build result message.";
        }

        /// <summary>
        /// Tests that the constructor assigns the provided message to the RawMessage property.
        /// Expected outcome: The RawMessage property equals the input message.
        /// </summary>
//         [Fact] [Error] (133-56)CS0122 'BuildEventArgs.RawMessage' is inaccessible due to its protection level
//         public void Constructor_WithValidMessage_SetsRawMessageProperty()
//         {
//             // Act
//             var resultMessage = new BuildCheckResultMessage(_sampleMessage);
// 
//             // Assert
//             Assert.Equal(_sampleMessage, resultMessage.RawMessage);
//         }

        /// <summary>
        /// Tests that the constructor assigns an empty string to the RawMessage property when provided an empty message.
        /// Expected outcome: The RawMessage property is an empty string.
        /// </summary>
//         [Fact] [Error] (147-54)CS0122 'BuildEventArgs.RawMessage' is inaccessible due to its protection level
//         public void Constructor_WithEmptyMessage_SetsRawMessagePropertyToEmpty()
//         {
//             // Act
//             var resultMessage = new BuildCheckResultMessage(string.Empty);
// 
//             // Assert
//             Assert.Equal(string.Empty, resultMessage.RawMessage);
//         }

        /// <summary>
        /// Tests that the constructor assigns null to the RawMessage property when provided a null message.
        /// Expected outcome: The RawMessage property is null.
        /// </summary>
//         [Fact] [Error] (161-39)CS0122 'BuildEventArgs.RawMessage' is inaccessible due to its protection level
//         public void Constructor_WithNullMessage_SetsRawMessagePropertyToNull()
//         {
//             // Act
//             var resultMessage = new BuildCheckResultMessage(null);
// 
//             // Assert
//             Assert.Null(resultMessage.RawMessage);
//         }
    }
}

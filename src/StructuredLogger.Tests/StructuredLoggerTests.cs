using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Build.Logging.StructuredLogger;
using Moq;
using Xunit;

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="StructuredLogger"/> class.
    /// </summary>
    public class StructuredLoggerTests
    {
        /// <summary>
        /// Tests that calling Initialize with null Parameters throws a LoggerException.
        /// </summary>
        [Fact]
        public void Initialize_NullParameters_ThrowsLoggerException()
        {
            // Arrange
            var logger = new StructuredLogger();
            logger.Parameters = null;
            var mockEventSource = new Mock<IEventSource>();

            // Act & Assert
            var exception = Assert.Throws<LoggerException>(() => logger.Initialize(mockEventSource.Object));
            Assert.Contains("Need to specify a log file", exception.Message);
        }

        /// <summary>
        /// Tests that calling Initialize with multiple parameters throws a LoggerException.
        /// </summary>
        [Fact]
        public void Initialize_MultipleParameters_ThrowsLoggerException()
        {
            // Arrange
            var logger = new StructuredLogger();
            logger.Parameters = "\"log1.log\";\"log2.log\"";
            var mockEventSource = new Mock<IEventSource>();

            // Act & Assert
            var exception = Assert.Throws<LoggerException>(() => logger.Initialize(mockEventSource.Object));
            Assert.Contains("Need to specify a log file", exception.Message);
        }

        /// <summary>
        /// Tests that Initialize sets the expected environment variables and initializes the Construction property.
        /// </summary>
        [Fact]
        public void Initialize_ValidParameters_EnvironmentVariablesSetAndConstructionInitialized()
        {
            // Arrange
            var logger = new StructuredLogger();
            // Using a valid parameter with a single file path wrapped in quotes.
            logger.Parameters = "\"test.log\"";
            // Set SaveLogToDisk to false to bypass file writing in Shutdown.
            StructuredLogger.SaveLogToDisk = false;

            var mockEventSource = new Mock<IEventSource>();

            // Setup dummy events to allow subscription.
            // These setups are not strictly necessary since we are not raising events here,
            // but they ensure that event subscription does not throw.
            mockEventSource.SetupAdd(m => m.BuildStarted += It.IsAny<BuildStartedEventHandler>());
            mockEventSource.SetupAdd(m => m.BuildFinished += It.IsAny<BuildFinishedEventHandler>());
            mockEventSource.SetupAdd(m => m.ProjectStarted += It.IsAny<ProjectStartedEventHandler>());
            mockEventSource.SetupAdd(m => m.ProjectFinished += It.IsAny<ProjectFinishedEventHandler>());
            mockEventSource.SetupAdd(m => m.TargetStarted += It.IsAny<TargetStartedEventHandler>());
            mockEventSource.SetupAdd(m => m.TargetFinished += It.IsAny<TargetFinishedEventHandler>());
            mockEventSource.SetupAdd(m => m.TaskStarted += It.IsAny<TaskStartedEventHandler>());
            mockEventSource.SetupAdd(m => m.TaskFinished += It.IsAny<TaskFinishedEventHandler>());
            mockEventSource.SetupAdd(m => m.MessageRaised += It.IsAny<BuildMessageEventHandler>());
            mockEventSource.SetupAdd(m => m.WarningRaised += It.IsAny<BuildWarningEventHandler>());
            mockEventSource.SetupAdd(m => m.ErrorRaised += It.IsAny<BuildErrorEventHandler>());
            mockEventSource.SetupAdd(m => m.CustomEventRaised += It.IsAny<CustomBuildEventHandler>());
            mockEventSource.SetupAdd(m => m.StatusEventRaised += It.IsAny<BuildStatusEventHandler>());
            mockEventSource.SetupAdd(m => m.AnyEventRaised += It.IsAny<AnyEventHandler>());

            // Act
            logger.Initialize(mockEventSource.Object);

            // Assert
            Assert.Equal("true", Environment.GetEnvironmentVariable("MSBUILDTARGETOUTPUTLOGGING"));
            Assert.Equal("1", Environment.GetEnvironmentVariable("MSBUILDLOGIMPORTS"));
            Assert.NotNull(logger.Construction);
        }

        /// <summary>
        /// Tests that Shutdown sets the static CurrentBuild property when SaveLogToDisk is false.
        /// </summary>
        [Fact]
        public void Shutdown_SaveLogToDiskFalse_SetsCurrentBuild()
        {
            // Arrange
            var logger = new StructuredLogger();
            logger.Parameters = "\"test.log\"";
            StructuredLogger.SaveLogToDisk = false;
            var mockEventSource = new Mock<IEventSource>();

            // Setup required event subscriptions
            mockEventSource.SetupAdd(m => m.BuildStarted += It.IsAny<BuildStartedEventHandler>());
            mockEventSource.SetupAdd(m => m.BuildFinished += It.IsAny<BuildFinishedEventHandler>());
            mockEventSource.SetupAdd(m => m.ProjectStarted += It.IsAny<ProjectStartedEventHandler>());
            mockEventSource.SetupAdd(m => m.ProjectFinished += It.IsAny<ProjectFinishedEventHandler>());
            mockEventSource.SetupAdd(m => m.TargetStarted += It.IsAny<TargetStartedEventHandler>());
            mockEventSource.SetupAdd(m => m.TargetFinished += It.IsAny<TargetFinishedEventHandler>());
            mockEventSource.SetupAdd(m => m.TaskStarted += It.IsAny<TaskStartedEventHandler>());
            mockEventSource.SetupAdd(m => m.TaskFinished += It.IsAny<TaskFinishedEventHandler>());
            mockEventSource.SetupAdd(m => m.MessageRaised += It.IsAny<BuildMessageEventHandler>());
            mockEventSource.SetupAdd(m => m.WarningRaised += It.IsAny<BuildWarningEventHandler>());
            mockEventSource.SetupAdd(m => m.ErrorRaised += It.IsAny<BuildErrorEventHandler>());
            mockEventSource.SetupAdd(m => m.CustomEventRaised += It.IsAny<CustomBuildEventHandler>());
            mockEventSource.SetupAdd(m => m.StatusEventRaised += It.IsAny<BuildStatusEventHandler>());
            mockEventSource.SetupAdd(m => m.AnyEventRaised += It.IsAny<AnyEventHandler>());

            logger.Initialize(mockEventSource.Object);

            // Act
            logger.Shutdown();

            // Assert
            Assert.NotNull(StructuredLogger.CurrentBuild);
            Assert.Equal(logger.Construction.Build, StructuredLogger.CurrentBuild);
        }
    }

    ///// <summary>
    ///// A testable subclass of StructuredLogger to expose the Parameters property.
    ///// Assumes that the base Logger class has a public or protected property "Parameters".
    ///// </summary>
    //public class TestableStructuredLogger : StructuredLogger
    //{
    //    /// <summary>
    //    /// Exposes the Parameters property for testing purposes.
    //    /// </summary>
    //    public new string Parameters { get; set; }

    //    /// <summary>
    //    /// Overrides Initialize to set the Parameters property in base Logger.
    //    /// </summary>
    //    /// <param name="eventSource">The event source to initialize with.</param>
    //    public override void Initialize(IEventSource eventSource)
    //    {
    //        // Manually assign the Parameters property in the base Logger.
    //        // Depending on the actual implementation of Logger, this might need to be set differently.
    //        base.Parameters = this.Parameters;
    //        base.Initialize(eventSource);
    //    }
    //}
}

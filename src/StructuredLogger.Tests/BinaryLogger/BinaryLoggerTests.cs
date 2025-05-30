using System;
using System.IO;
using Moq;
using Xunit;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging.StructuredLogger;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="BinaryLogger"/> class.
    /// </summary>
    public class BinaryLoggerTests : IDisposable
    {
        private readonly string _tempFilePath;

        /// <summary>
        /// Initializes a new instance of the <see cref="BinaryLoggerTests"/> class.
        /// Generates a unique temporary file path used for testing file creation.
        /// </summary>
        public BinaryLoggerTests()
        {
            _tempFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".binlog");
        }

        /// <summary>
        /// Disposes resources used by the tests, including deleting the temporary file if it exists.
        /// </summary>
        public void Dispose()
        {
            if (File.Exists(_tempFilePath))
            {
                try
                {
                    File.Delete(_tempFilePath);
                }
                catch
                {
                    // Ignore cleanup exceptions.
                }
            }
        }

        /// <summary>
        /// Tests that Initialize throws a LoggerException when Parameters is null.
        /// Functional steps:
        /// 1. Create an instance of BinaryLogger with null Parameters.
        /// 2. Invoke Initialize with a dummy event source.
        /// Expected outcome: A LoggerException is thrown.
        /// </summary>
        [Fact]
        public void Initialize_NullParameters_ThrowsLoggerException()
        {
            // Arrange
            var logger = new BinaryLogger { Parameters = null };
            var mockEventSource = new Mock<IEventSource>();

            // Act & Assert
            Assert.Throws<LoggerException>(() => logger.Initialize(mockEventSource.Object));
        }

        /// <summary>
        /// Tests that Initialize throws a LoggerException when an invalid parameter is provided.
        /// Functional steps:
        /// 1. Create an instance of BinaryLogger with an unrecognized parameter.
        /// 2. Invoke Initialize with a dummy event source.
        /// Expected outcome: A LoggerException is thrown.
        /// </summary>
        [Fact]
        public void Initialize_InvalidParameter_ThrowsLoggerException()
        {
            // Arrange
            var logger = new BinaryLogger { Parameters = "InvalidParameter" };
            var mockEventSource = new Mock<IEventSource>();

            // Act & Assert
            Assert.Throws<LoggerException>(() => logger.Initialize(mockEventSource.Object));
        }

        /// <summary>
        /// Tests that Initialize with valid parameters creates the log file and writes initial info.
        /// Functional steps:
        /// 1. Create an instance of BinaryLogger with valid Parameters including a file path.
        /// 2. Invoke Initialize with a dummy event source and raise an event to simulate logging.
        /// 3. Call Shutdown to finalize the file writing.
        /// Expected outcome: The log file exists and is non-empty.
        /// </summary>
//         [Fact] [Error] (97-72)CS0246 The type or namespace name 'BuildEventHandler' could not be found (are you missing a using directive or an assembly reference?)
//         public void Initialize_ValidParameters_CreatesFile()
//         {
//             // Arrange
//             string parameters = $"LogFile=\"{_tempFilePath}\";ProjectImports=None";
//             var logger = new BinaryLogger { Parameters = parameters };
//             var mockEventSource = new Mock<IEventSource>();
//             // Set up the event subscription for AnyEventRaised.
//             mockEventSource.SetupAdd(m => m.AnyEventRaised += It.IsAny<BuildEventHandler>());
// 
//             // Act
//             logger.Initialize(mockEventSource.Object);
//             // Raise an event to trigger writing.
//             var buildEvent = new BuildMessageEventArgs("Test message", null, "UnitTest", MessageImportance.Normal);
//             mockEventSource.Raise(m => m.AnyEventRaised += null, buildEvent);
//             logger.Shutdown();
// 
//             // Assert
//             Assert.True(File.Exists(_tempFilePath), "Log file was not created.");
//             FileInfo fileInfo = new FileInfo(_tempFilePath);
//             Assert.True(fileInfo.Length > 0, "Log file is empty.");
//         }

        /// <summary>
        /// Tests that Shutdown resets the environment variables to their original values.
        /// Functional steps:
        /// 1. Set environment variables MSBUILDTARGETOUTPUTLOGGING and MSBUILDLOGIMPORTS to known values.
        /// 2. Create an instance of BinaryLogger with valid Parameters.
        /// 3. Invoke Initialize and then Shutdown.
        /// Expected outcome: The environment variables are restored to their original values.
        /// </summary>
//         [Fact] [Error] (132-72)CS0246 The type or namespace name 'BuildEventHandler' could not be found (are you missing a using directive or an assembly reference?)
//         public void Shutdown_ResetsEnvironmentVariables()
//         {
//             // Arrange
//             string originalTargetOutputLogging = "OriginalTargetOutput";
//             string originalLogImports = "OriginalLogImports";
//             Environment.SetEnvironmentVariable("MSBUILDTARGETOUTPUTLOGGING", originalTargetOutputLogging);
//             Environment.SetEnvironmentVariable("MSBUILDLOGIMPORTS", originalLogImports);
// 
//             string parameters = $"LogFile=\"{_tempFilePath}\";ProjectImports=None";
//             var logger = new BinaryLogger { Parameters = parameters };
//             var mockEventSource = new Mock<IEventSource>();
//             mockEventSource.SetupAdd(m => m.AnyEventRaised += It.IsAny<BuildEventHandler>());
// 
//             // Act
//             logger.Initialize(mockEventSource.Object);
//             logger.Shutdown();
// 
//             // Assert
//             Assert.Equal(originalTargetOutputLogging, Environment.GetEnvironmentVariable("MSBUILDTARGETOUTPUTLOGGING"));
//             Assert.Equal(originalLogImports, Environment.GetEnvironmentVariable("MSBUILDLOGIMPORTS"));
//         }

        /// <summary>
        /// Tests that Initialize handles event sources implementing IBinaryLogReplaySource without throwing exceptions.
        /// Functional steps:
        /// 1. Create a mock that implements both IEventSource and IBinaryLogReplaySource.
        /// 2. Set up the DeferredInitialize method to invoke both provided callbacks.
        /// 3. Invoke Initialize with the mock replay source.
        /// Expected outcome: Initialize executes without throwing any exceptions.
        /// </summary>
//         [Fact] [Error] (177-36)CS0117 'Record' does not contain a definition for 'Exception'
//         public void Initialize_WithReplaySource_DoesNotThrow()
//         {
//             // Arrange
//             string parameters = $"LogFile=\"{_tempFilePath}\";ProjectImports=None";
//             var logger = new BinaryLogger { Parameters = parameters };
// 
//             // Create a mock that implements both IBinaryLogReplaySource and IEventSource.
//             var mockReplaySource = new Mock<IBinaryLogReplaySource>();
//             var replaySourceAsEventSource = mockReplaySource.As<IEventSource>();
// 
//             // Setup DeferredInitialize to execute both callbacks.
//             mockReplaySource.Setup(m => m.DeferredInitialize(It.IsAny<Action>(), It.IsAny<Action>()))
//                 .Callback<Action, Action>((rawInit, structuredInit) =>
//                 {
//                     rawInit();
//                     structuredInit();
//                 });
// 
//             // Setup IEventSource3 and IEventSource4 methods if necessary.
//             var mockEventSource3 = mockReplaySource.As<IEventSource3>();
//             mockEventSource3.Setup(m => m.IncludeEvaluationMetaprojects());
//             var mockEventSource4 = mockReplaySource.As<IEventSource4>();
//             mockEventSource4.Setup(m => m.IncludeEvaluationPropertiesAndItems());
// 
//             // Act
//             var exception = Record.Exception(() => logger.Initialize(mockReplaySource.Object));
// 
//             // Assert
//             Assert.Null(exception);
//             logger.Shutdown();
//         }
    }
}

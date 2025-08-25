using System;
using System.IO;
using System.Reflection;
using Xunit;
using Microsoft.Build.Logging.StructuredLogger;

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="ErrorReporting"/> class.
    /// </summary>
    public class ErrorReportingTests : IDisposable
    {
        private readonly string _tempDirectory;
        private readonly string _testLogFilePath;
        private const long ThresholdSize = 10000000; // 10 MB

        /// <summary>
        /// Initializes a new instance of the <see cref="ErrorReportingTests"/> class.
        /// Sets up a unique temporary directory and overrides the log file path for testing.
        /// </summary>
        public ErrorReportingTests()
        {
            // Create a unique temporary directory for testing
            _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDirectory);
            _testLogFilePath = Path.Combine(_tempDirectory, "LoggerExceptions.txt");

            // Override the private static readonly field logFilePath using reflection
            FieldInfo field = typeof(ErrorReporting).GetField("logFilePath", BindingFlags.Static | BindingFlags.NonPublic);
            field.SetValue(null, _testLogFilePath);
        }

        /// <summary>
        /// Cleans up the temporary directory after tests.
        /// </summary>
        public void Dispose()
        {
            // Clean up temporary directory after tests
            try
            {
                if (Directory.Exists(_tempDirectory))
                {
                    Directory.Delete(_tempDirectory, true);
                }
            }
            catch
            {
                // Ignore cleanup exceptions
            }
        }

        /// <summary>
        /// Tests that the LogFilePath property returns the overridden test log file path.
        /// </summary>
        [Fact]
        public void LogFilePath_WhenCalled_ReturnsTestFilePath()
        {
            // Act
            string logFilePath = ErrorReporting.LogFilePath;

            // Assert
            Assert.Equal(_testLogFilePath, logFilePath);
        }

        /// <summary>
        /// Tests that ReportException with a null exception does nothing.
        /// Expected outcome: No file is created.
        /// </summary>
        [Fact]
        public void ReportException_NullException_DoesNothing()
        {
            // Arrange
            if (File.Exists(_testLogFilePath))
            {
                File.Delete(_testLogFilePath);
            }

            // Act
            ErrorReporting.ReportException(null);

            // Assert: File should not be created when a null exception is passed.
            Assert.False(File.Exists(_testLogFilePath));
        }

        /// <summary>
        /// Tests that ReportException with a valid exception appends the exception details to the log file.
        /// Expected outcome: The log file is created with the exception message appended.
        /// </summary>
        [Fact]
        public void ReportException_ValidException_AppendsExceptionToLog()
        {
            // Arrange
            if (File.Exists(_testLogFilePath))
            {
                File.Delete(_testLogFilePath);
            }
            Exception testException = new Exception("Test exception");

            // Act
            ErrorReporting.ReportException(testException);

            // Assert: Log file exists and contains the exception message and a newline.
            Assert.True(File.Exists(_testLogFilePath));
            string content = File.ReadAllText(_testLogFilePath);
            Assert.Contains("Test exception", content);
            Assert.Contains(Environment.NewLine, content);
        }

        /// <summary>
        /// Tests that ReportException deletes an existing log file that is larger than the threshold before appending new content.
        /// Expected outcome: The oversized log file is deleted and replaced with a new log containing the new exception details.
        /// </summary>
        [Fact]
        public void ReportException_LogFileTooLarge_DeletesOldFileAndAppendsNewException()
        {
            // Arrange
            // Create an old log file with dummy content exceeding the threshold.
            Directory.CreateDirectory(Path.GetDirectoryName(_testLogFilePath));
            using (FileStream fs = File.Create(_testLogFilePath))
            {
                fs.SetLength(ThresholdSize + 1);
            }
            Exception testException = new Exception("New exception after deletion");

            // Act
            ErrorReporting.ReportException(testException);

            // Assert: Log file should exist, contain the new exception message, and be below the oversized threshold.
            Assert.True(File.Exists(_testLogFilePath));
            string content = File.ReadAllText(_testLogFilePath);
            Assert.Contains("New exception after deletion", content);
            Assert.True(new FileInfo(_testLogFilePath).Length < ThresholdSize);
        }

        /// <summary>
        /// Tests that ReportException swallows exceptions thrown during file operations.
        /// This is simulated by making the log file directory read-only.
        /// Expected outcome: No exception is thrown and the method fails silently.
        /// </summary>
        [Fact]
        public void ReportException_WhenFileOperationThrows_ExceptionIsSwallowed()
        {
            // Arrange
            Directory.CreateDirectory(Path.GetDirectoryName(_testLogFilePath));
            DirectoryInfo dirInfo = new DirectoryInfo(Path.GetDirectoryName(_testLogFilePath));
            FileAttributes originalAttributes = dirInfo.Attributes;
            dirInfo.Attributes |= FileAttributes.ReadOnly;
            Exception testException = new Exception("Simulated file operation exception");

            // Act & Assert: Ensure that ReportException does not propagate any exceptions.
            try
            {
                ErrorReporting.ReportException(testException);
            }
            catch
            {
                Assert.True(false, "ReportException should swallow exceptions thrown during file operations.");
            }
            finally
            {
                // Restore directory attributes so cleanup can succeed.
                dirInfo.Attributes = originalAttributes;
            }
        }
    }
}

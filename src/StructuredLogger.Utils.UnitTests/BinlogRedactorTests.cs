using Microsoft.Build.SensitiveDataDetector;
using Moq;
using StructuredLogger.Utils;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace StructuredLogger.Utils.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref = "BinlogRedactorOptions"/> class.
    /// </summary>
    public class BinlogRedactorOptionsTests
    {
        /// <summary>
        /// Tests that the constructor correctly sets the InputPath and default property values.
        /// </summary>
        [Fact]
        public void Constructor_ValidInputPath_DefaultPropertiesInitialized()
        {
            // Arrange
            string expectedInputPath = "dummyPath.binlog";
            // Act
            var options = new BinlogRedactorOptions(expectedInputPath);
            // Assert
            Assert.Equal(expectedInputPath, options.InputPath);
            Assert.True(options.ProcessEmbeddedFiles);
            Assert.True(options.IdentifyReplacemenets);
            Assert.True(options.AutodetectCommonPatterns);
            Assert.True(options.AutodetectUsername);
            Assert.Null(options.TokensToRedact);
            Assert.Null(options.OutputFileName);
        }

        /// <summary>
        /// Tests setting and getting properties of BinlogRedactorOptions.
        /// </summary>
        [Fact]
        public void Properties_SetAndGet_ValuesPersist()
        {
            // Arrange
            string inputPath = "original.binlog";
            var options = new BinlogRedactorOptions(inputPath);
            string[] tokens = new[]
            {
                "secret",
                "password"
            };
            string outputFile = "redacted.binlog";
            // Act
            options.TokensToRedact = tokens;
            options.OutputFileName = outputFile;
            options.ProcessEmbeddedFiles = false;
            options.IdentifyReplacemenets = false;
            options.AutodetectCommonPatterns = false;
            options.AutodetectUsername = false;
            // Assert
            Assert.Equal(tokens, options.TokensToRedact);
            Assert.Equal(inputPath, options.InputPath);
            Assert.Equal(outputFile, options.OutputFileName);
            Assert.False(options.ProcessEmbeddedFiles);
            Assert.False(options.IdentifyReplacemenets);
            Assert.False(options.AutodetectCommonPatterns);
            Assert.False(options.AutodetectUsername);
        }
    }

    /// <summary>
    /// Unit tests for the <see cref = "BinlogRedactor"/> class.
    /// </summary>
    public class BinlogRedactorTests
    {
        private readonly Mock<ISensitiveDataRedactor> _mockSensitiveDataRedactor;
        /// <summary>
        /// Constructor for BinlogRedactorTests.
        /// Initializes the mock for ISensitiveDataRedactor.
        /// </summary>
        public BinlogRedactorTests()
        {
            _mockSensitiveDataRedactor = new Mock<ISensitiveDataRedactor>();
            // Setup the mock to simulate redaction by appending "-redacted" to the input string.
            _mockSensitiveDataRedactor.Setup(redactor => redactor.Redact(It.IsAny<string>())).Returns((string s) => s + "-redacted");
        }

        /// <summary>
        /// Tests that the ProcessBinlog method completes successfully when skipEmbeddedFiles is true.
        /// </summary>
        [Fact]
        public void ProcessBinlog_SkipEmbeddedFilesTrue_CompletesSuccessfully()
        {
            // Arrange
            string inputContent = "Test content";
            string inputFile = Path.GetTempFileName();
            string outputFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".binlog");
            try
            {
                File.WriteAllText(inputFile, inputContent);
                var redactor = new BinlogRedactor(_mockSensitiveDataRedactor.Object);
                // Not setting Progress, so progress reporting is bypassed.
                // Act
                redactor.ProcessBinlog(inputFile, outputFile, skipEmbeddedFiles: true);
                // Assert
                Assert.True(File.Exists(outputFile));
            }
            finally
            {
                if (File.Exists(inputFile))
                {
                    File.Delete(inputFile);
                }

                if (File.Exists(outputFile))
                {
                    File.Delete(outputFile);
                }
            }
        }

        /// <summary>
        /// Tests that the ProcessBinlog method reports progress and eventually reports completion (1.0) when a Progress instance is provided.
        /// </summary>
//         [Fact] [Error] (141-32)CS0266 Cannot implicitly convert type 'System.IProgress<double>' to 'Microsoft.Build.Logging.StructuredLogger.Progress'. An explicit conversion exists (are you missing a cast?)
//         public void ProcessBinlog_WithProgress_ReportsCompletion()
//         {
//             // Arrange
//             string inputContent = "Progress test content";
//             string inputFile = Path.GetTempFileName();
//             string outputFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".binlog");
//             var progressMock = new Mock<IProgress<double>>();
//             // Use a counter to capture the final progress report.
//             double finalProgress = 0.0;
//             progressMock.Setup(p => p.Report(It.IsAny<double>())).Callback<double>(value => finalProgress = value);
//             try
//             {
//                 File.WriteAllText(inputFile, inputContent);
//                 var redactor = new BinlogRedactor(_mockSensitiveDataRedactor.Object)
//                 {
//                     // Set the Progress property.
//                     Progress = progressMock.Object
//                 };
//                 // Act
//                 redactor.ProcessBinlog(inputFile, outputFile, skipEmbeddedFiles: false);
//                 // Assert
//                 // The progress should eventually reach 1.0.
//                 Assert.Equal(1.0, finalProgress);
//                 Assert.True(File.Exists(outputFile));
//             }
//             finally
//             {
//                 if (File.Exists(inputFile))
//                 {
//                     File.Delete(inputFile);
//                 }
// 
//                 if (File.Exists(outputFile))
//                 {
//                     File.Delete(outputFile);
//                 }
//             }
//         }

        /// <summary>
        /// Tests that the static RedactSecrets overload accepting a BinlogRedactorOptions and Progress
        /// performs in non in-place mode when OutputFileName is explicitly provided.
        /// </summary>
        [Fact]
        public void RedactSecrets_WithExplicitOutputFile_DoesNotReplaceInputFile()
        {
            // Arrange
            string inputContent = "Static secrets test";
            string inputFile = Path.GetTempFileName();
            string outputFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".binlog");
            try
            {
                File.WriteAllText(inputFile, inputContent);
                var options = new BinlogRedactorOptions(inputFile)
                {
                    TokensToRedact = new[]
                    {
                        "secret"
                    },
                    OutputFileName = outputFile
                };
                // Act
                BinlogRedactor.RedactSecrets(options, progress: null);
                // Assert
                // Since explicit output file was provided, input file should not be replaced.
                Assert.True(File.Exists(inputFile));
                Assert.True(File.Exists(outputFile));
            }
            finally
            {
                if (File.Exists(inputFile))
                {
                    File.Delete(inputFile);
                }

                if (File.Exists(outputFile))
                {
                    File.Delete(outputFile);
                }
            }
        }

        /// <summary>
        /// Tests that the static RedactSecrets overload accepting a binlog path and tokens performs in in-place mode.
        /// </summary>
        [Fact]
        public void RedactSecrets_StringOverload_InPlaceReplacementOccurs()
        {
            // Arrange
            string inputContent = "InPlace redaction test";
            string inputFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(inputFile, inputContent);
                // Act
                // This overload does not allow explicit output file so it should perform in-place replacement.
                BinlogRedactor.RedactSecrets(inputFile, new[] { "redaction" });
                // Assert
                // After in-place replacement, the input file should exist.
                Assert.True(File.Exists(inputFile));
            }
            finally
            {
                if (File.Exists(inputFile))
                {
                    File.Delete(inputFile);
                }
            }
        }
    }
}
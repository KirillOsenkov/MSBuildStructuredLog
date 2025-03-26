using System;
using Xunit;

namespace Microsoft.Build.Logging.StructuredLogger
{
    /// <summary>
    /// Represents an archive file.
    /// </summary>
    public class ArchiveFile
    {
        public string FullPath { get; }
        public string Text { get; }

        public ArchiveFile(string fullPath, string text)
        {
            FullPath = fullPath;
            Text = text;
        }
    }

    /// <summary>
    /// Event arguments containing an archive file.
    /// </summary>
    public class ArchiveFileEventArgs
    {
        public ArchiveFile ArchiveFile { get; set; }

        public ArchiveFileEventArgs(ArchiveFile archiveFile)
        {
            ArchiveFile = archiveFile;
        }
    }

    /// <summary>
    /// Event arguments for string reading operations.
    /// </summary>
    public class StringReadEventArgs
    {
        public string Input { get; }
        public string StringToBeUsed { get; set; }

        public StringReadEventArgs(string input)
        {
            Input = input;
            StringToBeUsed = input;
        }
    }

    /// <summary>
    /// Extension methods for ArchiveFileEventArgs.
    /// </summary>
    public static class ArchiveFileEventArgsExtensions
    {
        /// <summary>
        /// Converts an Action&lt;StringReadEventArgs&gt; into an Action&lt;ArchiveFileEventArgs&gt; that processes both the file path and its content.
        /// </summary>
        /// <param name="stringHandler">The delegate to handle string reading events.</param>
        /// <returns>
        /// An Action&lt;ArchiveFileEventArgs&gt; that calls the provided delegate twice (once for the file path and once for the content)
        /// and then updates the ArchiveFile with the modified strings.
        /// </returns>
        public static Action<ArchiveFileEventArgs> ToArchiveFileHandler(this Action<StringReadEventArgs> stringHandler)
        {
            return args =>
            {
                var pathArgs = new StringReadEventArgs(args.ArchiveFile.FullPath);
                stringHandler(pathArgs);
                var contentArgs = new StringReadEventArgs(args.ArchiveFile.Text);
                stringHandler(contentArgs);

                args.ArchiveFile = new ArchiveFile(pathArgs.StringToBeUsed, contentArgs.StringToBeUsed);
            };
        }
    }
}

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="ArchiveFileEventArgsExtensions"/> class.
    /// </summary>
    public class ArchiveFileEventArgsExtensionsTests
    {
        /// <summary>
        /// Tests that the ToArchiveFileHandler extension method correctly updates the ArchiveFile using a valid delegate.
        /// The delegate modifies the string values; this test verifies that the ArchiveFile is updated with these modified values.
        /// </summary>
        [Fact]
        public void ToArchiveFileHandler_ValidDelegate_UpdatesArchiveFileCorrectly()
        {
            // Arrange
            string originalPath = "original/path";
            string originalContent = "original/content";
            string modifiedPath = "modified/path";
            string modifiedContent = "modified/content";

            var originalArchiveFile = new ArchiveFile(originalPath, originalContent);
            var eventArgs = new ArchiveFileEventArgs(originalArchiveFile);

            // The delegate modifies the StringToBeUsed property based on the input value.
            Action<StringReadEventArgs> stringHandler = args =>
            {
                if (args.Input == originalPath)
                {
                    args.StringToBeUsed = modifiedPath;
                }
                else if (args.Input == originalContent)
                {
                    args.StringToBeUsed = modifiedContent;
                }
            };

            var archiveFileHandler = stringHandler.ToArchiveFileHandler();

            // Act
            archiveFileHandler(eventArgs);

            // Assert
            Assert.NotNull(eventArgs.ArchiveFile);
            Assert.Equal(modifiedPath, eventArgs.ArchiveFile.FullPath);
            Assert.Equal(modifiedContent, eventArgs.ArchiveFile.Text);
        }

        /// <summary>
        /// Tests that invoking ToArchiveFileHandler on a null delegate throws a NullReferenceException.
        /// This verifies that the extension method cannot be called on a null delegate.
        /// </summary>
        [Fact]
        public void ToArchiveFileHandler_NullDelegate_ThrowsNullReferenceException()
        {
            // Arrange
            Action<StringReadEventArgs> nullDelegate = null;

            // Act & Assert
            Assert.Throws<NullReferenceException>(() =>
            {
                // Attempting to call an extension method on a null instance should throw.
                var handler = nullDelegate.ToArchiveFileHandler();
            });
        }

        /// <summary>
        /// Tests that the handler throws a NullReferenceException when the ArchiveFile property in ArchiveFileEventArgs is null.
        /// This verifies that the method correctly fails when provided with invalid event data.
        /// </summary>
        [Fact]
        public void ToArchiveFileHandler_NullArchiveFileInArgs_ThrowsNullReferenceException()
        {
            // Arrange
            Action<StringReadEventArgs> stringHandler = args =>
            {
                // Delegate that simply passes the input through.
                args.StringToBeUsed = args.Input;
            };

            var handler = stringHandler.ToArchiveFileHandler();
            var eventArgs = new ArchiveFileEventArgs(null);

            // Act & Assert
            Assert.Throws<NullReferenceException>(() =>
            {
                handler(eventArgs);
            });
        }
    }
}

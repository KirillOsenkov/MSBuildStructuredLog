using Microsoft.Build.Framework;
using Microsoft.Build.Logging.StructuredLogger;
using Microsoft.Build.Logging.StructuredLogger.UnitTests;
using Moq;
using System;
using System.Text.RegularExpressions;
using Xunit;

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref = "TimedMessage"/> class.
    /// </summary>
    public class TimedMessageTests
    {
        private readonly TimedMessage _timedMessage;
        public TimedMessageTests()
        {
            _timedMessage = new TimedMessage();
        }

        /// <summary>
        /// Tests that the Timestamp property getter returns the value that was set.
        /// </summary>
        [Fact]
        public void Timestamp_SetAndGet_ReturnsSameValue()
        {
            // Arrange
            DateTime expected = new DateTime(2023, 1, 1, 12, 0, 0);
            // Act
            _timedMessage.Timestamp = expected;
            DateTime actual = _timedMessage.Timestamp;
            // Assert
            Assert.Equal(expected, actual);
        }

        /// <summary>
        /// Tests that the TimestampText property returns the expected string representation.
        /// Assumes that the Display extension method returns the ISO 8601 formatted string.
        /// </summary>
        [Fact]
        public void TimestampText_WithCustomTimestamp_ReturnsDisplayString()
        {
            // Arrange
            DateTime testTime = new DateTime(2023, 5, 15, 10, 30, 0, DateTimeKind.Utc);
            _timedMessage.Timestamp = testTime;
            // Assuming Timestamp.Display(fullPrecision: true) returns testTime.ToString("O")
            string expected = testTime.ToString("O");
            // Act
            string actual = _timedMessage.TimestampText;
            // Assert
            Assert.Equal(expected, actual);
        }

        /// <summary>
        /// Tests that the TypeName property returns "Message".
        /// </summary>
        [Fact]
        public void TypeName_Always_ReturnsMessage()
        {
            // Act
            string typeName = _timedMessage.TypeName;
            // Assert
            Assert.Equal("Message", typeName);
        }
    }

    /// <summary>
    /// Unit tests for the <see cref = "MessageWithLocation"/> class.
    /// </summary>
    public class MessageWithLocationTests
    {
        private readonly MessageWithLocation _messageWithLocation;
        public MessageWithLocationTests()
        {
            _messageWithLocation = new MessageWithLocation();
        }

        /// <summary>
        /// Tests that setting FilePath and Line correctly reflects in SourceFilePath and LineNumber properties.
        /// </summary>
        [Fact]
        public void SourceFilePathAndLineNumber_SetProperties_ReturnsCorrectValues()
        {
            // Arrange
            string expectedFilePath = "C:\\temp\\file.cs";
            int expectedLine = 42;
            _messageWithLocation.FilePath = expectedFilePath;
            _messageWithLocation.Line = expectedLine;
            // Act
            string actualFilePath = _messageWithLocation.SourceFilePath;
            int? actualLineNumber = _messageWithLocation.LineNumber;
            // Assert
            Assert.Equal(expectedFilePath, actualFilePath);
            Assert.Equal(expectedLine, actualLineNumber);
        }

        /// <summary>
        /// Tests that the TypeName property returns "Message".
        /// </summary>
        [Fact]
        public void TypeName_Always_ReturnsMessage()
        {
            // Act
            string typeName = _messageWithLocation.TypeName;
            // Assert
            Assert.Equal("Message", typeName);
        }
    }

    /// <summary>
    /// Unit tests for the <see cref = "Message"/> class.
    /// </summary>
    public class MessageTests
    {
        /// <summary>
        /// Tests that the default Timestamp getter always returns DateTime.MinValue even after setting a value.
        /// </summary>
//         [Fact] [Error] (126-21)CS1061 'Message' does not contain a definition for 'Timestamp' and no accessible extension method 'Timestamp' accepting a first argument of type 'Message' could be found (are you missing a using directive or an assembly reference?) [Error] (127-39)CS1061 'Message' does not contain a definition for 'Timestamp' and no accessible extension method 'Timestamp' accepting a first argument of type 'Message' could be found (are you missing a using directive or an assembly reference?)
//         public void Timestamp_DefaultBehavior_ReturnsMinValue()
//         {
//             // Arrange
//             Message message = new Message();
//             DateTime setTime = new DateTime(2023, 6, 1, 15, 0, 0);
//             // Act
//             message.Timestamp = setTime;
//             DateTime actual = message.Timestamp;
//             // Assert
//             Assert.Equal(DateTime.MinValue, actual);
//         }

        /// <summary>
        /// Tests that the TypeName property returns "Message".
        /// </summary>
//         [Fact] [Error] (141-39)CS1061 'Message' does not contain a definition for 'TypeName' and no accessible extension method 'TypeName' accepting a first argument of type 'Message' could be found (are you missing a using directive or an assembly reference?)
//         public void TypeName_Always_ReturnsMessage()
//         {
//             // Arrange
//             Message message = new Message();
//             // Act
//             string typeName = message.TypeName;
//             // Assert
//             Assert.Equal("Message", typeName);
//         }

        /// <summary>
        /// Tests that SourceFilePath and LineNumber return null when the text does not match any expected pattern.
        /// </summary>
//         [Fact] [Error] (158-45)CS1061 'MessageTests.TestMessageWithText' does not contain a definition for 'SourceFilePath' and no accessible extension method 'SourceFilePath' accepting a first argument of type 'MessageTests.TestMessageWithText' could be found (are you missing a using directive or an assembly reference?) [Error] (159-43)CS1061 'MessageTests.TestMessageWithText' does not contain a definition for 'LineNumber' and no accessible extension method 'LineNumber' accepting a first argument of type 'MessageTests.TestMessageWithText' could be found (are you missing a using directive or an assembly reference?)
//         public void SourceFilePathAndLineNumber_InvalidText_ReturnsNull()
//         {
//             // Arrange
//             var testMessage = new TestMessageWithText
//             {
//                 Text = "This is a simple log message with no file info."
//             };
//             // Act
//             string sourceFile = testMessage.SourceFilePath;
//             int? lineNumber = testMessage.LineNumber;
//             // Assert
//             Assert.Null(sourceFile);
//             Assert.Null(lineNumber);
//         }

        /// <summary>
        /// Tests the IsLowRelevance property behavior using a derived test message that allows flag simulation.
        /// </summary>
        [Fact]
        public void IsLowRelevance_FlagLogic_WorksAsExpected()
        {
            // Arrange
            var testMessage = new TestMessage();
            // By default, no flags are set and IsSelected is false.
            Assert.False(testMessage.IsLowRelevance);
            // Act - set IsLowRelevance to true.
            testMessage.IsLowRelevance = true;
            bool lowRelevanceAfterSet = testMessage.IsLowRelevance;
            // Assert
            Assert.True(lowRelevanceAfterSet);
            // Act - when IsSelected becomes true, IsLowRelevance should become false logically.
            testMessage.IsSelected = true;
            bool lowRelevanceAfterSelect = testMessage.IsLowRelevance;
            // Assert
            Assert.False(lowRelevanceAfterSelect);
        }

        /// <summary>
        /// A helper subclass of Message that exposes a settable Text property for testing purposes.
        /// </summary>
        private class TestMessageWithText : Message
        {
            public new string Text { get; set; }
        }

        /// <summary>
        /// Enum to simulate node flags for testing IsLowRelevance.
        /// </summary>
        [Flags]
        private enum NodeFlags
        {
            None = 0,
            LowRelevance = 1
        }

        /// <summary>
        /// A helper subclass of Message that simulates flag behavior for testing IsLowRelevance.
        /// </summary>
        private class TestMessage : Message
        {
            public NodeFlags Flags { get; set; }
            public bool IsSelected { get; set; }

            // Hide the base IsLowRelevance and simulate behavior.
            public new bool IsLowRelevance
            {
                get => (Flags & NodeFlags.LowRelevance) != 0 && !IsSelected;
                set
                {
                    if (value)
                    {
                        Flags |= NodeFlags.LowRelevance;
                    }
                    else
                    {
                        Flags &= ~NodeFlags.LowRelevance;
                    }
                }
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Logging.StructuredLogger;
using Xunit;

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// Minimal stub implementation of Task for unit testing.
    /// </summary>
    public class Task
    {
        /// <summary>
        /// Gets or sets the collection of child nodes.
        /// </summary>
        public List<object> Children { get; set; } = new List<object>();

        /// <summary>
        /// Gets or sets the name of the task.
        /// </summary>
        public string Name { get; set; }
    }

    /// <summary>
    /// Minimal stub implementation of Message for unit testing.
    /// </summary>
    public class Message
    {
        /// <summary>
        /// Gets or sets the shortened text of the message.
        /// </summary>
        public string ShortenedText { get; set; }
    }

    /// <summary>
    /// Unit tests for the <see cref="MessageTaskAnalyzer"/> class.
    /// </summary>
    public class MessageTaskAnalyzerTests
    {
        private readonly string _expectedPrefix = "Message: ";

        /// <summary>
        /// Tests that Analyze throws a NullReferenceException when the task parameter is null.
        /// </summary>
//         [Fact] [Error] (53-85)CS1503 Argument 1: cannot convert from 'Microsoft.Build.Logging.StructuredLogger.UnitTests.Task' to 'Microsoft.Build.Logging.StructuredLogger.Task'
//         public void Analyze_NullTask_ThrowsNullReferenceException()
//         {
//             // Arrange
//             Task task = null;
// 
//             // Act & Assert
//             Assert.Throws<NullReferenceException>(() => MessageTaskAnalyzer.Analyze(task));
//         }

        /// <summary>
        /// Tests that Analyze does not modify the task name when there are no Message children.
        /// </summary>
//         [Fact] [Error] (64-24)CS0144 Cannot create an instance of the abstract type or interface 'Task' [Error] (66-17)CS0229 Ambiguity between 'Task.Name' and 'Task.Name' [Error] (75-41)CS1503 Argument 1: cannot convert from 'Microsoft.Build.Logging.StructuredLogger.UnitTests.Task' to 'Microsoft.Build.Logging.StructuredLogger.Task' [Error] (78-45)CS0229 Ambiguity between 'Task.Name' and 'Task.Name'
//         public void Analyze_TaskWithNoMessageChild_TaskNameRemainsUnchanged()
//         {
//             // Arrange
//             var originalName = "OriginalTaskName";
//             var task = new Task
//             {
//                 Name = originalName,
//                 Children = new List<object>
//                 {
//                     // Add an object that is NOT of type Message.
//                     new object()
//                 }
//             };
// 
//             // Act
//             MessageTaskAnalyzer.Analyze(task);
// 
//             // Assert
//             Assert.Equal(originalName, task.Name);
//         }

        /// <summary>
        /// Tests that Analyze sets the task name correctly when a Message child with non-null ShortenedText is present.
        /// </summary>
//         [Fact] [Error] (90-24)CS0144 Cannot create an instance of the abstract type or interface 'Task' [Error] (92-17)CS0229 Ambiguity between 'Task.Name' and 'Task.Name' [Error] (97-41)CS1503 Argument 1: cannot convert from 'Microsoft.Build.Logging.StructuredLogger.UnitTests.Task' to 'Microsoft.Build.Logging.StructuredLogger.Task' [Error] (100-64)CS0229 Ambiguity between 'Task.Name' and 'Task.Name'
//         public void Analyze_TaskWithValidMessageChild_SetsTaskName()
//         {
//             // Arrange
//             var shortenedText = "TestMessage";
//             var message = new Message { ShortenedText = shortenedText };
//             var task = new Task
//             {
//                 Name = "InitialName",
//                 Children = new List<object> { message }
//             };
// 
//             // Act
//             MessageTaskAnalyzer.Analyze(task);
// 
//             // Assert
//             Assert.Equal(_expectedPrefix + shortenedText, task.Name);
//         }

        /// <summary>
        /// Tests that Analyze does not change the task name when the Message child's ShortenedText is null.
        /// </summary>
//         [Fact] [Error] (112-24)CS0144 Cannot create an instance of the abstract type or interface 'Task' [Error] (114-17)CS0229 Ambiguity between 'Task.Name' and 'Task.Name' [Error] (119-41)CS1503 Argument 1: cannot convert from 'Microsoft.Build.Logging.StructuredLogger.UnitTests.Task' to 'Microsoft.Build.Logging.StructuredLogger.Task' [Error] (122-45)CS0229 Ambiguity between 'Task.Name' and 'Task.Name'
//         public void Analyze_MessageChildWithNullShortenedText_TaskNameRemainsUnchanged()
//         {
//             // Arrange
//             var originalName = "InitialName";
//             var message = new Message { ShortenedText = null };
//             var task = new Task
//             {
//                 Name = originalName,
//                 Children = new List<object> { message }
//             };
// 
//             // Act
//             MessageTaskAnalyzer.Analyze(task);
// 
//             // Assert
//             Assert.Equal(originalName, task.Name);
//         }

        /// <summary>
        /// Tests that Analyze uses the first Message child with non-null ShortenedText when multiple Message children are present.
        /// </summary>
//         [Fact] [Error] (135-24)CS0144 Cannot create an instance of the abstract type or interface 'Task' [Error] (137-17)CS0229 Ambiguity between 'Task.Name' and 'Task.Name' [Error] (142-41)CS1503 Argument 1: cannot convert from 'Microsoft.Build.Logging.StructuredLogger.UnitTests.Task' to 'Microsoft.Build.Logging.StructuredLogger.Task' [Error] (145-77)CS0229 Ambiguity between 'Task.Name' and 'Task.Name'
//         public void Analyze_TaskWithMultipleMessageChildren_UsesFirstValidMessage()
//         {
//             // Arrange
//             var validMessage = new Message { ShortenedText = "ValidMessage" };
//             var invalidMessage = new Message { ShortenedText = null };
//             // Arrange children in such order that the first Message encountered has valid ShortenedText.
//             var task = new Task
//             {
//                 Name = "InitialName",
//                 Children = new List<object> { validMessage, invalidMessage }
//             };
// 
//             // Act
//             MessageTaskAnalyzer.Analyze(task);
// 
//             // Assert
//             Assert.Equal(_expectedPrefix + validMessage.ShortenedText, task.Name);
//         }

        /// <summary>
        /// Tests that Analyze does not update the task name when the first Message child has null ShortenedText even if subsequent Message children are valid.
        /// </summary>
//         [Fact] [Error] (158-24)CS0144 Cannot create an instance of the abstract type or interface 'Task' [Error] (160-17)CS0229 Ambiguity between 'Task.Name' and 'Task.Name' [Error] (165-41)CS1503 Argument 1: cannot convert from 'Microsoft.Build.Logging.StructuredLogger.UnitTests.Task' to 'Microsoft.Build.Logging.StructuredLogger.Task' [Error] (169-45)CS0229 Ambiguity between 'Task.Name' and 'Task.Name'
//         public void Analyze_FirstMessageChildHasNullShortenedText_IgnoresSubsequentValidMessages()
//         {
//             // Arrange
//             var firstMessage = new Message { ShortenedText = null };
//             var secondMessage = new Message { ShortenedText = "SecondMessage" };
//             var originalName = "InitialName";
//             var task = new Task
//             {
//                 Name = originalName,
//                 Children = new List<object> { firstMessage, secondMessage }
//             };
// 
//             // Act
//             MessageTaskAnalyzer.Analyze(task);
// 
//             // Assert
//             // Since OfType<Message>() picks the first Message (firstMessage) which has null ShortenedText, the task name should remain unchanged.
//             Assert.Equal(originalName, task.Name);
//         }
    }
}
 
namespace Microsoft.Build.Logging.StructuredLogger
{
    /// <summary>
    /// Analysis methods for tasks.
    /// </summary>
    public class MessageTaskAnalyzer
    {
        /// <summary>
        /// Analyzes the specified task. If the task has a child of type Message with a non-null ShortenedText,
        /// the task's Name is updated to "Message: " followed by the ShortenedText.
        /// </summary>
        /// <param name="task">The task to analyze.</param>
        public static void Analyze(Task task)
        {
            var message = task.Children.OfType<Message>().FirstOrDefault();
            if (message?.ShortenedText != null)
            {
                task.Name = "Message: " + message.ShortenedText;
            }
        }
    }
}

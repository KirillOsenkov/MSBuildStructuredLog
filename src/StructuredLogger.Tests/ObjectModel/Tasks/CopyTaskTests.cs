// using System;
// using System.Collections.Generic;
// using System.Linq;
// using System.Text.RegularExpressions;
// using Moq;
// using Xunit;
// 
// namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
// {
//     /// <summary>
//     /// A fake message class for unit testing that simulates log messages.
//     /// </summary>
// //     public class FakeMessage [Error] (13-18)CS0101 The namespace 'Microsoft.Build.Logging.StructuredLogger.UnitTests' already contains a definition for 'FakeMessage'
// //     {
// //         /// <summary>
// //         /// Gets or sets the text of the fake message.
// //         /// </summary>
// //         public string Text { get; set; }
// //     }
// 
//     /// <summary>
//     /// A testable subclass of CopyTask for overriding external dependencies (e.g. HasChildren and GetMessages).
//     /// </summary>
//     public class TestableCopyTask : CopyTask
//     {
//         private readonly bool hasChildren;
//         private readonly IEnumerable<FakeMessage> messages;
// 
//         /// <summary>
//         /// Initializes a new instance of the <see cref="TestableCopyTask"/> class.
//         /// </summary>
//         /// <param name="hasChildren">Indicates whether the task has children nodes.</param>
//         /// <param name="messages">The collection of fake messages to return.</param>
//         public TestableCopyTask(bool hasChildren, IEnumerable<FakeMessage> messages)
//         {
//             this.hasChildren = hasChildren;
//             this.messages = messages;
//         }
// 
//         /// <summary>
//         /// Overrides the HasChildren property to use test-specific value.
//         /// </summary>
// //         protected override bool HasChildren => hasChildren; [Error] (43-33)CS0506 'TestableCopyTask.HasChildren': cannot override inherited member 'TreeNode.HasChildren' because it is not marked virtual, abstract, or override
// 
//         /// <summary>
//         /// Overrides the GetMessages method to return fake messages from the test.
//         /// </summary>
//         /// <returns>A collection of fake messages.</returns>
// //         protected override IEnumerable<FakeMessage> GetMessages() [Error] (49-53)CS0506 'TestableCopyTask.GetMessages()': cannot override inherited member 'Task.GetMessages()' because it is not marked virtual, abstract, or override
// //         {
// //             return messages;
// //         }
//     }
// 
//     /// <summary>
//     /// Unit tests for the <see cref="CopyTask"/> class.
//     /// </summary>
//     public class CopyTaskTests
//     {
//         /// <summary>
//         /// Tests that the FileCopyOperations property returns an empty collection when the task has no children.
//         /// </summary>
//         [Fact]
//         public void FileCopyOperations_HasNoChildren_ReturnsEmptyCollection()
//         {
//             // Arrange: Create a testable task with HasChildren set to false.
//             var task = new TestableCopyTask(hasChildren: false, messages: new List<FakeMessage>());
// 
//             // Act: Retrieve the FileCopyOperations property.
//             var operations = task.FileCopyOperations;
// 
//             // Assert: The operations collection should be non-null and empty.
//             Assert.NotNull(operations);
//             Assert.Empty(operations);
//         }
// 
//         /// <summary>
//         /// Tests that the FileCopyOperations property returns an empty collection when messages do not match any expected regex.
//         /// </summary>
//         [Fact]
//         public void FileCopyOperations_MessagesDoNotMatchRegex_ReturnsEmptyCollection()
//         {
//             // Arrange: Create fake messages with texts that will not match any copy operation regex.
//             var fakeMessages = new List<FakeMessage>
//             {
//                 new FakeMessage { Text = "This is a regular log message without copy details." },
//                 new FakeMessage { Text = "Another unimportant message." }
//             };
//             var task = new TestableCopyTask(hasChildren: true, messages: fakeMessages);
// 
//             // Act: Retrieve the FileCopyOperations property.
//             var operations = task.FileCopyOperations;
// 
//             // Assert: As none of the messages match any expected copy operation pattern, the returned collection should be empty.
//             Assert.NotNull(operations);
//             Assert.Empty(operations);
//         }
// 
//         /// <summary>
//         /// Tests that the ParseCopyingFileFrom method returns a valid FileCopyOperation with Copied set to true when using a valid match.
//         /// </summary>
// //         [Fact] [Error] (112-38)CS0122 'CopyTask.ParseCopyingFileFrom(Match, bool)' is inaccessible due to its protection level
// //         public void ParseCopyingFileFrom_ValidMatchWithDefaultCopied_ReturnsOperationWithCopiedTrue()
// //         {
// //             // Arrange: Create a regex that produces groups "From" and "To".
// //             var pattern = new Regex(@"(?<From>source.txt)(?<To>dest.txt)");
// //             string input = "source.txtdest.txt";
// //             var match = pattern.Match(input);
// //             Assert.True(match.Success, "Regex should successfully match the input.");
// // 
// //             // Act: Call ParseCopyingFileFrom with the obtained match.
// //             var operation = CopyTask.ParseCopyingFileFrom(match);
// // 
// //             // Assert: The returned operation should have the correct source and destination, with Copied set to true.
// //             Assert.NotNull(operation);
// //             Assert.Equal("source.txt", operation.Source);
// //             Assert.Equal("dest.txt", operation.Destination);
// //             Assert.True(operation.Copied);
// //         }
// 
//         /// <summary>
//         /// Tests that the ParseCopyingFileFrom method returns a valid FileCopyOperation with Copied set to false when specified.
//         /// </summary>
// //         [Fact] [Error] (134-38)CS0122 'CopyTask.ParseCopyingFileFrom(Match, bool)' is inaccessible due to its protection level
// //         public void ParseCopyingFileFrom_ValidMatchWithCopiedFalse_ReturnsOperationWithCopiedFalse()
// //         {
// //             // Arrange: Create a regex with the named groups "From" and "To".
// //             var pattern = new Regex(@"(?<From>alpha.txt)(?<To>beta.txt)");
// //             string input = "alpha.txtbeta.txt";
// //             var match = pattern.Match(input);
// //             Assert.True(match.Success, "Regex should successfully match the input.");
// // 
// //             // Act: Call ParseCopyingFileFrom with copied parameter set to false.
// //             var operation = CopyTask.ParseCopyingFileFrom(match, copied: false);
// // 
// //             // Assert: Verify that the operation has the correct source, destination, and that Copied is false.
// //             Assert.NotNull(operation);
// //             Assert.Equal("alpha.txt", operation.Source);
// //             Assert.Equal("beta.txt", operation.Destination);
// //             Assert.False(operation.Copied);
// //         }
//     }
// }

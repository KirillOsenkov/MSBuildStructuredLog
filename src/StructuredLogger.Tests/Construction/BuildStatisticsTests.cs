// using System;
// using System.Collections.Generic;
// using Microsoft.Build.Logging.StructuredLogger;
// using Xunit;
// 
// namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
// {
//     /// <summary>
//     /// A fake task implementation to simulate a Task with a Name property.
//     /// </summary>
// //     internal class FakeTask [Error] (11-20)CS0101 The namespace 'Microsoft.Build.Logging.StructuredLogger.UnitTests' already contains a definition for 'FakeTask'
// //     {
// //         public string Name { get; }
// // 
// //         public FakeTask(string name)
// //         {
// //             Name = name;
// //         }
// //     }
// 
//     /// <summary>
//     /// An abstract representation of a Task used by BuildStatistics methods.
//     /// </summary>
// //     public abstract class Task [Error] (24-27)CS0101 The namespace 'Microsoft.Build.Logging.StructuredLogger.UnitTests' already contains a definition for 'Task'
// //     {
// //         public abstract string Name { get; }
// //     }
// 
//     /// <summary>
//     /// A wrapper class that adapts FakeTask to the expected Task type.
//     /// </summary>
// //     internal class TaskWrapper : Task [Error] (32-20)CS0534 'TaskWrapper' does not implement inherited abstract member 'Task.Name.get'
// //     {
// //         private readonly FakeTask _fakeTask;
// // 
// //         public TaskWrapper(FakeTask fakeTask)
// //         {
// //             _fakeTask = fakeTask;
// //         }
// // 
// //         public override string Name => _fakeTask?.Name; [Error] (41-32)CS0506 'TaskWrapper.Name': cannot override inherited member 'Task.Name' because it is not marked virtual, abstract, or override
//     }
// 
//     /// <summary>
//     /// Unit tests for the <see cref="BuildStatistics"/> class.
//     /// </summary>
//     public class BuildStatisticsTests
//     {
//         private readonly BuildStatistics _buildStatistics;
// 
//         public BuildStatisticsTests()
//         {
//             _buildStatistics = new BuildStatistics();
//         }
// 
//         /// <summary>
//         /// Tests that ReportTaskParameterMessage adds a message to the TaskParameterMessagesByTask dictionary.
//         /// The test creates a fake task and verifies that the correct key and message are added.
//         /// </summary>
// //         [Fact] [Error] (68-57)CS1503 Argument 1: cannot convert from 'Microsoft.Build.Logging.StructuredLogger.UnitTests.TaskWrapper' to 'Microsoft.Build.Logging.StructuredLogger.Task'
// //         public void ReportTaskParameterMessage_ValidTaskAndMessage_MessageAddedToDictionary()
// //         {
// //             // Arrange
// //             var fakeTask = new FakeTask("Task1");
// //             var message = "Parameter Message";
// // 
// //             // Act
// //             _buildStatistics.ReportTaskParameterMessage(new TaskWrapper(fakeTask), message);
// // 
// //             // Assert
// //             Assert.True(_buildStatistics.TaskParameterMessagesByTask.ContainsKey("Task1"));
// //             var messagesList = _buildStatistics.TaskParameterMessagesByTask["Task1"];
// //             Assert.Contains(message, messagesList);
// //         }
// 
//         /// <summary>
//         /// Tests that ReportOutputItemMessage adds a message to the OutputItemMessagesByTask dictionary.
//         /// The test creates a fake task and verifies that the correct key and message are added.
//         /// </summary>
// //         [Fact] [Error] (88-54)CS1503 Argument 1: cannot convert from 'Microsoft.Build.Logging.StructuredLogger.UnitTests.TaskWrapper' to 'Microsoft.Build.Logging.StructuredLogger.Task'
// //         public void ReportOutputItemMessage_ValidTaskAndMessage_MessageAddedToDictionary()
// //         {
// //             // Arrange
// //             var fakeTask = new FakeTask("Task2");
// //             var message = "Output Message";
// // 
// //             // Act
// //             _buildStatistics.ReportOutputItemMessage(new TaskWrapper(fakeTask), message);
// // 
// //             // Assert
// //             Assert.True(_buildStatistics.OutputItemMessagesByTask.ContainsKey("Task2"));
// //             var messagesList = _buildStatistics.OutputItemMessagesByTask["Task2"];
// //             Assert.Contains(message, messagesList);
// //         }
// 
//         /// <summary>
//         /// Tests that Add creates a new bucket when the key is not present and adds the provided value.
//         /// </summary>
//         [Fact]
//         public void Add_KeyNotPresent_CreatesBucketAndAddsValue()
//         {
//             // Arrange
//             var dictionary = new Dictionary<string, List<string>>();
//             var key = "Key1";
//             var value = "Value1";
// 
//             // Act
//             _buildStatistics.Add(key, value, dictionary);
// 
//             // Assert
//             Assert.True(dictionary.ContainsKey(key));
//             Assert.Single(dictionary[key]);
//             Assert.Equal(value, dictionary[key][0]);
//         }
// 
//         /// <summary>
//         /// Tests that Add appends a value to an existing bucket when the key already exists.
//         /// </summary>
//         [Fact]
//         public void Add_KeyAlreadyPresent_AppendsValueToBucket()
//         {
//             // Arrange
//             var dictionary = new Dictionary<string, List<string>>
//             {
//                 { "Key1", new List<string> { "Initial" } }
//             };
//             var key = "Key1";
//             var value = "Value2";
// 
//             // Act
//             _buildStatistics.Add(key, value, dictionary);
// 
//             // Assert
//             Assert.True(dictionary.ContainsKey(key));
//             Assert.Equal(2, dictionary[key].Count);
//             Assert.Equal("Initial", dictionary[key][0]);
//             Assert.Equal(value, dictionary[key][1]);
//         }
// 
//         /// <summary>
//         /// Tests that Add correctly adds a null value to the bucket.
//         /// </summary>
//         [Fact]
//         public void Add_NullValue_AddsNullToBucket()
//         {
//             // Arrange
//             var dictionary = new Dictionary<string, List<string>>();
//             var key = "KeyWithNull";
// 
//             // Act
//             _buildStatistics.Add(key, null, dictionary);
// 
//             // Assert
//             Assert.True(dictionary.ContainsKey(key));
//             Assert.Single(dictionary[key]);
//             Assert.Null(dictionary[key][0]);
//         }
// 
//         /// <summary>
//         /// Tests that Add throws a NullReferenceException when the dictionary is null.
//         /// </summary>
//         [Fact]
//         public void Add_NullDictionary_ThrowsNullReferenceException()
//         {
//             // Arrange
//             string key = "SomeKey";
//             string value = "SomeValue";
//             Dictionary<string, List<string>> nullDictionary = null;
// 
//             // Act & Assert
//             Assert.Throws<NullReferenceException>(() => _buildStatistics.Add(key, value, nullDictionary));
//         }
// 
//         /// <summary>
//         /// Tests that ReportTaskParameterMessage throws a NullReferenceException when the task is null.
//         /// Since the method accesses task.Name, passing a null task results in an exception.
//         /// </summary>
// //         [Fact] [Error] (186-101)CS1503 Argument 1: cannot convert from 'Microsoft.Build.Logging.StructuredLogger.UnitTests.TaskWrapper' to 'Microsoft.Build.Logging.StructuredLogger.Task'
// //         public void ReportTaskParameterMessage_NullTask_ThrowsNullReferenceException()
// //         {
// //             // Arrange
// //             FakeTask nullFakeTask = null;
// //             var message = "Test Message";
// // 
// //             // Act & Assert
// //             Assert.Throws<NullReferenceException>(() => _buildStatistics.ReportTaskParameterMessage(new TaskWrapper(nullFakeTask), message));
// //         }
// 
//         /// <summary>
//         /// Tests that ReportOutputItemMessage throws a NullReferenceException when the task is null.
//         /// Since the method accesses task.Name, passing a null task results in an exception.
//         /// </summary>
// //         [Fact] [Error] (201-98)CS1503 Argument 1: cannot convert from 'Microsoft.Build.Logging.StructuredLogger.UnitTests.TaskWrapper' to 'Microsoft.Build.Logging.StructuredLogger.Task'
// //         public void ReportOutputItemMessage_NullTask_ThrowsNullReferenceException()
// //         {
// //             // Arrange
// //             FakeTask nullFakeTask = null;
// //             var message = "Test Message";
// // 
// //             // Act & Assert
// //             Assert.Throws<NullReferenceException>(() => _buildStatistics.ReportOutputItemMessage(new TaskWrapper(nullFakeTask), message));
// //         }
// 
//         /// <summary>
//         /// Tests the getter and setter of the TimedNodeCount property.
//         /// </summary>
//         [Fact]
//         public void TimedNodeCount_SetAndGet_ReturnsSetValue()
//         {
//             // Arrange
//             var expectedCount = 10;
// 
//             // Act
//             _buildStatistics.TimedNodeCount = expectedCount;
//             var actualCount = _buildStatistics.TimedNodeCount;
// 
//             // Assert
//             Assert.Equal(expectedCount, actualCount);
//         }
//     }
// }

// using Microsoft.Build.Logging.StructuredLogger;
// using Microsoft.Build.Logging.StructuredLogger.UnitTests;
// using Moq;
// using StructuredLogViewer;
// using StructuredLogViewer.UnitTests;
// using System;
// using System.Collections.Generic;
// using Xunit;
// 
// namespace StructuredLogViewer.UnitTests
// {
//     /// <summary>
//     /// Unit tests for the <see cref = "SearchResult"/> class.
//     /// </summary>
//     public class SearchResultTests
//     {
//         /// <summary>
//         /// Tests the default constructor of SearchResult to ensure initial properties are set correctly.
//         /// </summary>
// //         [Fact] [Error] (24-30)CS7036 There is no argument given that corresponds to the required parameter 'result' of 'SearchResult.SearchResult(string)' [Error] (26-32)CS1061 'SearchResult' does not contain a definition for 'Node' and no accessible extension method 'Node' accepting a first argument of type 'SearchResult' could be found (are you missing a using directive or an assembly reference?) [Error] (27-33)CS1061 'SearchResult' does not contain a definition for 'WordsInFields' and no accessible extension method 'WordsInFields' accepting a first argument of type 'SearchResult' could be found (are you missing a using directive or an assembly reference?) [Error] (28-32)CS1061 'SearchResult' does not contain a definition for 'FieldsToDisplay' and no accessible extension method 'FieldsToDisplay' accepting a first argument of type 'SearchResult' could be found (are you missing a using directive or an assembly reference?) [Error] (29-33)CS1061 'SearchResult' does not contain a definition for 'MatchedByType' and no accessible extension method 'MatchedByType' accepting a first argument of type 'SearchResult' could be found (are you missing a using directive or an assembly reference?) [Error] (30-52)CS1061 'SearchResult' does not contain a definition for 'Duration' and no accessible extension method 'Duration' accepting a first argument of type 'SearchResult' could be found (are you missing a using directive or an assembly reference?) [Error] (31-52)CS1061 'SearchResult' does not contain a definition for 'StartTime' and no accessible extension method 'StartTime' accepting a first argument of type 'SearchResult' could be found (are you missing a using directive or an assembly reference?) [Error] (32-52)CS1061 'SearchResult' does not contain a definition for 'EndTime' and no accessible extension method 'EndTime' accepting a first argument of type 'SearchResult' could be found (are you missing a using directive or an assembly reference?) [Error] (33-32)CS1061 'SearchResult' does not contain a definition for 'RootFolder' and no accessible extension method 'RootFolder' accepting a first argument of type 'SearchResult' could be found (are you missing a using directive or an assembly reference?) [Error] (34-32)CS1061 'SearchResult' does not contain a definition for 'AssociatedFileCopy' and no accessible extension method 'AssociatedFileCopy' accepting a first argument of type 'SearchResult' could be found (are you missing a using directive or an assembly reference?)
// //         public void DefaultConstructor_InitializesPropertiesCorrectly()
// //         {
// //             // Arrange & Act
// //             var result = new SearchResult();
// //             // Assert
// //             Assert.Null(result.Node);
// //             Assert.Empty(result.WordsInFields);
// //             Assert.Null(result.FieldsToDisplay);
// //             Assert.False(result.MatchedByType);
// //             Assert.Equal(default(TimeSpan), result.Duration);
// //             Assert.Equal(default(DateTime), result.StartTime);
// //             Assert.Equal(default(DateTime), result.EndTime);
// //             Assert.Null(result.RootFolder);
// //             Assert.Null(result.AssociatedFileCopy);
// //         }
// 
//         /// <summary>
//         /// Tests the parameterized constructor with a non-timed node to ensure the Node property is set and timing fields remain default.
//         /// </summary>
// //         [Fact] [Error] (46-53)CS1739 The best overload for 'SearchResult' does not have a parameter named 'includeDuration' [Error] (48-43)CS1061 'SearchResult' does not contain a definition for 'Node' and no accessible extension method 'Node' accepting a first argument of type 'SearchResult' could be found (are you missing a using directive or an assembly reference?) [Error] (50-52)CS1061 'SearchResult' does not contain a definition for 'Duration' and no accessible extension method 'Duration' accepting a first argument of type 'SearchResult' could be found (are you missing a using directive or an assembly reference?) [Error] (51-52)CS1061 'SearchResult' does not contain a definition for 'StartTime' and no accessible extension method 'StartTime' accepting a first argument of type 'SearchResult' could be found (are you missing a using directive or an assembly reference?) [Error] (52-52)CS1061 'SearchResult' does not contain a definition for 'EndTime' and no accessible extension method 'EndTime' accepting a first argument of type 'SearchResult' could be found (are you missing a using directive or an assembly reference?)
// //         public void Constructor_WithNonTimedNode_SetsNodeAndLeavesTimeDefaults()
// //         {
// //             // Arrange
// //             var fakeNode = new FakeBaseNode("NonTimedNode");
// //             // Act
// //             var result = new SearchResult(fakeNode, includeDuration: true, includeStart: true, includeEnd: true);
// //             // Assert
// //             Assert.Equal(fakeNode, result.Node);
// //             // Since fakeNode is not a TimedNode, time properties should remain default.
// //             Assert.Equal(default(TimeSpan), result.Duration);
// //             Assert.Equal(default(DateTime), result.StartTime);
// //             Assert.Equal(default(DateTime), result.EndTime);
// //         }
// 
//         /// <summary>
//         /// Tests the parameterized constructor with a timed node to ensure timing properties are set based on flags.
//         /// </summary>
// //         [Fact] [Error] (67-54)CS1739 The best overload for 'SearchResult' does not have a parameter named 'includeDuration' [Error] (69-44)CS1061 'SearchResult' does not contain a definition for 'Node' and no accessible extension method 'Node' accepting a first argument of type 'SearchResult' could be found (are you missing a using directive or an assembly reference?) [Error] (70-51)CS1061 'SearchResult' does not contain a definition for 'Duration' and no accessible extension method 'Duration' accepting a first argument of type 'SearchResult' could be found (are you missing a using directive or an assembly reference?) [Error] (72-52)CS1061 'SearchResult' does not contain a definition for 'StartTime' and no accessible extension method 'StartTime' accepting a first argument of type 'SearchResult' could be found (are you missing a using directive or an assembly reference?) [Error] (73-50)CS1061 'SearchResult' does not contain a definition for 'EndTime' and no accessible extension method 'EndTime' accepting a first argument of type 'SearchResult' could be found (are you missing a using directive or an assembly reference?)
// //         public void Constructor_WithTimedNode_SetsTimingPropertiesAccordingToFlags()
// //         {
// //             // Arrange
// //             var expectedDuration = TimeSpan.FromSeconds(10);
// //             var expectedStartTime = new DateTime(2020, 1, 1);
// //             var expectedEndTime = new DateTime(2020, 1, 1, 0, 0, 10);
// //             var timedNode = new FakeTimedNode("TimedNode", expectedDuration, expectedStartTime, expectedEndTime);
// //             // Act - include duration and end time only.
// //             var result = new SearchResult(timedNode, includeDuration: true, includeStart: false, includeEnd: true);
// //             // Assert
// //             Assert.Equal(timedNode, result.Node);
// //             Assert.Equal(expectedDuration, result.Duration);
// //             // StartTime is not included, so it should remain default.
// //             Assert.Equal(default(DateTime), result.StartTime);
// //             Assert.Equal(expectedEndTime, result.EndTime);
// //         }
// 
//         /// <summary>
//         /// Tests the AddMatch method to verify that adding a match at the beginning inserts at index 0.
//         /// </summary>
// //         [Fact] [Error] (83-30)CS7036 There is no argument given that corresponds to the required parameter 'result' of 'SearchResult.SearchResult(string)' [Error] (84-20)CS1061 'SearchResult' does not contain a definition for 'AddMatch' and no accessible extension method 'AddMatch' accepting a first argument of type 'SearchResult' could be found (are you missing a using directive or an assembly reference?) [Error] (86-20)CS1061 'SearchResult' does not contain a definition for 'AddMatch' and no accessible extension method 'AddMatch' accepting a first argument of type 'SearchResult' could be found (are you missing a using directive or an assembly reference?) [Error] (88-55)CS1061 'SearchResult' does not contain a definition for 'WordsInFields' and no accessible extension method 'WordsInFields' accepting a first argument of type 'SearchResult' could be found (are you missing a using directive or an assembly reference?) [Error] (89-54)CS1061 'SearchResult' does not contain a definition for 'WordsInFields' and no accessible extension method 'WordsInFields' accepting a first argument of type 'SearchResult' could be found (are you missing a using directive or an assembly reference?)
// //         public void AddMatch_WhenAddAtBeginning_InsertsAtStartOfList()
// //         {
// //             // Arrange
// //             var result = new SearchResult();
// //             result.AddMatch("Field1", "First", addAtBeginning: false);
// //             // Act
// //             result.AddMatch("Field2", "Second", addAtBeginning: true);
// //             // Assert
// //             Assert.Equal(("Field2", "Second"), result.WordsInFields[0]);
// //             Assert.Equal(("Field1", "First"), result.WordsInFields[1]);
// //         }
// 
//         /// <summary>
//         /// Tests the AddMatch method to verify that adding without the beginning flag appends the match to the list.
//         /// </summary>
// //         [Fact] [Error] (99-30)CS7036 There is no argument given that corresponds to the required parameter 'result' of 'SearchResult.SearchResult(string)' [Error] (100-20)CS1061 'SearchResult' does not contain a definition for 'AddMatch' and no accessible extension method 'AddMatch' accepting a first argument of type 'SearchResult' could be found (are you missing a using directive or an assembly reference?) [Error] (101-20)CS1061 'SearchResult' does not contain a definition for 'AddMatch' and no accessible extension method 'AddMatch' accepting a first argument of type 'SearchResult' could be found (are you missing a using directive or an assembly reference?) [Error] (103-57)CS1061 'SearchResult' does not contain a definition for 'WordsInFields' and no accessible extension method 'WordsInFields' accepting a first argument of type 'SearchResult' could be found (are you missing a using directive or an assembly reference?) [Error] (103-36)CS1061 'SearchResult' does not contain a definition for 'WordsInFields' and no accessible extension method 'WordsInFields' accepting a first argument of type 'SearchResult' could be found (are you missing a using directive or an assembly reference?)
// //         public void AddMatch_WhenNotAddingAtBeginning_AppendsToEndOfList()
// //         {
// //             // Arrange
// //             var result = new SearchResult();
// //             result.AddMatch("Field1", "Match1", addAtBeginning: false);
// //             result.AddMatch("Field2", "Match2", addAtBeginning: false);
// //             // Act
// //             var lastMatch = result.WordsInFields[result.WordsInFields.Count - 1];
// //             // Assert
// //             Assert.Equal(("Field2", "Match2"), lastMatch);
// //         }
// 
//         /// <summary>
//         /// Tests the AddMatchByNodeType method to ensure that the MatchedByType property is set to true.
//         /// </summary>
// //         [Fact] [Error] (115-30)CS7036 There is no argument given that corresponds to the required parameter 'result' of 'SearchResult.SearchResult(string)' [Error] (117-20)CS1061 'SearchResult' does not contain a definition for 'AddMatchByNodeType' and no accessible extension method 'AddMatchByNodeType' accepting a first argument of type 'SearchResult' could be found (are you missing a using directive or an assembly reference?) [Error] (119-32)CS1061 'SearchResult' does not contain a definition for 'MatchedByType' and no accessible extension method 'MatchedByType' accepting a first argument of type 'SearchResult' could be found (are you missing a using directive or an assembly reference?)
// //         public void AddMatchByNodeType_SetsMatchedByTypeToTrue()
// //         {
// //             // Arrange
// //             var result = new SearchResult();
// //             // Act
// //             result.AddMatchByNodeType();
// //             // Assert
// //             Assert.True(result.MatchedByType);
// //         }
// 
//         /// <summary>
//         /// Tests the ToString method when Node is not null, expecting it to return the Node's ToString value.
//         /// </summary>
// //         [Fact] [Error] (131-43)CS1503 Argument 1: cannot convert from 'StructuredLogViewer.UnitTests.FakeBaseNode' to 'string'
// //         public void ToString_WhenNodeIsNotNull_ReturnsNodeToString()
// //         {
// //             // Arrange
// //             var expectedString = "FakeNodeToString";
// //             var fakeNode = new FakeBaseNode(expectedString);
// //             var result = new SearchResult(fakeNode);
// //             // Act
// //             var toStringResult = result.ToString();
// //             // Assert
// //             Assert.Equal(expectedString, toStringResult);
// //         }
// 
//         /// <summary>
//         /// Tests the ToString method when Node is null, expecting it to return null.
//         /// </summary>
// //         [Fact] [Error] (145-30)CS7036 There is no argument given that corresponds to the required parameter 'result' of 'SearchResult.SearchResult(string)'
// //         public void ToString_WhenNodeIsNull_ReturnsNull()
// //         {
// //             // Arrange
// //             var result = new SearchResult();
// //             // Act
// //             var toStringResult = result.ToString();
// //             // Assert
// //             Assert.Null(toStringResult);
// //         }
//     }
// 
//     /// <summary>
//     /// A fake implementation of BaseNode for testing non-timed scenarios.
//     /// </summary>
//     internal class FakeBaseNode : BaseNode
//     {
//         private readonly string _value;
//         public FakeBaseNode(string value)
//         {
//             _value = value;
//         }
// 
//         /// <summary>
//         /// Returns the string representation of the node.
//         /// </summary>
//         /// <returns>The value passed in the constructor.</returns>
//         public override string ToString()
//         {
//             return _value;
//         }
//     }
// 
//     /// <summary>
//     /// A fake implementation of a TimedNode for testing timing features.
//     /// Inherits from FakeBaseNode and mimics TimedNode behavior.
//     /// </summary>
// //     internal class FakeTimedNode : FakeBaseNode, TimedNode [Error] (178-50)CS0104 'TimedNode' is an ambiguous reference between 'Microsoft.Build.Logging.StructuredLogger.UnitTests.TimedNode' and 'Microsoft.Build.Logging.StructuredLogger.TimedNode'
// //     {
// //         public TimeSpan Duration { get; }
// //         public DateTime StartTime { get; }
// //         public DateTime EndTime { get; }
// // 
// //         public FakeTimedNode(string value, TimeSpan duration, DateTime startTime, DateTime endTime) : base(value)
// //         {
// //             Duration = duration;
// //             StartTime = startTime;
// //             EndTime = endTime;
// //         }
// //     }
// }

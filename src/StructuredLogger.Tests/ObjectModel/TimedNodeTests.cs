using Microsoft.Build.Framework;
using Microsoft.Build.Logging.StructuredLogger;
using Microsoft.Build.Logging.StructuredLogger.UnitTests;
using StructuredLogViewer.UnitTests;
using System;
using Xunit;

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref = "TimedNode"/> class.
    /// </summary>
    public class TimedNodeTests
    {
        private readonly TimedNode _timedNode;
        public TimedNodeTests()
        {
            _timedNode = new TimedNode();
        }

        /// <summary>
        /// Tests that the Id property can be set and retrieved correctly.
        /// </summary>
//         [Fact] [Error] (30-24)CS1061 'TimedNode' does not contain a definition for 'Id' and no accessible extension method 'Id' accepting a first argument of type 'TimedNode' could be found (are you missing a using directive or an assembly reference?) [Error] (32-49)CS1061 'TimedNode' does not contain a definition for 'Id' and no accessible extension method 'Id' accepting a first argument of type 'TimedNode' could be found (are you missing a using directive or an assembly reference?)
//         public void IdProperty_SetAndGet_ReturnsExpectedValue()
//         {
//             // Arrange
//             const int expectedId = 42;
//             // Act
//             _timedNode.Id = expectedId;
//             // Assert
//             Assert.Equal(expectedId, _timedNode.Id);
//         }

        /// <summary>
        /// Tests that the NodeId property can be set and retrieved correctly.
        /// </summary>
//         [Fact] [Error] (44-24)CS1061 'TimedNode' does not contain a definition for 'NodeId' and no accessible extension method 'NodeId' accepting a first argument of type 'TimedNode' could be found (are you missing a using directive or an assembly reference?) [Error] (46-53)CS1061 'TimedNode' does not contain a definition for 'NodeId' and no accessible extension method 'NodeId' accepting a first argument of type 'TimedNode' could be found (are you missing a using directive or an assembly reference?)
//         public void NodeIdProperty_SetAndGet_ReturnsExpectedValue()
//         {
//             // Arrange
//             const int expectedNodeId = 7;
//             // Act
//             _timedNode.NodeId = expectedNodeId;
//             // Assert
//             Assert.Equal(expectedNodeId, _timedNode.NodeId);
//         }

        /// <summary>
        /// Tests that the Index property can be set and retrieved correctly.
        /// </summary>
//         [Fact] [Error] (58-24)CS1061 'TimedNode' does not contain a definition for 'Index' and no accessible extension method 'Index' accepting a first argument of type 'TimedNode' could be found (are you missing a using directive or an assembly reference?) [Error] (60-52)CS1061 'TimedNode' does not contain a definition for 'Index' and no accessible extension method 'Index' accepting a first argument of type 'TimedNode' could be found (are you missing a using directive or an assembly reference?)
//         public void IndexProperty_SetAndGet_ReturnsExpectedValue()
//         {
//             // Arrange
//             const int expectedIndex = 3;
//             // Act
//             _timedNode.Index = expectedIndex;
//             // Assert
//             Assert.Equal(expectedIndex, _timedNode.Index);
//         }

        /// <summary>
        /// Tests that the StartTime property can be set and retrieved correctly.
        /// </summary>
//         [Fact] [Error] (72-24)CS1061 'TimedNode' does not contain a definition for 'StartTime' and no accessible extension method 'StartTime' accepting a first argument of type 'TimedNode' could be found (are you missing a using directive or an assembly reference?) [Error] (74-56)CS1061 'TimedNode' does not contain a definition for 'StartTime' and no accessible extension method 'StartTime' accepting a first argument of type 'TimedNode' could be found (are you missing a using directive or an assembly reference?)
//         public void StartTimeProperty_SetAndGet_ReturnsExpectedValue()
//         {
//             // Arrange
//             DateTime expectedStartTime = DateTime.Now;
//             // Act
//             _timedNode.StartTime = expectedStartTime;
//             // Assert
//             Assert.Equal(expectedStartTime, _timedNode.StartTime);
//         }

        /// <summary>
        /// Tests that the EndTime property can be set and retrieved correctly.
        /// </summary>
//         [Fact] [Error] (86-24)CS1061 'TimedNode' does not contain a definition for 'EndTime' and no accessible extension method 'EndTime' accepting a first argument of type 'TimedNode' could be found (are you missing a using directive or an assembly reference?) [Error] (88-54)CS1061 'TimedNode' does not contain a definition for 'EndTime' and no accessible extension method 'EndTime' accepting a first argument of type 'TimedNode' could be found (are you missing a using directive or an assembly reference?)
//         public void EndTimeProperty_SetAndGet_ReturnsExpectedValue()
//         {
//             // Arrange
//             DateTime expectedEndTime = DateTime.Now.AddHours(1);
//             // Act
//             _timedNode.EndTime = expectedEndTime;
//             // Assert
//             Assert.Equal(expectedEndTime, _timedNode.EndTime);
//         }

        /// <summary>
        /// Tests that the Duration property returns the correct TimeSpan when EndTime is after StartTime.
        /// </summary>
//         [Fact] [Error] (100-24)CS1061 'TimedNode' does not contain a definition for 'StartTime' and no accessible extension method 'StartTime' accepting a first argument of type 'TimedNode' could be found (are you missing a using directive or an assembly reference?) [Error] (101-24)CS1061 'TimedNode' does not contain a definition for 'EndTime' and no accessible extension method 'EndTime' accepting a first argument of type 'TimedNode' could be found (are you missing a using directive or an assembly reference?) [Error] (104-50)CS1061 'TimedNode' does not contain a definition for 'Duration' and no accessible extension method 'Duration' accepting a first argument of type 'TimedNode' could be found (are you missing a using directive or an assembly reference?)
//         public void Duration_WhenEndTimeAfterStartTime_ReturnsPositiveTimeSpan()
//         {
//             // Arrange
//             DateTime start = DateTime.Now;
//             DateTime end = start.AddMinutes(30);
//             _timedNode.StartTime = start;
//             _timedNode.EndTime = end;
//             TimeSpan expectedDuration = end - start;
//             // Act
//             TimeSpan actualDuration = _timedNode.Duration;
//             // Assert
//             Assert.Equal(expectedDuration, actualDuration);
//         }

        /// <summary>
        /// Tests that the Duration property returns TimeSpan.Zero when EndTime is before StartTime.
        /// </summary>
//         [Fact] [Error] (118-24)CS1061 'TimedNode' does not contain a definition for 'StartTime' and no accessible extension method 'StartTime' accepting a first argument of type 'TimedNode' could be found (are you missing a using directive or an assembly reference?) [Error] (119-24)CS1061 'TimedNode' does not contain a definition for 'EndTime' and no accessible extension method 'EndTime' accepting a first argument of type 'TimedNode' could be found (are you missing a using directive or an assembly reference?) [Error] (121-50)CS1061 'TimedNode' does not contain a definition for 'Duration' and no accessible extension method 'Duration' accepting a first argument of type 'TimedNode' could be found (are you missing a using directive or an assembly reference?)
//         public void Duration_WhenEndTimeBeforeStartTime_ReturnsTimeSpanZero()
//         {
//             // Arrange
//             DateTime start = DateTime.Now;
//             DateTime end = start.AddMinutes(-5);
//             _timedNode.StartTime = start;
//             _timedNode.EndTime = end;
//             // Act
//             TimeSpan actualDuration = _timedNode.Duration;
//             // Assert
//             Assert.Equal(TimeSpan.Zero, actualDuration);
//         }

        /// <summary>
        /// Tests that the TypeName property returns the expected value "TimedNode".
        /// </summary>
        [Fact]
        public void TypeName_Property_ReturnsTimedNode()
        {
            // Act
            string typeName = _timedNode.TypeName;
            // Assert
            Assert.Equal("TimedNode", typeName);
        }

        /// <summary>
        /// Tests that the DurationText property returns a non-null string.
        /// Note: This test assumes that the external TextUtilities.DisplayDuration method returns a valid string.
        /// </summary>
//         [Fact] [Error] (149-24)CS1061 'TimedNode' does not contain a definition for 'StartTime' and no accessible extension method 'StartTime' accepting a first argument of type 'TimedNode' could be found (are you missing a using directive or an assembly reference?) [Error] (150-24)CS1061 'TimedNode' does not contain a definition for 'EndTime' and no accessible extension method 'EndTime' accepting a first argument of type 'TimedNode' could be found (are you missing a using directive or an assembly reference?) [Error] (152-46)CS1061 'TimedNode' does not contain a definition for 'DurationText' and no accessible extension method 'DurationText' accepting a first argument of type 'TimedNode' could be found (are you missing a using directive or an assembly reference?)
//         public void DurationText_Property_ReturnsNonNullOrEmptyString()
//         {
//             // Arrange
//             // Set a valid duration.
//             DateTime start = DateTime.Now;
//             DateTime end = start.AddSeconds(10);
//             _timedNode.StartTime = start;
//             _timedNode.EndTime = end;
//             // Act
//             string durationText = _timedNode.DurationText;
//             // Assert
//             Assert.False(string.IsNullOrEmpty(durationText));
//         }

        /// <summary>
        /// Tests that the GetTimeAndDurationText method returns a formatted string containing "Start:", "End:" and "Duration:".
        /// This test is performed for both fullPrecision false and true.
        /// </summary>
        /// <param name = "fullPrecision">Specifies whether full precision is requested.</param>
//         [Theory] [Error] (170-24)CS1061 'TimedNode' does not contain a definition for 'StartTime' and no accessible extension method 'StartTime' accepting a first argument of type 'TimedNode' could be found (are you missing a using directive or an assembly reference?) [Error] (171-24)CS1061 'TimedNode' does not contain a definition for 'EndTime' and no accessible extension method 'EndTime' accepting a first argument of type 'TimedNode' could be found (are you missing a using directive or an assembly reference?) [Error] (173-40)CS0122 'TimedNode.GetTimeAndDurationText()' is inaccessible due to its protection level
//         [InlineData(false)]
//         [InlineData(true)]
//         public void GetTimeAndDurationText_WhenCalled_ReturnsFormattedString(bool fullPrecision)
//         {
//             // Arrange
//             DateTime start = new DateTime(2023, 1, 1, 8, 30, 0);
//             DateTime end = start.AddHours(2);
//             _timedNode.StartTime = start;
//             _timedNode.EndTime = end;
//             // Act
//             string result = _timedNode.GetTimeAndDurationText(fullPrecision);
//             // Assert
//             Assert.Contains("Start:", result);
//             Assert.Contains("End:", result);
//             Assert.Contains("Duration:", result);
//         }

        /// <summary>
        /// Tests that the ToolTip property returns the same value as GetTimeAndDurationText().
        /// </summary>
//         [Fact] [Error] (189-24)CS1061 'TimedNode' does not contain a definition for 'StartTime' and no accessible extension method 'StartTime' accepting a first argument of type 'TimedNode' could be found (are you missing a using directive or an assembly reference?) [Error] (190-24)CS1061 'TimedNode' does not contain a definition for 'EndTime' and no accessible extension method 'EndTime' accepting a first argument of type 'TimedNode' could be found (are you missing a using directive or an assembly reference?) [Error] (191-49)CS0122 'TimedNode.GetTimeAndDurationText()' is inaccessible due to its protection level
//         public void ToolTip_Property_EqualsGetTimeAndDurationText()
//         {
//             // Arrange
//             DateTime start = DateTime.Now;
//             DateTime end = start.AddMinutes(15);
//             _timedNode.StartTime = start;
//             _timedNode.EndTime = end;
//             string expectedToolTip = _timedNode.GetTimeAndDurationText();
//             // Act
//             string actualToolTip = _timedNode.ToolTip;
//             // Assert
//             Assert.Equal(expectedToolTip, actualToolTip);
//         }
    }
}
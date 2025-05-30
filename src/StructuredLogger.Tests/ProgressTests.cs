using Microsoft.Build.Logging.StructuredLogger;
using Microsoft.Build.Logging.StructuredLogger.UnitTests;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref = "Progress"/> class.
    /// </summary>
//     public class ProgressTests [Error] (18-25)CS0144 Cannot create an instance of the abstract type or interface 'Progress'
//     {
//         private readonly Progress _progress;
//         public ProgressTests()
//         {
//             _progress = new Progress();
//         }
// 
//         /// <summary>
//         /// Tests that the default value of the CancellationToken property is CancellationToken.None.
//         /// </summary>
//         [Fact] [Error] (28-49)CS1061 'Progress' does not contain a definition for 'CancellationToken' and no accessible extension method 'CancellationToken' accepting a first argument of type 'Progress' could be found (are you missing a using directive or an assembly reference?)
//         public void CancellationToken_Default_ReturnsCancellationTokenNone()
//         {
//             // Act
//             CancellationToken token = _progress.CancellationToken;
//             // Assert
//             Assert.Equal(CancellationToken.None, token);
//         }
// 
//         /// <summary>
//         /// Tests that the CancellationToken property can be set and retrieved correctly.
//         /// </summary>
//         [Fact] [Error] (43-23)CS1061 'Progress' does not contain a definition for 'CancellationToken' and no accessible extension method 'CancellationToken' accepting a first argument of type 'Progress' could be found (are you missing a using directive or an assembly reference?) [Error] (44-55)CS1061 'Progress' does not contain a definition for 'CancellationToken' and no accessible extension method 'CancellationToken' accepting a first argument of type 'Progress' could be found (are you missing a using directive or an assembly reference?)
//         public void CancellationToken_SetValue_ReturnsSetValue()
//         {
//             // Arrange
//             using CancellationTokenSource cts = new CancellationTokenSource();
//             CancellationToken expectedToken = cts.Token;
//             // Act
//             _progress.CancellationToken = expectedToken;
//             CancellationToken actualToken = _progress.CancellationToken;
//             // Assert
//             Assert.Equal(expectedToken, actualToken);
//         }
// 
//         /// <summary>
//         /// Tests the Report(double) method to ensure it raises the Updated event with a ProgressUpdate having the correct Ratio.
//         /// </summary>
//         [Theory] [Error] (62-23)CS1061 'Progress' does not contain a definition for 'Updated' and no accessible extension method 'Updated' accepting a first argument of type 'Progress' could be found (are you missing a using directive or an assembly reference?) [Error] (64-30)CS1503 Argument 1: cannot convert from 'double' to 'Microsoft.Build.Logging.StructuredLogger.UnitTests.ProgressUpdate' [Error] (67-48)CS1061 'ProgressUpdate' does not contain a definition for 'Value' and no accessible extension method 'Value' accepting a first argument of type 'ProgressUpdate' could be found (are you missing a using directive or an assembly reference?) [Error] (69-44)CS1061 'ProgressUpdate' does not contain a definition for 'Value' and no accessible extension method 'Value' accepting a first argument of type 'ProgressUpdate' could be found (are you missing a using directive or an assembly reference?)
//         [InlineData(0.0)]
//         [InlineData(0.5)]
//         [InlineData(1.0)]
//         [InlineData(-1.0)]
//         [InlineData(double.MaxValue)]
//         public void Report_DoubleValue_RaisesUpdatedEventWithCorrectProgressUpdate(double ratio)
//         {
//             // Arrange
//             ProgressUpdate? receivedUpdate = null;
//             _progress.Updated += update => receivedUpdate = update;
//             // Act
//             _progress.Report(ratio);
//             // Assert
//             Assert.NotNull(receivedUpdate);
//             Assert.Equal(ratio, receivedUpdate.Value.Ratio);
//             // BufferLength is not set by Report(double) and remains its default value.
//             Assert.Equal(0, receivedUpdate.Value.BufferLength);
//         }
// 
//         /// <summary>
//         /// Tests the Report(ProgressUpdate) method to ensure it raises the Updated event with the provided ProgressUpdate.
//         /// </summary>
//         [Fact] [Error] (85-23)CS1061 'Progress' does not contain a definition for 'Updated' and no accessible extension method 'Updated' accepting a first argument of type 'Progress' could be found (are you missing a using directive or an assembly reference?) [Error] (90-61)CS1061 'ProgressUpdate' does not contain a definition for 'Value' and no accessible extension method 'Value' accepting a first argument of type 'ProgressUpdate' could be found (are you missing a using directive or an assembly reference?) [Error] (91-68)CS1061 'ProgressUpdate' does not contain a definition for 'Value' and no accessible extension method 'Value' accepting a first argument of type 'ProgressUpdate' could be found (are you missing a using directive or an assembly reference?)
//         public void Report_ProgressUpdate_WithSubscriber_RaisesUpdatedEvent()
//         {
//             // Arrange
//             var expectedUpdate = new ProgressUpdate
//             {
//                 Ratio = 0.75,
//                 BufferLength = 1024
//             };
//             ProgressUpdate? actualUpdate = null;
//             _progress.Updated += update => actualUpdate = update;
//             // Act
//             _progress.Report(expectedUpdate);
//             // Assert
//             Assert.NotNull(actualUpdate);
//             Assert.Equal(expectedUpdate.Ratio, actualUpdate.Value.Ratio);
//             Assert.Equal(expectedUpdate.BufferLength, actualUpdate.Value.BufferLength);
//         }
// 
//         /// <summary>
//         /// Tests the Report(ProgressUpdate) method when no subscribers are attached; ensures no exception is thrown.
//         /// </summary>
//         [Fact] [Error] (102-23)CS1061 'Progress' does not contain a definition for 'Updated' and no accessible extension method 'Updated' accepting a first argument of type 'Progress' could be found (are you missing a using directive or an assembly reference?) [Error] (109-36)CS0117 'Record' does not contain a definition for 'Exception'
//         public void Report_ProgressUpdate_NoSubscriber_DoesNotThrow()
//         {
//             // Arrange
//             // Ensure no subscriber is attached.
//             _progress.Updated = null;
//             var progressUpdate = new ProgressUpdate
//             {
//                 Ratio = 0.25,
//                 BufferLength = 512
//             };
//             // Act & Assert
//             var exception = Record.Exception(() => _progress.Report(progressUpdate));
//             Assert.Null(exception);
//         }
//     }
}
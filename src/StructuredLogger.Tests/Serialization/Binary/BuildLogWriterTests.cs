// using System;
// using System.IO;
// using System.Reflection;
// using Microsoft.Build.Logging.StructuredLogger;
// using Xunit;
// 
// namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
// {
//     /// <summary>
//     /// Unit tests for the <see cref="BuildLogWriter"/> class.
//     /// </summary>
//     public class BuildLogWriterTests
//     {
//         /// <summary>
//         /// Tests the static Write method to ensure it completes execution without throwing exceptions when provided a valid build node.
//         /// This test creates a temporary file for writing and uses a fake build instance with minimal required properties.
//         /// Expected outcome: The method runs to completion without exceptions.
//         /// </summary>
// //         [Fact] [Error] (34-39)CS0117 'Record' does not contain a definition for 'Exception'
// //         public void Write_WithValidFakeBuild_CompletesWithoutException()
// //         {
// //             // Arrange: Create a temporary file path and a fake build object with minimal required values.
// //             string tempFilePath = Path.GetTempFileName();
// //             try
// //             {
// //                 var fakeBuild = new FakeBuild
// //                 {
// //                     Succeeded = true,
// //                     IsAnalyzed = false,
// //                     SourceFilesArchive = new byte[] { 0x01, 0x02 }
// //                 };
// // 
// //                 // Act & Assert: Call the static Write method and ensure no exception is thrown.
// //                 Exception ex = Record.Exception(() => BuildLogWriter.Write(fakeBuild, tempFilePath));
// //                 Assert.Null(ex);
// //             }
// //             finally
// //             {
// //                 // Cleanup: Remove the temporary file if it exists.
// //                 if (File.Exists(tempFilePath))
// //                 {
// //                     File.Delete(tempFilePath);
// //                 }
// //             }
// //         }
// 
//         /// <summary>
//         /// Tests the Dispose method to ensure that it calls Dispose on the internal writer and clears its reference.
//         /// This is achieved by using reflection to inject a fake tree writer that records whether Dispose was called.
//         /// Expected outcome: After calling Dispose, the internal writer field should be null and the fake writer's Dispose method should have been invoked.
//         /// </summary>
//         [Fact]
//         public void Dispose_WhenCalled_ClearsWriterAndCallsDisposeOnWriter()
//         {
//             // Arrange: Use reflection to create an instance of BuildLogWriter with a dummy file path.
//             string dummyFilePath = "dummy.bin";
//             ConstructorInfo ctor = typeof(BuildLogWriter).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, 
//                 binder: null, new Type[] { typeof(string) }, modifiers: null);
//             Assert.NotNull(ctor);
//             var buildLogWriterInstance = (BuildLogWriter)ctor.Invoke(new object[] { dummyFilePath });
//             Assert.NotNull(buildLogWriterInstance);
// 
//             // Create a fake tree writer and inject it into the BuildLogWriter instance via reflection.
//             var fakeTreeWriter = new FakeTreeBinaryWriter();
//             FieldInfo writerField = typeof(BuildLogWriter).GetField("writer", BindingFlags.NonPublic | BindingFlags.Instance);
//             Assert.NotNull(writerField);
//             writerField.SetValue(buildLogWriterInstance, fakeTreeWriter);
// 
//             // Act: Call Dispose on buildLogWriterInstance.
//             buildLogWriterInstance.Dispose();
// 
//             // Assert: Verify that the fake writer's Dispose method was called and the internal field is set to null.
//             Assert.True(fakeTreeWriter.DisposedCalled, "Expected the fake tree writer's Dispose method to be called.");
//             object writerValueAfterDispose = writerField.GetValue(buildLogWriterInstance);
//             Assert.Null(writerValueAfterDispose);
//         }
// 
//         #region Fake Types for Testing
// 
//         /// <summary>
//         /// A fake implementation of TreeBinaryWriter to capture method calls for testing purposes.
//         /// </summary>
//         private class FakeTreeBinaryWriter : IDisposable
//         {
//             /// <summary>
//             /// Gets a value indicating whether Dispose was called on this instance.
//             /// </summary>
//             public bool DisposedCalled { get; private set; }
// 
//             public void WriteNode(string nodeName)
//             {
//                 // No action needed for testing.
//             }
// 
//             public void WriteAttributeValue(string value)
//             {
//                 // No action needed for testing.
//             }
// 
//             public void WriteEndAttributes()
//             {
//                 // No action needed for testing.
//             }
// 
//             public void WriteChildrenCount(int count)
//             {
//                 // No action needed for testing.
//             }
// 
//             public void WriteByteArray(byte[] data)
//             {
//                 // No action needed for testing.
//             }
// 
//             public void Dispose()
//             {
//                 DisposedCalled = true;
//             }
//         }
// 
//         /// <summary>
//         /// A fake implementation of Build to use as a test input for BuildLogWriter.
//         /// This class provides minimal implementations for the properties used by BuildLogWriter.
//         /// </summary>
// //         private class FakeBuild : Build [Error] (125-23)CS0534 'BuildLogWriterTests.FakeBuild' does not implement inherited abstract member 'Build.EvaluationFolder.get' [Error] (125-23)CS0534 'BuildLogWriterTests.FakeBuild' does not implement inherited abstract member 'Build.GetOrCreateNodeWithName<T>(string)' [Error] (125-23)CS0534 'BuildLogWriterTests.FakeBuild' does not implement inherited abstract member 'Build.EvaluationFolder.set'
// //         {
// //             public override bool Succeeded { get; set; } [Error] (127-34)CS0115 'BuildLogWriterTests.FakeBuild.Succeeded': no suitable method found to override
// //             public override bool IsAnalyzed { get; set; } [Error] (128-34)CS0115 'BuildLogWriterTests.FakeBuild.IsAnalyzed': no suitable method found to override
// //             public override byte[] SourceFilesArchive { get; set; } [Error] (129-36)CS0115 'BuildLogWriterTests.FakeBuild.SourceFilesArchive': no suitable method found to override
//         }
// 
//         #endregion
//     }
// }

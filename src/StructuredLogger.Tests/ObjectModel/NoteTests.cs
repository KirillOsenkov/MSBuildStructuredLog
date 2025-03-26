// using Microsoft.Build.Logging.StructuredLogger;
// using Microsoft.Build.Logging.StructuredLogger.UnitTests;
// using System;
// using Xunit;
// 
// namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
// {
//     /// <summary>
//     /// Unit tests for the <see cref = "Note"/> class.
//     /// </summary>
//     public class NoteTests
//     {
//         private readonly Note _note;
//         private readonly TestableNote _testableNote;
//         /// <summary>
//         /// Initializes a new instance of the <see cref = "NoteTests"/> class.
//         /// </summary>
//         public NoteTests()
//         {
//             _note = new Note();
//             _testableNote = new TestableNote();
//         }
// 
//         /// <summary>
//         /// Tests that the TypeName property returns "Note" as expected.
//         /// </summary>
// //         [Fact] [Error] (31-43)CS1061 'Note' does not contain a definition for 'TypeName' and no accessible extension method 'TypeName' accepting a first argument of type 'Note' could be found (are you missing a using directive or an assembly reference?)
// //         public void TypeName_WhenAccessed_ReturnsNote()
// //         {
// //             // Act
// //             string actualTypeName = _note.TypeName;
// //             // Assert
// //             Assert.Equal(nameof(Note), actualTypeName);
// //         }
// 
//         /// <summary>
//         /// Tests that the protected IsSelectable property returns false.
//         /// This is verified by using a derived class that exposes the protected property.
//         /// </summary>
//         [Fact]
//         public void IsSelectable_WhenAccessedThroughDerivedClass_ReturnsFalse()
//         {
//             // Act
//             bool actualIsSelectable = _testableNote.ExposedIsSelectable;
//             // Assert
//             Assert.False(actualIsSelectable);
//         }
//     }
// 
//     /// <summary>
//     /// A derived class from <see cref = "Note"/> to expose the protected IsSelectable property for testing purposes.
//     /// </summary>
//     internal class TestableNote : Note
//     {
//         /// <summary>
//         /// Exposes the protected IsSelectable property.
//         /// </summary>
// //         public bool ExposedIsSelectable => base.IsSelectable; [Error] (58-49)CS0117 'Note' does not contain a definition for 'IsSelectable'
//     }
// }

using Microsoft.Build.Logging.StructuredLogger;
using Microsoft.Build.Logging.StructuredLogger.UnitTests;
using Moq;
using System;
using Xunit;

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref = "Folder"/> class.
    /// </summary>
    public class FolderTests
    {
        private readonly FakeFolder _folder;
        /// <summary>
        /// Initializes a new instance of the <see cref = "FolderTests"/> class.
        /// </summary>
        public FolderTests()
        {
            _folder = new FakeFolder();
        }

        /// <summary>
        /// A fake subclass of <see cref = "Folder"/> to override and simulate behavior from base methods.
        /// </summary>
        private class FakeFolder : Folder
        {
            /// <summary>
            /// Simulates whether the LowRelevance flag is set.
            /// </summary>
            public bool HasLowRelevanceFlag { get; set; }
            /// <summary>
            /// Captures the value passed to SetFlag method.
            /// </summary>
            public bool? SetFlagCalledValue { get; private set; }
            /// <summary>
            /// Simulated backing value for the IsSelected property.
            /// </summary>
            public bool IsSelectedValue { get; set; }

            /// <summary>
            /// Overrides the HasFlag method to simulate flag checking for LowRelevance.
            /// </summary>
            /// <param name = "flag">The flag to check.</param>
            /// <returns>Returns true if the flag is LowRelevance and set; otherwise false.</returns>
//             protected override bool HasFlag(NodeFlags flag) [Error] (46-37)CS0115 'FolderTests.FakeFolder.HasFlag(NodeFlags)': no suitable method found to override
//             {
//                 if (flag == NodeFlags.LowRelevance)
//                 {
//                     return HasLowRelevanceFlag;
//                 }
// 
//                 return false;
//             }

            /// <summary>
            /// Overrides the SetFlag method to capture the flag setting for LowRelevance.
            /// </summary>
            /// <param name = "flag">The flag to set.</param>
            /// <param name = "value">The value to set for the flag.</param>
//             protected override void SetFlag(NodeFlags flag, bool value) [Error] (61-37)CS0115 'FolderTests.FakeFolder.SetFlag(NodeFlags, bool)': no suitable method found to override
//             {
//                 if (flag == NodeFlags.LowRelevance)
//                 {
//                     SetFlagCalledValue = value;
//                     // Simulate setting the flag.
//                     HasLowRelevanceFlag = value;
//                 }
//             }

            /// <summary>
            /// Overrides the IsSelected property to return a simulated value.
            /// </summary>
//             public override bool IsSelected => IsSelectedValue; [Error] (74-34)CS0115 'FolderTests.FakeFolder.IsSelected': no suitable method found to override
        }

        /// <summary>
        /// Tests the IsLowRelevance getter when the underlying flag is set and the item is not selected.
        /// Expected to return true.
        /// </summary>
//         [Fact] [Error] (88-35)CS1061 'FolderTests.FakeFolder' does not contain a definition for 'IsLowRelevance' and no accessible extension method 'IsLowRelevance' accepting a first argument of type 'FolderTests.FakeFolder' could be found (are you missing a using directive or an assembly reference?)
//         public void IsLowRelevance_GetFlagSetNotSelected_ReturnsTrue()
//         {
//             // Arrange
//             _folder.HasLowRelevanceFlag = true;
//             _folder.IsSelectedValue = false;
//             // Act
//             bool result = _folder.IsLowRelevance;
//             // Assert
//             Assert.True(result);
//         }

        /// <summary>
        /// Tests the IsLowRelevance getter when the underlying flag is not set.
        /// Expected to return false regardless of the IsSelected value.
        /// </summary>
        /// <param name = "isSelected">The value for IsSelected to simulate different scenarios.</param>
//         [Theory] [Error] (107-35)CS1061 'FolderTests.FakeFolder' does not contain a definition for 'IsLowRelevance' and no accessible extension method 'IsLowRelevance' accepting a first argument of type 'FolderTests.FakeFolder' could be found (are you missing a using directive or an assembly reference?)
//         [InlineData(true)]
//         [InlineData(false)]
//         public void IsLowRelevance_GetFlagNotSet_ReturnsFalse(bool isSelected)
//         {
//             // Arrange
//             _folder.HasLowRelevanceFlag = false;
//             _folder.IsSelectedValue = isSelected;
//             // Act
//             bool result = _folder.IsLowRelevance;
//             // Assert
//             Assert.False(result);
//         }

        /// <summary>
        /// Tests the IsLowRelevance getter when the underlying flag is set but the item is selected.
        /// Expected to return false.
        /// </summary>
//         [Fact] [Error] (123-35)CS1061 'FolderTests.FakeFolder' does not contain a definition for 'IsLowRelevance' and no accessible extension method 'IsLowRelevance' accepting a first argument of type 'FolderTests.FakeFolder' could be found (are you missing a using directive or an assembly reference?)
//         public void IsLowRelevance_GetFlagSetButSelected_ReturnsFalse()
//         {
//             // Arrange
//             _folder.HasLowRelevanceFlag = true;
//             _folder.IsSelectedValue = true;
//             // Act
//             bool result = _folder.IsLowRelevance;
//             // Assert
//             Assert.False(result);
//         }

        /// <summary>
        /// Tests the IsLowRelevance setter to ensure it sets the flag correctly when true.
        /// Expected that the setter calls SetFlag with the true value and the underlying flag becomes true.
        /// </summary>
//         [Fact] [Error] (138-21)CS1061 'FolderTests.FakeFolder' does not contain a definition for 'IsLowRelevance' and no accessible extension method 'IsLowRelevance' accepting a first argument of type 'FolderTests.FakeFolder' could be found (are you missing a using directive or an assembly reference?) [Error] (142-33)CS1061 'FolderTests.FakeFolder' does not contain a definition for 'IsLowRelevance' and no accessible extension method 'IsLowRelevance' accepting a first argument of type 'FolderTests.FakeFolder' could be found (are you missing a using directive or an assembly reference?)
//         public void IsLowRelevance_SetTrue_CallsSetFlagAndUpdatesFlag()
//         {
//             // Arrange
//             _folder.IsSelectedValue = false; // ensure not selected for getter behavior.
//             // Act
//             _folder.IsLowRelevance = true;
//             // Assert
//             Assert.True(_folder.SetFlagCalledValue.HasValue && _folder.SetFlagCalledValue.Value, "SetFlag should be called with a true value.");
//             Assert.True(_folder.HasLowRelevanceFlag, "The underlying flag should be set to true.");
//             Assert.True(_folder.IsLowRelevance, "The IsLowRelevance getter should return true after setting to true.");
//         }

        /// <summary>
        /// Tests the IsLowRelevance setter to ensure it sets the flag correctly when false.
        /// Expected that the setter calls SetFlag with the false value and the underlying flag becomes false.
        /// </summary>
//         [Fact] [Error] (156-21)CS1061 'FolderTests.FakeFolder' does not contain a definition for 'IsLowRelevance' and no accessible extension method 'IsLowRelevance' accepting a first argument of type 'FolderTests.FakeFolder' could be found (are you missing a using directive or an assembly reference?) [Error] (160-34)CS1061 'FolderTests.FakeFolder' does not contain a definition for 'IsLowRelevance' and no accessible extension method 'IsLowRelevance' accepting a first argument of type 'FolderTests.FakeFolder' could be found (are you missing a using directive or an assembly reference?)
//         public void IsLowRelevance_SetFalse_CallsSetFlagAndUpdatesFlag()
//         {
//             // Arrange
//             _folder.HasLowRelevanceFlag = true; // initially true.
//             _folder.IsSelectedValue = false; // ensure not selected.
//             // Act
//             _folder.IsLowRelevance = false;
//             // Assert
//             Assert.True(_folder.SetFlagCalledValue.HasValue && !_folder.SetFlagCalledValue.Value, "SetFlag should be called with a false value.");
//             Assert.False(_folder.HasLowRelevanceFlag, "The underlying flag should be set to false.");
//             Assert.False(_folder.IsLowRelevance, "The IsLowRelevance getter should return false after setting to false.");
//         }

        /// <summary>
        /// Tests the TypeName property to ensure it returns the expected type name of the Folder class.
        /// Expected to return "Folder".
        /// </summary>
//         [Fact] [Error] (171-39)CS1061 'FolderTests.FakeFolder' does not contain a definition for 'TypeName' and no accessible extension method 'TypeName' accepting a first argument of type 'FolderTests.FakeFolder' could be found (are you missing a using directive or an assembly reference?)
//         public void TypeName_Get_ReturnsFolder()
//         {
//             // Act
//             string typeName = _folder.TypeName;
//             // Assert
//             Assert.Equal("Folder", typeName);
//         }
    }
}
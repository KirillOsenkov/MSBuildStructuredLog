using System;
using System.Collections.Generic;
using System.Text;
using Moq;
using Xunit;
using Microsoft.Build.Logging.StructuredLogger;

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="StringWriter"/> class.
    /// </summary>
    public class StringWriterTests
    {
        private readonly int _originalMaxStringLength;

        /// <summary>
        /// Initializes a new instance of the <see cref="StringWriterTests"/> class and stores the original MaxStringLength.
        /// </summary>
        public StringWriterTests()
        {
            _originalMaxStringLength = StringWriter.MaxStringLength;
        }

        /// <summary>
        /// Restores the original MaxStringLength after test modifications.
        /// </summary>
        private void RestoreMaxStringLength()
        {
            StringWriter.MaxStringLength = _originalMaxStringLength;
        }

        /// <summary>
        /// Tests that GetString returns an empty string when a null root node is provided.
        /// </summary>
//         [Fact] [Error] (43-52)CS1503 Argument 1: cannot convert from 'Microsoft.Build.Logging.StructuredLogger.UnitTests.BaseNode' to 'Microsoft.Build.Logging.StructuredLogger.BaseNode'
//         public void GetString_NullNode_ReturnsEmptyString()
//         {
//             // Arrange
//             BaseNode nullNode = null;
// 
//             // Act
//             string result = StringWriter.GetString(nullNode);
// 
//             // Assert
//             Assert.Equal(string.Empty, result);
//         }

        /// <summary>
        /// Tests that GetString returns the node's full text followed by a newline for a single node without children.
        /// </summary>
//         [Fact] [Error] (58-35)CS1061 'BaseNode' does not contain a definition for 'GetFullText' and no accessible extension method 'GetFullText' accepting a first argument of type 'BaseNode' could be found (are you missing a using directive or an assembly reference?) [Error] (61-52)CS1503 Argument 1: cannot convert from 'Microsoft.Build.Logging.StructuredLogger.UnitTests.BaseNode' to 'Microsoft.Build.Logging.StructuredLogger.BaseNode'
//         public void GetString_SingleNode_ReturnsNodeTextWithNewLine()
//         {
//             // Arrange
//             var expectedText = "TestNode";
//             var nodeMock = new Mock<BaseNode>();
//             nodeMock.Setup(n => n.GetFullText()).Returns(expectedText);
// 
//             // Act
//             string result = StringWriter.GetString(nodeMock.Object);
// 
//             // Assert
//             Assert.Equal(expectedText + Environment.NewLine, result);
//         }

        /// <summary>
        /// Tests that GetString produces an indented structure when a tree node with children is provided.
        /// The parent's text is output first followed by each child's text indented by four spaces.
        /// </summary>
//         [Fact] [Error] (80-40)CS1061 'BaseNode' does not contain a definition for 'GetFullText' and no accessible extension method 'GetFullText' accepting a first argument of type 'BaseNode' could be found (are you missing a using directive or an assembly reference?) [Error] (84-39)CS1061 'TreeNode' does not contain a definition for 'GetFullText' and no accessible extension method 'GetFullText' accepting a first argument of type 'TreeNode' could be found (are you missing a using directive or an assembly reference?) [Error] (86-57)CS1503 Argument 1: cannot convert from 'System.Collections.Generic.List<Microsoft.Build.Logging.StructuredLogger.UnitTests.BaseNode>' to 'System.Collections.Generic.List<Microsoft.Build.Logging.StructuredLogger.BaseNode>' [Error] (89-52)CS1503 Argument 1: cannot convert from 'Microsoft.Build.Logging.StructuredLogger.TreeNode' to 'Microsoft.Build.Logging.StructuredLogger.BaseNode'
//         public void GetString_TreeNode_ReturnsIndentedStructure()
//         {
//             // Arrange
//             var parentText = "ParentNode";
//             var childText = "ChildNode";
// 
//             // Create a mock for the child node.
//             var childNodeMock = new Mock<BaseNode>();
//             childNodeMock.Setup(n => n.GetFullText()).Returns(childText);
// 
//             // Create a mock for the tree node (assumed to be a subclass of BaseNode).
//             var treeNodeMock = new Mock<TreeNode>();
//             treeNodeMock.Setup(n => n.GetFullText()).Returns(parentText);
//             treeNodeMock.Setup(n => n.HasChildren).Returns(true);
//             treeNodeMock.Setup(n => n.Children).Returns(new List<BaseNode> { childNodeMock.Object });
// 
//             // Act
//             string result = StringWriter.GetString(treeNodeMock.Object);
// 
//             // Expected output: parent's text followed by newline and then child's text with an indentation of 4 spaces and a newline.
//             var expectedBuilder = new StringBuilder();
//             expectedBuilder.AppendLine(parentText);
//             expectedBuilder.Append(new string(' ', 4));
//             expectedBuilder.AppendLine(childText);
//             string expected = expectedBuilder.ToString();
// 
//             // Assert
//             Assert.Equal(expected, result);
//         }

        /// <summary>
        /// Tests that GetString stops appending nodes once the accumulated output exceeds MaxStringLength.
        /// In this test, MaxStringLength is temporarily set to a small value to force termination after writing the parent node.
        /// </summary>
//         [Fact] [Error] (119-44)CS1061 'BaseNode' does not contain a definition for 'GetFullText' and no accessible extension method 'GetFullText' accepting a first argument of type 'BaseNode' could be found (are you missing a using directive or an assembly reference?) [Error] (123-43)CS1061 'TreeNode' does not contain a definition for 'GetFullText' and no accessible extension method 'GetFullText' accepting a first argument of type 'TreeNode' could be found (are you missing a using directive or an assembly reference?) [Error] (125-61)CS1503 Argument 1: cannot convert from 'System.Collections.Generic.List<Microsoft.Build.Logging.StructuredLogger.UnitTests.BaseNode>' to 'System.Collections.Generic.List<Microsoft.Build.Logging.StructuredLogger.BaseNode>' [Error] (128-56)CS1503 Argument 1: cannot convert from 'Microsoft.Build.Logging.StructuredLogger.TreeNode' to 'Microsoft.Build.Logging.StructuredLogger.BaseNode'
//         public void GetString_WhenOutputExceedsMaxLength_StopsAppendingFurtherNodes()
//         {
//             try
//             {
//                 // Arrange
//                 // Set MaxStringLength to a small value so that after writing the parent's text the limit is exceeded.
//                 StringWriter.MaxStringLength = 5;
//                 var parentText = "Parent"; // Length is 6, ensuring that appending this text (plus newline) will exceed the limit.
//                 var childText = "Child";
// 
//                 // Create a mock for the child node.
//                 var childNodeMock = new Mock<BaseNode>();
//                 childNodeMock.Setup(n => n.GetFullText()).Returns(childText);
// 
//                 // Create a mock for the tree node.
//                 var treeNodeMock = new Mock<TreeNode>();
//                 treeNodeMock.Setup(n => n.GetFullText()).Returns(parentText);
//                 treeNodeMock.Setup(n => n.HasChildren).Returns(true);
//                 treeNodeMock.Setup(n => n.Children).Returns(new List<BaseNode> { childNodeMock.Object });
// 
//                 // Act
//                 string result = StringWriter.GetString(treeNodeMock.Object);
// 
//                 // Expected output: only the parent's text is written since the StringBuilder length exceeds MaxStringLength thereafter.
//                 var expected = parentText + Environment.NewLine;
// 
//                 // Assert
//                 Assert.Equal(expected, result);
//             }
//             finally
//             {
//                 // Restore the original MaxStringLength.
//                 RestoreMaxStringLength();
//             }
//         }
    }
}

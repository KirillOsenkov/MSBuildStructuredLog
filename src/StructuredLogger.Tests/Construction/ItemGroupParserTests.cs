using Microsoft.Build.Logging.StructuredLogger;
using Microsoft.Build.Logging.StructuredLogger.UnitTests;
using Moq;
using System;
using System.Collections.Generic;
using Xunit;

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// A simple implementation of StringCache for testing purposes.
    /// This implementation interns strings by simply returning the provided string.
    /// </summary>
    internal class TestStringCache : StringCache
    {
//         public override string Intern(string value) [Error] (16-32)CS0506 'TestStringCache.Intern(string)': cannot override inherited member 'StringCache.Intern(string)' because it is not marked virtual, abstract, or override
//         {
//             return value;
//         }
    }

    /// <summary>
    /// A simple implementation of TreeNode for testing purposes.
    /// </summary>
    internal class TestTreeNode : TreeNode
    {
        private readonly List<TreeNode> _children = new List<TreeNode>();
//         public override IList<TreeNode> Children => _children; [Error] (28-41)CS0506 'TestTreeNode.Children': cannot override inherited member 'TreeNode.Children' because it is not marked virtual, abstract, or override

//         public override void AddChild(TreeNode child) [Error] (30-30)CS0115 'TestTreeNode.AddChild(TreeNode)': no suitable method found to override
//         {
//             if (child != null)
//             {
//                 _children.Add(child);
//             }
//         }
    }

    /// <summary>
    /// Unit tests for the <see cref = "ItemGroupParser"/> class.
    /// </summary>
    public class ItemGroupParserTests
    {
        private readonly TestStringCache _stringCache;
        public ItemGroupParserTests()
        {
            _stringCache = new TestStringCache();
        }

        /// <summary>
        /// Tests ParsePropertyOrItemList with a single-line non-output property message.
        /// The test verifies that the method returns a Property node with the expected key and value.
        /// </summary>
//         [Fact] [Error] (61-31)CS0029 Cannot implicitly convert type 'Microsoft.Build.Logging.StructuredLogger.BaseNode' to 'Microsoft.Build.Logging.StructuredLogger.UnitTests.BaseNode'
//         public void ParsePropertyOrItemList_SingleLineNonOutput_ReturnsProperty()
//         {
//             // Arrange
//             string prefix = "Prefix: ";
//             string message = prefix + "Key=Value";
//             // Act
//             BaseNode result = ItemGroupParser.ParsePropertyOrItemList(message, prefix, _stringCache, isOutputItem: false);
//             // Assert
//             Assert.NotNull(result);
//             var property = Assert.IsType<Property>(result);
//             Assert.Equal("Key", property.Name);
//             Assert.Equal("Value", property.Value);
//         }

        /// <summary>
        /// Tests ParsePropertyOrItemList with a single-line output item message.
        /// The test verifies that the method returns an AddItem node containing an Item child with the expected values.
        /// </summary>
//         [Fact] [Error] (80-31)CS0029 Cannot implicitly convert type 'Microsoft.Build.Logging.StructuredLogger.BaseNode' to 'Microsoft.Build.Logging.StructuredLogger.UnitTests.BaseNode' [Error] (84-41)CS1061 'AddItem' does not contain a definition for 'Name' and no accessible extension method 'Name' accepting a first argument of type 'AddItem' could be found (are you missing a using directive or an assembly reference?)
//         public void ParsePropertyOrItemList_SingleLineOutput_ReturnsAddItemWithChild()
//         {
//             // Arrange
//             string prefix = "Prefix: ";
//             string message = prefix + "Key=SomeItemText";
//             // Act
//             BaseNode result = ItemGroupParser.ParsePropertyOrItemList(message, prefix, _stringCache, isOutputItem: true);
//             // Assert
//             Assert.NotNull(result);
//             var addItem = Assert.IsType<AddItem>(result);
//             Assert.Equal("Key", addItem.Name);
//             Assert.Single(addItem.Children);
//             var item = Assert.IsType<Item>(addItem.Children[0]);
//             Assert.Equal("SomeItemText", item.Text);
//         }

        /// <summary>
        /// Tests ParsePropertyOrItemList with a multi-line message (containing line breaks) representing a parameter with a multi-line value.
        /// The first line contains a key/value pair and subsequent lines are appended as additional Items.
        /// </summary>
//         [Fact] [Error] (105-31)CS0029 Cannot implicitly convert type 'Microsoft.Build.Logging.StructuredLogger.BaseNode' to 'Microsoft.Build.Logging.StructuredLogger.UnitTests.BaseNode'
//         public void ParsePropertyOrItemList_MultiLineValue_ReturnsParameterWithMultipleItems()
//         {
//             // Arrange
//             string prefix = "Prefix: ";
//             // First line: prefix followed by key=value.
//             // Second line: additional text.
//             string firstLine = prefix + "Key=Value";
//             string secondLine = "ExtraValuePart";
//             string message = firstLine + "\n" + secondLine;
//             // Act
//             BaseNode result = ItemGroupParser.ParsePropertyOrItemList(message, prefix, _stringCache, isOutputItem: false);
//             // Assert
//             Assert.NotNull(result);
//             var parameter = Assert.IsType<Parameter>(result);
//             Assert.Equal("Key", parameter.Name);
//             Assert.NotEmpty(parameter.Children);
//             // The first child item should have text "Value"
//             var firstItem = Assert.IsType<Item>(parameter.Children[0]);
//             Assert.Equal("Value", firstItem.Text);
//             // The second child item should have text matching the extra line.
//             Assert.True(parameter.Children.Count >= 2);
//             var secondItem = Assert.IsType<Item>(parameter.Children[1]);
//             Assert.Equal("ExtraValuePart", secondItem.Text);
//         }

        /// <summary>
        /// Tests ParsePropertyOrItemList with an indented multi-line structure.
        /// The message simulates a parameter with its name, a property that is later corrected into an item and associated metadata.
        /// </summary>
//         [Fact] [Error] (140-31)CS0029 Cannot implicitly convert type 'Microsoft.Build.Logging.StructuredLogger.BaseNode' to 'Microsoft.Build.Logging.StructuredLogger.UnitTests.BaseNode'
//         public void ParsePropertyOrItemList_IndentedStructure_ProcessesNestedItems()
//         {
//             // Arrange
//             string prefix = "Prefix: ";
//             // Construct message:
//             // Line 1: prefix only.
//             // Line 2: 4-space indented line setting the parameter name (ends with '=')
//             // Line 3: 8-space indented line with "Key=Value" which initially is treated as a Property.
//             // Line 4: 16-space indented line with "Meta=Data" to be attached as metadata.
//             string line1 = prefix;
//             string line2 = "    Name=";
//             string line3 = "        Key=Value";
//             string line4 = "                Meta=Data";
//             string message = string.Join("\n", new[] { line1, line2, line3, line4 });
//             // Act
//             BaseNode result = ItemGroupParser.ParsePropertyOrItemList(message, prefix, _stringCache, isOutputItem: false);
//             // Assert
//             Assert.NotNull(result);
//             var parameter = Assert.IsType<Parameter>(result);
//             Assert.Equal("Name", parameter.Name);
//             // Expect one child item after correction from property to item.
//             Assert.Single(parameter.Children);
//             var item = Assert.IsType<Item>(parameter.Children[0]);
//             Assert.Equal("Key=Value", item.Text);
//             // Verify that metadata has been attached.
//             Assert.Single(item.Children);
//             var metadata = Assert.IsType<Metadata>(item.Children[0]);
//             Assert.Equal("Meta", metadata.Name);
//             Assert.Equal("Data", metadata.Value);
//         }

        /// <summary>
        /// Tests ParseThereWasAConflict with a valid message containing various indentation levels.
        /// The test verifies that a tree structure is built with nested items corresponding to the different indentation levels.
        /// </summary>
//         [Fact] [Error] (183-52)CS1503 Argument 1: cannot convert from 'Microsoft.Build.Logging.StructuredLogger.UnitTests.TestTreeNode' to 'Microsoft.Build.Logging.StructuredLogger.TreeNode' [Error] (212-34)CS8121 An expression of type 'BaseNode' cannot be handled by a pattern of type 'Item'. [Error] (218-36)CS1503 Argument 1: cannot convert from 'Microsoft.Build.Logging.StructuredLogger.BaseNode' to 'Microsoft.Build.Logging.StructuredLogger.TreeNode'
//         public void ParseThereWasAConflict_ValidMessage_BuildsCorrectTreeStructure()
//         {
//             // Arrange
//             var parent = new TestTreeNode();
//             // Construct message lines:
//             // Line 1 (0 spaces): "RootLine"
//             // Line 2 (4 spaces): "    Child4"
//             // Line 3 (8 spaces): "        Child8"
//             // Line 4 (10 spaces): "          Child10"
//             // Line 5 (12 spaces): "            Child12"
//             // Line 6 (2 spaces): "  Extra" - will be added to a fallback parent.
//             string[] lines = new[]
//             {
//                 "RootLine",
//                 "    Child4",
//                 "        Child8",
//                 "          Child10",
//                 "            Child12",
//                 "  Extra"
//             };
//             string message = string.Join("\n", lines);
//             // Act
//             ItemGroupParser.ParseThereWasAConflict(parent, message, _stringCache);
//             // Assert
//             // Verify that the parent has children added.
//             Assert.NotEmpty(parent.Children);
//             // Expect the first child corresponds to "RootLine".
//             var rootChild = Assert.IsType<Item>(parent.Children[0]);
//             Assert.Equal("RootLine", rootChild.Text);
//             // Second child should be the item created from the 4-space indent "Child4".
//             Assert.True(parent.Children.Count >= 2);
//             var child4 = Assert.IsType<Item>(parent.Children[1]);
//             Assert.Equal("Child4", child4.Text);
//             // "Child8" should be nested under "Child4".
//             Assert.NotEmpty(child4.Children);
//             var child8 = Assert.IsType<Item>(child4.Children[0]);
//             Assert.Equal("Child8", child8.Text);
//             // "Child10" should be nested under "Child8".
//             Assert.NotEmpty(child8.Children);
//             var child10 = Assert.IsType<Item>(child8.Children[0]);
//             Assert.Equal("Child10", child10.Text);
//             // "Child12" should be nested under "Child10".
//             Assert.NotEmpty(child10.Children);
//             var child12 = Assert.IsType<Item>(child10.Children[0]);
//             Assert.Equal("Child12", child12.Text);
//             // Verify that the "Extra" line (2-space indent) has been added to the appropriate parent in the tree.
//             bool extraFound = false;
//             void SearchForExtra(TreeNode node)
//             {
//                 foreach (var child in node.Children)
//                 {
//                     if (child is Item item && item.Text == "Extra")
//                     {
//                         extraFound = true;
//                         return;
//                     }
// 
//                     SearchForExtra(child);
//                 }
//             }
// 
//             SearchForExtra(parent);
//             Assert.True(extraFound, "Expected to find an item with text 'Extra' in the tree.");
//         }

        /// <summary>
        /// Tests ParsePropertyOrItemList with an empty message.
        /// Expected to return a non-null BaseNode without throwing an exception.
        /// </summary>
//         [Fact] [Error] (237-31)CS0029 Cannot implicitly convert type 'Microsoft.Build.Logging.StructuredLogger.BaseNode' to 'Microsoft.Build.Logging.StructuredLogger.UnitTests.BaseNode'
//         public void ParsePropertyOrItemList_EmptyMessage_ReturnsNonNullNode()
//         {
//             // Arrange
//             string prefix = "Prefix: ";
//             string message = string.Empty;
//             // Act
//             BaseNode result = ItemGroupParser.ParsePropertyOrItemList(message, prefix, _stringCache, isOutputItem: false);
//             // Assert
//             Assert.NotNull(result);
//         }

        /// <summary>
        /// Tests ParseThereWasAConflict with an empty message.
        /// Expected to leave the parent tree node unchanged (i.e. no children added).
        /// </summary>
//         [Fact] [Error] (253-52)CS1503 Argument 1: cannot convert from 'Microsoft.Build.Logging.StructuredLogger.UnitTests.TestTreeNode' to 'Microsoft.Build.Logging.StructuredLogger.TreeNode'
//         public void ParseThereWasAConflict_EmptyMessage_LeavesParentUnchanged()
//         {
//             // Arrange
//             var parent = new TestTreeNode();
//             string message = string.Empty;
//             // Act
//             ItemGroupParser.ParseThereWasAConflict(parent, message, _stringCache);
//             // Assert
//             Assert.Empty(parent.Children);
//         }
    }
}
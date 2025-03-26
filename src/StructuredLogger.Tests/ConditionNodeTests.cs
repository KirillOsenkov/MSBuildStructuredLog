using System;
using System.Collections.Generic;
using Moq;
using StructuredLogViewer;
using Xunit;

namespace StructuredLogViewer.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="ConditionNode"/> class.
    /// </summary>
    public class ConditionNodeTests
    {
        /// <summary>
        /// Tests that parsing a simple literal returns a child node with the expected text.
        /// This test ensures that the Parse method correctly interprets a basic string input.
        /// </summary>
        [Fact]
        public void Parse_SimpleLiteral_ReturnsSingleChildWithExpectedText()
        {
            // Arrange
            string input = "simple";

            // Act
            ConditionNode root = ConditionNode.Parse(input);

            // Assert
            Assert.NotNull(root);
            Assert.NotEmpty(root.Children);
            Assert.Single(root.Children);
            Assert.Equal("simple", root.Children[0].Text);
        }

        /// <summary>
        /// Tests that parsing a string with grouping parentheses returns a nested group with correct inner text.
        /// This test verifies that the parser creates a nested node for parenthesized expressions.
        /// </summary>
        [Fact]
        public void Parse_Grouping_ReturnsNestedGroupWithCorrectText()
        {
            // Arrange
            string input = "(value)";

            // Act
            ConditionNode root = ConditionNode.Parse(input);

            // Assert
            Assert.NotNull(root);
            Assert.NotEmpty(root.Children);
            
            // The first child of root should be a group node that was created on encountering '('.
            ConditionNode groupNode = root.Children[0];
            Assert.NotNull(groupNode);
            Assert.NotEmpty(groupNode.Children);
            Assert.Single(groupNode.Children);
            Assert.Equal("value", groupNode.Children[0].Text);
        }

        /// <summary>
        /// Tests that the GetEnumerator method returns the node itself followed by all of its descendants.
        /// This test manually constructs a tree and verifies enumeration output.
        /// </summary>
        [Fact]
        public void GetEnumerator_ReturnsSelfAndAllDescendants()
        {
            // Arrange
            var root = new ConditionNode { Text = "root" };
            var child1 = new ConditionNode { Text = "child1" };
            var child2 = new ConditionNode { Text = "child2" };
            root.Children.Add(child1);
            root.Children.Add(child2);

            // Act
            var nodes = new List<ConditionNode>();
            foreach (var node in root)
            {
                nodes.Add(node);
            }

            // Assert
            // Expecting the enumeration to include the root and its two children.
            Assert.Equal(3, nodes.Count);
            Assert.Contains(root, nodes);
            Assert.Contains(child1, nodes);
            Assert.Contains(child2, nodes);
        }

        /// <summary>
        /// Tests that ParseAndProcess correctly combines corresponding node texts from unevaluated and evaluated trees,
        /// and that it sets the evaluation result as expected.
        /// </summary>
        [Fact]
        public void ParseAndProcess_SingleNode_CombinesTextWithArrowAndSetsResult()
        {
            // Arrange
            // For unevaluated, use a plain literal.
            string unevaluated = "value1";
            // For evaluated, use a quoted boolean literal to trigger evaluation logic.
            string evaluated = "\"true\"";

            // Act
            ConditionNode resultNode = ConditionNode.ParseAndProcess(unevaluated, evaluated);

            // Assert
            Assert.NotNull(resultNode);
            Assert.NotEmpty(resultNode.Children);
            ConditionNode processedChild = resultNode.Children[0];

            // The expected behavior is that the texts are concatenated with a right arrow separating them,
            // and that the Result property is copied from the evaluated tree.
            string expectedText = "value1 \u2794 \"true\"";
            Assert.Equal(expectedText, processedChild.Text);
            Assert.True(processedChild.Result);
        }

        /// <summary>
        /// Tests that the Process method throws an exception when the two provided trees yield a different number of nodes.
        /// This test manually constructs trees with mismatched enumeration counts to ensure error handling.
        /// </summary>
        [Fact]
        public void Process_DifferentNumberOfNodes_ThrowsException()
        {
            // Arrange
            // Create an unevaluated node with only the root.
            ConditionNode unevaluated = new ConditionNode { Text = "root" };

            // Create an evaluated node with the root and an extra child.
            ConditionNode evaluated = new ConditionNode { Text = "root" };
            evaluated.Children.Add(new ConditionNode { Text = "child" });

            // Act & Assert
            Exception ex = Assert.Throws<Exception>(() => ConditionNode.Process(unevaluated, evaluated));
            Assert.Contains("Condition parsing return a different number of nodes", ex.Message);
        }

        /// <summary>
        /// Tests that parsing with evaluation enabled correctly processes a boolean literal.
        /// This test checks that when a valid boolean value is parsed, the evaluation returns the appropriate result.
        /// </summary>
        [Fact]
        public void Parse_WithEvaluation_BooleanLiteral_ReturnsCorrectResult()
        {
            // Arrange
            // The input "true" should be interpreted as a boolean literal.
            string input = "true";

            // Act
            ConditionNode root = ConditionNode.Parse(input, doEvaluate: true);

            // Assert
            Assert.NotNull(root);
            Assert.NotEmpty(root.Children);
            ConditionNode child = root.Children[0];
            Assert.True(child.Result);
        }

        /// <summary>
        /// Tests that parsing text containing quotes returns a node with the quotes properly retained in the text.
        /// This ensures that the parser does not strip or alter quoted strings.
        /// </summary>
        [Fact]
        public void Parse_WithQuotes_ReturnsProperlyQuotedText()
        {
            // Arrange
            string input = " 'test' ";

            // Act
            ConditionNode root = ConditionNode.Parse(input);

            // Assert
            Assert.NotNull(root);
            Assert.NotEmpty(root.Children);
            ConditionNode child = root.Children[0];
            // The parser appends the starting and trailing quotes, so the expected text should include them.
            string expectedText = "'test'";
            Assert.Equal(expectedText, child.Text);
        }
    }
}

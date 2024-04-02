using System.Linq;
using System.Threading;
using StructuredLogViewer;
using Xunit;

namespace StructuredLogger.Tests
{
    public class ConditionParserTests
    {
        [Fact]
        public void Empty_Test()
        {
            ParseAndAssert(@"", 1, evaluate: false);
            ParseAndAssert(@"( )", 2, evaluate: false);
            ParseAndAssert(@"(() )", 3, evaluate: false);
        }

        [Fact]
        public void EvaluatedNotEqual()
        {
            ParseAndAssert(@"'statement2' != 'statement2'", 2, expectedResult: false);
        }

        [Fact]
        public void EvaluatedEqual()
        {
            ParseAndAssert(@"'statement2' == 'statement2'", 2, expectedResult: true);
            ParseAndAssert(@"'statement2' == ''", 2, expectedResult: false);
            ParseAndAssert(@"'' == 'statement2'", 2, expectedResult: false);
        }

        [Fact]
        public void EvaluatedAnd()
        {
            ParseAndAssert(@"( 'statement1' != '' And 'statement2' != 'statement2' )", 4, expectedResult: false);
        }

        [Fact]
        public void EvaluatedOr()
        {
           ParseAndAssert(@"( 'statement1' != '' Or 'statement2' != 'statement2' )", 4, expectedResult: true);
        }

        [Fact]
        public void EvaluatedNestedStatements()
        {
            string evaluated = @"('statement1' != '' And ('statement2' != 'statement2' or 'statement3' != 'statement3') And 'statement4' != '')";

            var node = ConditionNode.Parse(evaluated, true);
            Assert.Equal(7, node.Count());
            Assert.False(node.Result);
            Assert.Equal(2, node.Max(p => p.Level));

            string evaluatedTrue = @"('statement1' != '' And ('statement2' == 'statement2' or 'statement3' != 'statement3') And 'statement4' != '')";

            var node2 = ConditionNode.Parse(evaluatedTrue, true);
            Assert.Equal(7, node2.Count());
            Assert.True(node2.Result);
            Assert.Equal(2, node2.Max(p => p.Level));
        }

        [Fact]
        public void Properties()
        {
            string unevaluated = @"('$(Property1)' != '' And '$(Property3)' != '$(Property3)' )";
            string evaluated = @"( 'statement1' != '' And 'statement2' != 'statement2' )";

            var node = ConditionNode.ParseAndProcess(unevaluated, evaluated);
            Assert.Equal(4, node.Count());
            Assert.False(node.Result);
        }

        [Fact]
        public void Items()
        {
            string unevaluated = @"('@(Item1)' != '' And '@(Item2)' != '@(Item2)' )";
            string evaluated = @"( 'statement1' != '' And 'statement2' != 'statement2' )";

            var node = ConditionNode.ParseAndProcess(unevaluated, evaluated);
            Assert.Equal(4, node.Count());
            Assert.False(node.Result);
        }

        [Fact]
        public void ItemMetadata()
        {
            string unevaluated = @"('%(Item.Data1)' != '' And '%(Item.Data2)' != '%(Item.Data2)' )";
            string evaluated = @"( 'statement1' != '' And 'statement2' != 'statement2' )";

            var node = ConditionNode.ParseAndProcess(unevaluated, evaluated);
            Assert.Equal(4, node.Count());
            Assert.False(node.Result);
        }

        [Fact]
        public void ItemTransformation()
        {
            string unevaluated = @"'%(Filename)%(Extension)' != '@(Items->'%(Filename)%(Extension)')'";
            string evaluated = @"'file.cs' != 'file.cs'";

            var node = ConditionNode.ParseAndProcess(unevaluated, evaluated);
            Assert.Equal(2, node.Count());
            Assert.False(node.Result);
        }

        [Fact]
        public void ConjunctionAnds()
        {
            string unevaluated = @" '$(EnableBaseIntermediateOutputPathMismatchWarning)' == 'true' And '$(_InitialBaseIntermediateOutputPath)' != '$(BaseIntermediateOutputPath)' And '$(BaseIntermediateOutputPath)' != '$(MSBuildProjectExtensionsPath)' ";
            string evaluatedFalse = @"'' == 'true' And '' != 'obj\' And 'obj\' != 'project\obj\'";

            var node = ConditionNode.ParseAndProcess(unevaluated, evaluatedFalse);
            Assert.Equal(4, node.Count());
            Assert.False(node.Result);

            string evaluatedTrue = @"'true' == 'true' And '' != 'obj\' And '' != 'project\obj\'";

            var node2 = ConditionNode.ParseAndProcess(unevaluated, evaluatedTrue);
            Assert.Equal(4, node2.Count());
            Assert.True(node2.Result);
        }

        [Fact]
        public void ConjunctionOrs()
        {
            string unevaluated = @" '$(EnableBaseIntermediateOutputPathMismatchWarning)' == 'true' Or '$(_InitialBaseIntermediateOutputPath)' != '$(BaseIntermediateOutputPath)' Or '$(BaseIntermediateOutputPath)' != '$(MSBuildProjectExtensionsPath)' ";
            string evaluatedTrue = @"'' == 'true' Or 'obj\' != 'obj\' Or 'obj\' != 'project\obj\'";

            var node = ConditionNode.ParseAndProcess(unevaluated, evaluatedTrue);
            Assert.Equal(4, node.Count());
            Assert.True(node.Result);

            string evaluatedFalse = @"'' == 'true' Or 'obj\' != 'obj\' Or 'project\obj\' != 'project\obj\'";

            var node2 = ConditionNode.ParseAndProcess(unevaluated, evaluatedFalse);
            Assert.Equal(4, node2.Count());
            Assert.False(node2.Result);
        }

        [Fact]
        public void ExistsNot()
        {
            var node = ParseAndAssert(@"!Exists($(File))", 2, evaluate: false);
            Assert.Equal("!Exists($(File))", node.Children[0].Text);
        }

        [Fact]
        public void Exists()
        {
            var node = ParseAndAssert(@"Exists($(File))", 2, evaluate: false);
            Assert.Equal("Exists($(File))", node.Children[0].Text);
        }

        [Fact]
        public void NoQuotesProperty()
        {
            var node = ParseAndAssert(@"$(file) == ''", 2, evaluate: false);
            Assert.Equal("$(file)==''", node.Children[0].Text);
        }

        [Fact]
        public void NumericCompareDouble()
        {
            ParseAndAssert(@"'123.456' < '567.123'", 2, expectedResult: true);
            ParseAndAssert(@"'123.456' <= '567.123'", 2, expectedResult: true);
            ParseAndAssert(@"'123.456' > '567.123'", 2, expectedResult: false);
            ParseAndAssert(@"'123.456' >= '567.123'", 2, expectedResult: false);
        }

        [Fact]
        public void NumericCompareVersion()
        {
            ParseAndAssert(@"'123.456.789' < '567.123.456'", 2, expectedResult: true);
            ParseAndAssert(@"'123.456.789' <= '567.123.456'", 2, expectedResult: true);
            ParseAndAssert(@"'123.456.789' > '567.123.456'", 2, expectedResult: false);
            ParseAndAssert(@"'123.456.789' >= '567.123.456'", 2, expectedResult: false);
        }

        [Fact]
        public void Boolean()
        {
            // test with whitespace
            var node = ParseAndAssert(@" false ", 2, expectedResult: false);
            Assert.Equal("false", node.Children[0].Text);

            var node2 = ParseAndAssert(@"true", 2, expectedResult: true);
            Assert.Equal("true", node2.Children[0].Text);

            var node3 = ParseAndAssert(@"!false", 2, expectedResult: true);
            Assert.Equal("!false", node3.Children[0].Text);
        }

        private static ConditionNode ParseAndAssert(string text, int expectedCount, bool evaluate = true, bool expectedResult = true)
        {
            var node = ConditionNode.Parse(text, evaluate);

            if (expectedCount == 1)
            {
                Assert.Single(node);
            }
            else
            {
                Assert.Equal(expectedCount, node.Count());
            }

            if (evaluate)
            {
                if (expectedResult)
                {
                    Assert.True(node.Result);
                }
                else
                {
                    Assert.False(node.Result);
                }
            }

            return node;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging.StructuredLogger;
using StructuredLogViewer;
using Xunit;
using Task = Microsoft.Build.Logging.StructuredLogger.Task;

namespace StructuredLogger.Tests
{
    public class SemanticRegressionTests
    {
        [Theory]
        [InlineData("needle", 0)]
        [InlineData("prefix needle", 0)]
        [InlineData("prefix NEEDLE", 0)]
        [InlineData("first\nprefix needle", 1)]
        [InlineData("first\r\nprefix needle", 1)]
        [InlineData("first\rprefix needle", 1)]
        public void SourceSearchIncludesMatchesEndingAtLineBoundary(string text, int expectedLine)
        {
            Assert.Equal(new[] { expectedLine }, new SourceText(text).Find("needle"));
        }

        [Fact]
        public void SourceSearchDoesNotMatchAcrossLinesOrReportMissingText()
        {
            var source = new SourceText("prefix\nneedle\nsuffix");
            Assert.Empty(source.Find("missing"));
            Assert.Empty(source.Find("prefix\nneedle"));
            Assert.Equal(new[] { 1 }, source.Find("needle"));
        }

        [Theory]
        [InlineData("\r")]
        [InlineData("a\r")]
        [InlineData("\r\r")]
        [InlineData("first\nlast\r")]
        [InlineData("first\r\nlast\r")]
        public void TrailingCarriageReturnStartsEmptyLineAtEnd(string text)
        {
            foreach (bool includeLineBreak in new[] { false, true })
            {
                var spans = text.GetLineSpans(includeLineBreak);
                var last = spans[spans.Count - 1];
                Assert.Equal(text.Length, last.Start);
                Assert.Equal(0, last.Length);
            }
        }

        [Theory]
        [InlineData(null, "symbol")]
        [InlineData("", "symbol")]
        [InlineData("value", "symbol \u2794 value")]
        public void ConditionProcessingRequiresBothTexts(string evaluatedText, string expectedText)
        {
            var unevaluated = new ConditionNode { Text = "symbol", Result = false };
            var evaluated = new ConditionNode { Text = evaluatedText, Result = true };

            var result = ConditionNode.Process(unevaluated, evaluated);

            Assert.Equal(expectedText, result.Text);
            Assert.True(result.Result);
        }

        [Theory]
        [InlineData("name=Foo value=bar")]
        [InlineData("skipped=true name=Foo value=bar")]
        [InlineData("name=Foo skipped=true value=bar")]
        [InlineData("name=Foo value=bar skipped=true")]
        [InlineData("height=2 name=Foo value=bar")]
        [InlineData("name=Foo height=max value=bar")]
        [InlineData("name=Foo value=bar height=2")]
        [InlineData("height=2 name=Foo skipped=false value=bar")]
        [InlineData("skipped=true value=bar height=2 name=Foo")]
        [InlineData("name=\"\" value=bar name=Foo")]
        [InlineData("value=\"\" name=Foo value=bar")]
        public void FieldQueryIndexesSurviveTermRemoval(string query)
        {
            var matcher = new NodeQueryMatcher(query);

            Assert.Equal(2, matcher.Terms.Count);
            Assert.Equal("Foo", matcher.Terms[matcher.NameTermIndex].Word);
            Assert.Equal("bar", matcher.Terms[matcher.ValueTermIndex].Word);
            Assert.NotNull(matcher.IsMatch(new Property { Name = "Foo", Value = "bar" }));
            Assert.Null(matcher.IsMatch(new Property { Name = "bar", Value = "Foo" }));
        }

        [Theory]
        [InlineData("duration")]
        [InlineData("start")]
        [InlineData("end")]
        public void TimedSearchResultsAreFlatAndSorted(string timing)
        {
            var start = new DateTime(2026, 1, 1);
            var first = new Task { Name = "First", StartTime = start.AddSeconds(2), EndTime = start.AddSeconds(3) };
            var second = new Task { Name = "Second", StartTime = start, EndTime = start.AddSeconds(10) };
            var firstProject = new Project { Name = "First project" };
            var secondProject = new Project { Name = "Second project" };
            firstProject.AddChild(first);
            secondProject.AddChild(second);
            var results = new[]
            {
                new SearchResult(first, timing == "duration", timing == "start", timing == "end"),
                new SearchResult(second, timing == "duration", timing == "start", timing == "end")
            };

            var tree = ResultTree.BuildResultTree(results, addDuration: false);
            var proxies = tree.Children.OfType<ProxyNode>().ToArray();

            Assert.Equal(2, proxies.Length);
            Assert.Same(timing == "end" ? first : second, proxies[0].Original);
            Assert.Same(timing == "end" ? second : first, proxies[1].Original);
        }

        [Fact]
        public void UntimedSearchResultsRemainNested()
        {
            var project = new Project { Name = "Project" };
            var task = new Task { Name = "Task" };
            project.AddChild(task);

            var tree = ResultTree.BuildResultTree(new[] { new SearchResult(task) }, addDuration: false);

            Assert.Same(project, Assert.Single(tree.Children.OfType<ProxyNode>()).Original);
        }

        [Fact]
        public void FindAllPathsIncludesBranchesWithSharedVertices()
        {
            var graph = Digraph.Create(new[]
            {
                ("A", "B"), ("A", "C"), ("B", "D"), ("C", "D"), ("D", "E")
            });
            var paths = new List<string>();

            graph.FindAllPaths(graph.TryFindVertex("A"), graph.TryFindVertex("E"),
                path => paths.Add(string.Join(",", path.Select(vertex => vertex.Value))));

            Assert.Equal(new[] { "A,B,D", "A,C,D" }, paths.OrderBy(path => path).ToArray());
        }

        [Fact]
        public void FindAllPathsDoesNotRevisitVerticesWithinAPath()
        {
            var graph = new Digraph();
            var start = graph.GetOrCreate("A");
            var middle = graph.GetOrCreate("B");
            var end = graph.GetOrCreate("C");
            start.Height = 2;
            end.Height = 0;
            start.AddChild(middle);
            middle.AddChild(start);
            middle.AddChild(end);
            start.AddChild(end);
            var paths = new List<string>();

            graph.FindAllPaths(start, end,
                path => paths.Add(string.Join(",", path.Select(vertex => vertex.Value))));

            Assert.Equal(new[] { "A", "A,B" }, paths.OrderBy(path => path).ToArray());
        }

        [Theory]
        [InlineData(true, false, false)]
        [InlineData(true, false, true)]
        [InlineData(true, true, false)]
        [InlineData(true, true, true)]
        [InlineData(false, false, false)]
        [InlineData(false, false, true)]
        [InlineData(false, true, false)]
        [InlineData(false, true, true)]
        public void TraversalFiltersLeaves(bool next, bool includeRejectedSubtree, bool includeMatch)
        {
            var root = new Folder();
            var anchor = new Folder();
            var accepted = new Property { Name = "accept" };
            var siblings = new List<BaseNode> { anchor };
            if (includeRejectedSubtree)
            {
                siblings.Add(new Folder());
            }

            siblings.Add(new Property { Name = "reject" });
            if (includeMatch)
            {
                siblings.Add(accepted);
            }

            if (!next)
            {
                siblings.Reverse();
            }

            foreach (var sibling in siblings)
            {
                root.AddChild(sibling);
            }

            Predicate<BaseNode> predicate = node => node is Property property && property.Name == "accept";
            var result = next
                ? anchor.FindNextInTraversalOrder(predicate)
                : anchor.FindPreviousInTraversalOrder(predicate);

            Assert.Same(includeMatch ? accepted : null, result);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void TraversalSearchesNonmatchingSubtrees(bool next)
        {
            var root = new Folder();
            var anchor = new Folder();
            var subtree = new Folder();
            var accepted = new Property { Name = "accept" };
            subtree.AddChild(accepted);
            root.AddChild(next ? anchor : subtree);
            root.AddChild(next ? subtree : anchor);
            Predicate<BaseNode> predicate = node => node is Property property && property.Name == "accept";

            var result = next
                ? anchor.FindNextInTraversalOrder(predicate)
                : anchor.FindPreviousInTraversalOrder(predicate);

            Assert.Same(accepted, result);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void TraversalWithoutPredicateReturnsAdjacentLeaf(bool next)
        {
            var root = new Folder();
            var anchor = new Folder();
            var leaf = new Property { Name = "value" };
            root.AddChild(next ? (BaseNode)anchor : leaf);
            root.AddChild(next ? (BaseNode)leaf : anchor);

            var result = next
                ? anchor.FindNextInTraversalOrder<Property>()
                : anchor.FindPreviousInTraversalOrder<Property>();

            Assert.Same(leaf, result);
        }

        [Fact]
        public void CustomEventPreservesTaskContextAndTimestamp()
        {
            Strings.Initialize("en-US");
            var construction = new Construction();
            var project = construction.GetOrAddProject(42);
            construction.Build.AddChild(project);
            project.AddChild(project.CreateTarget("Build", 1));
            var context = new BuildEventContext(1, 1, 42, 1, 1);
            construction.TaskStarted(null, new TaskStartedEventArgs("", "", "project.proj", "tasks.dll", "CustomTask")
            {
                BuildEventContext = context
            });
            var timestamp = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            construction.CustomEventRaised(null, new TestCustomEvent("custom message", timestamp)
            {
                BuildEventContext = context
            });

            var task = project.FindFirstDescendant<Task>();
            Assert.NotNull(task);
            var message = Assert.IsType<TimedMessage>(Assert.Single(task.Children.OfType<Message>()));
            Assert.Same(task, message.Parent);
            Assert.Equal("custom message", message.Text);
            Assert.Equal(timestamp, message.Timestamp.ToUniversalTime());
            Assert.Empty(construction.Build.Children.OfType<Message>());
            Assert.Empty(construction.Build.Children.OfType<Error>());
        }

        private sealed class TestCustomEvent : CustomBuildEventArgs
        {
            public TestCustomEvent(string message, DateTime timestamp)
                : base(message, null, null, timestamp)
            {
            }
        }

        [Theory]
        [InlineData("Environment")]
        [InlineData("Global Properties")]
        public void InitialPropertyAssignmentPreservesValueForNonFileSource(string source)
        {
            Strings.Initialize("en-US");
            var construction = new Construction();
            var processor = new MessageProcessor(construction, construction.StringTable);
            string text = $"Property initial value: $(Foo)=\"bar\" Source: {source}";
            var message = new BuildMessageEventArgs(text, null, null, MessageImportance.Low)
            {
                BuildEventContext = new BuildEventContext(1, 1, 42, -1, -1, -1, -1)
            };

            processor.AddMessage(message, text);

            var assignment = construction.Build.FindFirstDescendant<PropertyInitialAssignmentMessage>();
            Assert.NotNull(assignment);
            Assert.Equal(source, assignment.Source);
            Assert.Equal("bar", assignment.NewValue);
            Assert.Null(assignment.FilePath);
        }
    }
}

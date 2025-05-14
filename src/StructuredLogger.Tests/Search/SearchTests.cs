using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using StructuredLogViewer;
using Xunit;

namespace StructuredLogViewer.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="Search"/> class.
    /// </summary>
    public class SearchTests
    {
        private readonly CancellationTokenSource _cts;
        private readonly List<string> _dummyStrings;

        public SearchTests()
        {
            _cts = new CancellationTokenSource();
            _dummyStrings = new List<string> { "dummy" };
        }

        /// <summary>
        /// Tests that FindNodes returns an empty result set when provided with empty roots.
        /// </summary>
//         [Fact] [Error] (33-37)CS1503 Argument 1: cannot convert from 'System.Collections.Generic.List<StructuredLogViewer.UnitTests.TreeNode>' to 'System.Collections.Generic.IEnumerable<Microsoft.Build.Logging.StructuredLogger.TreeNode>'
//         public void FindNodes_WithEmptyRoots_ReturnsEmptyResultSet()
//         {
//             // Arrange
//             var emptyRoots = new List<TreeNode>();
//             var search = new Search(emptyRoots, _dummyStrings, 10, markResultsInTree: false);
// 
//             // Act
//             var results = search.FindNodes("query", CancellationToken.None);
// 
//             // Assert
//             Assert.Empty(results);
//         }

        /// <summary>
        /// Tests that FindNodes honors a cancelled cancellation token by returning an empty result set.
        /// </summary>
//         [Fact] [Error] (51-37)CS1503 Argument 1: cannot convert from 'System.Collections.Generic.List<StructuredLogViewer.UnitTests.TreeNode>' to 'System.Collections.Generic.IEnumerable<Microsoft.Build.Logging.StructuredLogger.TreeNode>'
//         public void FindNodes_WhenCancellationRequested_ReturnsEmptyResultSet()
//         {
//             // Arrange
//             var fakeNode = new FakeTreeNode(matchResult: null);
//             var roots = new List<TreeNode> { fakeNode };
//             var search = new Search(roots, _dummyStrings, 10, markResultsInTree: false);
//             var cts = new CancellationTokenSource();
//             cts.Cancel();
// 
//             // Act
//             var results = search.FindNodes("query", cts.Token);
// 
//             // Assert
//             Assert.Empty(results);
//         }

        /// <summary>
        /// Tests that FindNodes immediately returns results when a Build node's SearchExtension returns true.
        /// </summary>
//         [Fact] [Error] (73-37)CS1503 Argument 1: cannot convert from 'System.Collections.Generic.List<StructuredLogViewer.UnitTests.TreeNode>' to 'System.Collections.Generic.IEnumerable<Microsoft.Build.Logging.StructuredLogger.TreeNode>'
//         public void FindNodes_WithBuildSearchExtensionReturningResults_ReturnsEarlyWithResults()
//         {
//             // Arrange
//             var expectedResult = new SearchResult("EarlyResult");
//             var fakeSearchExtension = new FakeSearchExtension(expectedResult, shouldReturn: true);
//             var fakeBuild = new FakeBuild(new List<ISearchExtension> { fakeSearchExtension });
//             var roots = new List<TreeNode> { fakeBuild };
//             var search = new Search(roots, _dummyStrings, 10, markResultsInTree: false);
// 
//             // Act
//             var results = search.FindNodes("query", CancellationToken.None);
// 
//             // Assert
//             Assert.Single(results);
//             Assert.Equal(expectedResult, ((List<SearchResult>)results)[0]);
//         }

        /// <summary>
        /// Tests that FindNodes recursively visits tree nodes, collects matching results, and marks nodes when markResultsInTree is enabled.
        /// </summary>
//         [Fact] [Error] (98-37)CS1503 Argument 1: cannot convert from 'System.Collections.Generic.List<StructuredLogViewer.UnitTests.TreeNode>' to 'System.Collections.Generic.IEnumerable<Microsoft.Build.Logging.StructuredLogger.TreeNode>' [Error] (104-53)CS1503 Argument 1: cannot convert from 'System.Collections.Generic.IEnumerable<StructuredLogViewer.SearchResult>' to 'int'
//         public void FindNodes_WithMatchingNodesAndMarkResultsInTreeEnabled_ReturnsMatchesAndMarksNodes()
//         {
//             // Arrange
//             // Create a tree structure:
//             // root
//             //   ├ child1 (matches)
//             //   └ child2 (does not match)
//             var child1 = new FakeTreeNode(matchResult: new SearchResult("Match1"), shouldMark: true);
//             var child2 = new FakeTreeNode(matchResult: null);
//             var root = new FakeTreeNode(children: new List<BaseNode> { child1, child2 });
//             var roots = new List<TreeNode> { root };
//             var search = new Search(roots, _dummyStrings, maxResults: 10, markResultsInTree: true);
// 
//             // Act
//             var results = search.FindNodes("query", CancellationToken.None);
// 
//             // Assert
//             var resultList = new List<SearchResult>(results);
//             Assert.Single(resultList);
//             Assert.Equal("Match1", resultList[0].Result);
//             // Verify that nodes that matched and their ancestors have been marked
//             Assert.True(child1.IsSearchResult);
//             Assert.True(child1.ContainsSearchResult);
//             // child2 did not match so its flag should be false
//             Assert.False(child2.IsSearchResult);
//             Assert.False(child2.ContainsSearchResult);
//             // Root should reflect that one of its children matched
//             Assert.False(root.IsSearchResult);
//             Assert.True(root.ContainsSearchResult);
//         }

        /// <summary>
        /// Tests that ClearSearchResults does nothing when markResultsInTree is false.
        /// </summary>
//         [Fact] [Error] (125-28)CS7036 There is no argument given that corresponds to the required parameter 'value' of 'FakeBaseNode.FakeBaseNode(string)' [Error] (132-39)CS1503 Argument 1: cannot convert from 'StructuredLogViewer.UnitTests.FakeBuild' to 'Microsoft.Build.Logging.StructuredLogger.Build'
//         public void ClearSearchResults_WhenMarkResultsInTreeFalse_DoesNotModifyNodes()
//         {
//             // Arrange
//             var node = new FakeBaseNode();
//             // Set flags to true so that if ResetSearchResultStatus is called, they will be reset.
//             node.IsSearchResult = true;
//             node.ContainsSearchResult = true;
//             var fakeBuild = new FakeBuild(new List<BaseNode> { node });
//             
//             // Act
//             Search.ClearSearchResults(fakeBuild, markResultsInTree: false);
// 
//             // Assert
//             // Since markResultsInTree is false, ResetSearchResultStatus should not be called.
//             Assert.True(node.IsSearchResult);
//             Assert.True(node.ContainsSearchResult);
//         }

        /// <summary>
        /// Tests that ClearSearchResults resets search result flags on all nodes when markResultsInTree is true.
        /// </summary>
//         [Fact] [Error] (147-29)CS7036 There is no argument given that corresponds to the required parameter 'value' of 'FakeBaseNode.FakeBaseNode(string)' [Error] (148-29)CS7036 There is no argument given that corresponds to the required parameter 'value' of 'FakeBaseNode.FakeBaseNode(string)' [Error] (157-39)CS1503 Argument 1: cannot convert from 'StructuredLogViewer.UnitTests.FakeBuild' to 'Microsoft.Build.Logging.StructuredLogger.Build'
//         public void ClearSearchResults_WhenMarkResultsInTreeTrue_ResetsSearchResultStatusOnAllNodes()
//         {
//             // Arrange
//             var node1 = new FakeBaseNode();
//             var node2 = new FakeBaseNode();
//             // Set flags to true.
//             node1.IsSearchResult = true;
//             node1.ContainsSearchResult = true;
//             node2.IsSearchResult = true;
//             node2.ContainsSearchResult = true;
//             var fakeBuild = new FakeBuild(new List<BaseNode> { node1, node2 });
// 
//             // Act
//             Search.ClearSearchResults(fakeBuild, markResultsInTree: true);
// 
//             // Assert
//             Assert.False(node1.IsSearchResult);
//             Assert.False(node1.ContainsSearchResult);
//             Assert.False(node2.IsSearchResult);
//             Assert.False(node2.ContainsSearchResult);
//         }
    }

    #region Fake Implementations

    // Fake interfaces and classes to simulate dependencies used by Search.

    /// <summary>
    /// Represents a fake search extension used for testing Build's search extension behavior.
    /// </summary>
    public interface ISearchExtension
    {
        bool TryGetResults(NodeQueryMatcher matcher, List<SearchResult> resultSet, int maxResults);
    }

    /// <summary>
    /// A fake implementation of a search extension.
    /// </summary>
    public class FakeSearchExtension : ISearchExtension
    {
        private readonly SearchResult _resultToAdd;
        private readonly bool _shouldReturn;

        public FakeSearchExtension(SearchResult resultToAdd, bool shouldReturn)
        {
            _resultToAdd = resultToAdd;
            _shouldReturn = shouldReturn;
        }

        public bool TryGetResults(NodeQueryMatcher matcher, List<SearchResult> resultSet, int maxResults)
        {
            if (_shouldReturn)
            {
                resultSet.Add(_resultToAdd);
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// A fake implementation of BaseNode for testing purposes.
    /// </summary>
//     public class FakeBaseNode : BaseNode [Error] (207-18)CS0101 The namespace 'StructuredLogViewer.UnitTests' already contains a definition for 'FakeBaseNode'
//     {
//         public bool ResetCalled { get; private set; } = false;
// 
//         public override void ResetSearchResultStatus()
//         {
//             ResetCalled = true;
//             IsSearchResult = false;
//             ContainsSearchResult = false;
//         }
//     }

    /// <summary>
    /// A fake implementation of TreeNode for testing. Inherits from FakeBaseNode.
    /// </summary>
    public class FakeTreeNode : TreeNode
    {
        private readonly List<BaseNode> _children;

        /// <summary>
        /// Gets or sets the fake match result to return from the matcher.
        /// </summary>
        public SearchResult FakeMatchResult { get; }

        /// <summary>
        /// Specifies if the node should be marked as a match when evaluated.
        /// </summary>
        public bool ShouldMark { get; }

        public FakeTreeNode(SearchResult matchResult = null, bool shouldMark = false)
        {
            FakeMatchResult = matchResult;
            ShouldMark = shouldMark;
            _children = new List<BaseNode>();
        }

        public FakeTreeNode(List<BaseNode> children)
        {
            _children = children ?? new List<BaseNode>();
        }

        public override IList<BaseNode> Children => _children;

        public override bool HasChildren => _children != null && _children.Count > 0;

        // Simulate matching behavior: if FakeMatchResult is not null, then return it.
        public override SearchResult EvaluateMatch(NodeQueryMatcher matcher)
        {
            return FakeMatchResult;
        }
    }

    /// <summary>
    /// A fake implementation of Build, which is a specialized TreeNode with SearchExtensions and VisitAllChildren.
    /// </summary>
    public class FakeBuild : Build
    {
        private readonly List<BaseNode> _children = new List<BaseNode>();

        public override IList<BaseNode> Children => _children;

        public override bool HasChildren => _children.Count > 0;

        public IList<ISearchExtension> SearchExtensions { get; }

        public FakeBuild(IList<ISearchExtension> searchExtensions)
        {
            SearchExtensions = searchExtensions;
        }

        public FakeBuild(IList<BaseNode> children) : this(new List<ISearchExtension>())
        {
            _children.AddRange(children);
        }

        public override void VisitAllChildren<T>(Action<T> action)
        {
            // Visit self if applicable
            if (this is T node)
            {
                action(node);
            }
            // Recursively visit all children.
            foreach (var child in Children)
            {
                if (child is Build buildChild)
                {
                    buildChild.VisitAllChildren(action);
                }
                else if (child is T tChild)
                {
                    action(tChild);
                }
                else if (child is TreeNode treeNode)
                {
                    VisitAllChildrenRecursive(treeNode, action);
                }
            }
        }

        private void VisitAllChildrenRecursive<T>(TreeNode node, Action<T> action)
        {
            if (node is T tNode)
            {
                action(tNode);
            }
            foreach (var child in node.Children)
            {
                if (child is TreeNode childTree)
                {
                    VisitAllChildrenRecursive(childTree, action);
                }
                else if (child is T tChild)
                {
                    action(tChild);
                }
            }
        }
    }

    /// <summary>
    /// A fake implementation of Project to simulate nodes that trigger parallel search.
    /// </summary>
    public class FakeProject : Project
    {
        private readonly List<BaseNode> _children;

        public FakeProject(List<BaseNode> children)
        {
            _children = children;
        }

        public override IList<BaseNode> Children => _children;

        public override bool HasChildren => _children != null && _children.Count > 0;

        public override SearchResult EvaluateMatch(NodeQueryMatcher matcher)
        {
            // For simplicity, assume project nodes do not match.
            return null;
        }
    }

    /// <summary>
    /// Fake implementation of Project that inherits from TreeNode.
    /// </summary>
    public abstract class Project : TreeNode
    {
    }

    /// <summary>
    /// Fake implementation of Build that inherits from TreeNode.
    /// </summary>
    public abstract class Build : TreeNode
    {
        public abstract void VisitAllChildren<T>(Action<T> action);
    }

    /// <summary>
    /// Fake abstract base class representing a node.
    /// </summary>
    public abstract class BaseNode
    {
        /// <summary>
        /// Indicates if this node is a direct search result.
        /// </summary>
        public bool IsSearchResult { get; set; }

        /// <summary>
        /// Indicates if this node or its descendents contain a search result.
        /// </summary>
        public bool ContainsSearchResult { get; set; }

        public abstract void ResetSearchResultStatus();
    }

    /// <summary>
    /// Fake abstract class representing a tree node.
    /// </summary>
    public abstract class TreeNode : BaseNode
    {
        /// <summary>
        /// Gets the child nodes.
        /// </summary>
        public abstract IList<BaseNode> Children { get; }

        /// <summary>
        /// Indicates if the node has any children.
        /// </summary>
        public abstract bool HasChildren { get; }

        /// <summary>
        /// Simulates evaluating the node against a query matcher.
        /// Returns a SearchResult if the node matches; otherwise, null.
        /// </summary>
        /// <param name="matcher">The query matcher.</param>
        /// <returns>A SearchResult if matched; otherwise, null.</returns>
        public virtual SearchResult EvaluateMatch(NodeQueryMatcher matcher)
        {
            return null;
        }

        public override void ResetSearchResultStatus()
        {
            IsSearchResult = false;
            ContainsSearchResult = false;
        }
    }

    /// <summary>
    /// Fake implementation of NodeQueryMatcher for testing purposes.
    /// </summary>
    public class NodeQueryMatcher
    {
        public bool HasTimeIntervalConstraints { get; set; }
        public TimeSpan PrecalculationDuration { get; private set; }
        private readonly string _query;

        public NodeQueryMatcher(string query)
        {
            _query = query;
            // For testing, assume no time interval constraints by default.
            HasTimeIntervalConstraints = false;
        }

        public void Initialize(IEnumerable<string> strings, CancellationToken cancellationToken)
        {
            // Simulate some precalculation delay.
            var start = DateTime.Now;
            // For testing, do nothing.
            PrecalculationDuration = DateTime.Now - start;
        }

        /// <summary>
        /// Simulates matching a node. It calls the node's EvaluateMatch method.
        /// </summary>
        /// <param name="node">The node to evaluate.</param>
        /// <returns>A SearchResult if the node matches; otherwise, null.</returns>
        public SearchResult IsMatch(BaseNode node)
        {
            if (node is TreeNode treeNode)
            {
                return treeNode.EvaluateMatch(this);
            }
            return null;
        }

        public bool IsTimeIntervalMatch(BaseNode node)
        {
            // For testing, simply return true.
            return true;
        }
    }

    /// <summary>
    /// Fake implementation of SearchResult to simulate search outcomes.
    /// </summary>
    public class SearchResult
    {
        public string Result { get; }

        public SearchResult(string result)
        {
            Result = result;
        }

        public override bool Equals(object obj)
        {
            if (obj is SearchResult other)
            {
                return Result == other.Result;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return Result != null ? Result.GetHashCode() : 0;
        }
    }

    /// <summary>
    /// Fake static class to simulate PlatformUtilities.
    /// </summary>
    public static class PlatformUtilities
    {
        // For testing, we assume multithreading is enabled.
        public static bool HasThreads { get; } = true;
    }
    #endregion
}

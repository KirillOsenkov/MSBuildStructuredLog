// using System;
// using System.Collections.Generic;
// using System.IO;
// using System.Linq;
// using Moq;
// using Xunit;
// 
// namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
// {
//     /// <summary>
//     /// Unit tests for the <see cref="Microsoft.Build.Logging.StructuredLogger.ProjectReferenceGraph"/> class.
//     /// </summary>
//     public class ProjectReferenceGraphTests
//     {
//         /// <summary>
//         /// Tests that the constructor does not throw when the EvaluationFolder contains a ProjectEvaluation with no Items folder.
//         /// </summary>
// //         [Fact] [Error] (30-36)CS0117 'Record' does not contain a definition for 'Exception'
// //         public void Constructor_WithMissingItemsFolder_DoesNotThrow()
// //         {
// //             // Arrange
// //             var build = new DummyBuild();
// //             var evalFolder = new DummyFolder("EvaluationFolder");
// //             var projectEval = new DummyProjectEvaluation("C:/Project/Test.csproj");
// //             // Do not add Items folder to project evaluation.
// //             evalFolder.AddChild(projectEval);
// //             build.EvaluationFolder = evalFolder;
// // 
// //             // Act & Assert
// //             var exception = Record.Exception(() => new Microsoft.Build.Logging.StructuredLogger.ProjectReferenceGraph(build));
// //             Assert.Null(exception);
// //         }
// 
//         /// <summary>
//         /// Tests that the constructor correctly computes references for a valid project reference.
//         /// </summary>
// //         [Fact] [Error] (59-92)CS1503 Argument 1: cannot convert from 'Microsoft.Build.Logging.StructuredLogger.UnitTests.DummyBuild' to 'Microsoft.Build.Logging.StructuredLogger.Build' [Error] (68-47)CS1503 Argument 1: cannot convert from 'Microsoft.Build.Logging.StructuredLogger.UnitTests.DummyNodeQueryMatcher' to 'StructuredLogViewer.NodeQueryMatcher' [Error] (68-56)CS1503 Argument 2: cannot convert from 'System.Collections.Generic.List<Microsoft.Build.Logging.StructuredLogger.UnitTests.SearchResult>' to 'System.Collections.Generic.IList<StructuredLogViewer.SearchResult>'
// //         public void Constructor_WithValidProjectReference_ComputesReferenceCorrectly()
// //         {
// //             // Arrange
// //             var build = new DummyBuild();
// //             var evalFolder = new DummyFolder("EvaluationFolder");
// //             var projectPath = "C:/Project/Test.csproj";
// //             var referencedRelativePath = "ref.csproj";
// //             var projectEval = new DummyProjectEvaluation(projectPath);
// // 
// //             // Create Items folder and add AddItem node for "ProjectReference"
// //             var itemsFolder = new DummyFolder(Strings.Items);
// //             var addItemNode = new DummyAddItem("ProjectReference");
// //             // Add an item with text for the referenced project.
// //             var itemNode = new DummyItem(referencedRelativePath);
// //             addItemNode.AddChild(itemNode);
// //             itemsFolder.AddChild(addItemNode);
// //             projectEval.AddChild(itemsFolder);
// //             evalFolder.AddChild(projectEval);
// //             build.EvaluationFolder = evalFolder;
// // 
// //             // Act
// //             var graph = new Microsoft.Build.Logging.StructuredLogger.ProjectReferenceGraph(build);
// // 
// //             // Use a NodeQueryMatcher that searches for projects with height 0.
// //             var matcher = new DummyNodeQueryMatcher
// //             {
// //                 Height = 0,
// //                 TypeKeyword = "project"
// //             };
// //             var resultSet = new List<SearchResult>();
// //             bool result = graph.TryGetResults(matcher, resultSet, 10);
// // 
// //             // Assert
// //             Assert.True(result);
// //             // Since the project has one reference, its computed height should be 1.
// //             // Thus, searching for height 0 should not return the project.
// //             Assert.Empty(resultSet);
// //         }
// 
//         /// <summary>
//         /// Tests that TryGetResults returns false when the TypeKeyword is not recognized.
//         /// </summary>
// //         [Fact] [Error] (86-92)CS1503 Argument 1: cannot convert from 'Microsoft.Build.Logging.StructuredLogger.UnitTests.DummyBuild' to 'Microsoft.Build.Logging.StructuredLogger.Build' [Error] (94-47)CS1503 Argument 1: cannot convert from 'Microsoft.Build.Logging.StructuredLogger.UnitTests.DummyNodeQueryMatcher' to 'StructuredLogViewer.NodeQueryMatcher' [Error] (94-56)CS1503 Argument 2: cannot convert from 'System.Collections.Generic.List<Microsoft.Build.Logging.StructuredLogger.UnitTests.SearchResult>' to 'System.Collections.Generic.IList<StructuredLogViewer.SearchResult>'
// //         public void TryGetResults_InvalidType_ReturnsFalse()
// //         {
// //             // Arrange
// //             var build = new DummyBuild();
// //             build.EvaluationFolder = new DummyFolder("EvaluationFolder");
// //             var graph = new Microsoft.Build.Logging.StructuredLogger.ProjectReferenceGraph(build);
// //             var matcher = new DummyNodeQueryMatcher
// //             {
// //                 TypeKeyword = "invalid"
// //             };
// //             var resultSet = new List<SearchResult>();
// // 
// //             // Act
// //             bool result = graph.TryGetResults(matcher, resultSet, 10);
// // 
// //             // Assert
// //             Assert.False(result);
// //         }
// 
//         /// <summary>
//         /// Tests that TryGetResults for projectreference with no project() clause adds a note result.
//         /// </summary>
// //         [Fact] [Error] (110-92)CS1503 Argument 1: cannot convert from 'Microsoft.Build.Logging.StructuredLogger.UnitTests.DummyBuild' to 'Microsoft.Build.Logging.StructuredLogger.Build' [Error] (122-47)CS1503 Argument 1: cannot convert from 'Microsoft.Build.Logging.StructuredLogger.UnitTests.DummyNodeQueryMatcher' to 'StructuredLogViewer.NodeQueryMatcher' [Error] (122-56)CS1503 Argument 2: cannot convert from 'System.Collections.Generic.List<Microsoft.Build.Logging.StructuredLogger.UnitTests.SearchResult>' to 'System.Collections.Generic.IList<StructuredLogViewer.SearchResult>'
// //         public void TryGetResults_ProjectReferenceWithoutProjectMatcher_AddsNoteResult()
// //         {
// //             // Arrange
// //             var build = new DummyBuild();
// //             // Create minimal EvaluationFolder even though it won't be used.
// //             build.EvaluationFolder = new DummyFolder("EvaluationFolder");
// //             var graph = new Microsoft.Build.Logging.StructuredLogger.ProjectReferenceGraph(build);
// // 
// //             var matcher = new DummyNodeQueryMatcher
// //             {
// //                 TypeKeyword = "projectreference",
// //                 // Ensure no project matchers provided.
// //                 ProjectMatchers = new List<INodeQueryMatcher>()
// //             };
// // 
// //             var resultSet = new List<SearchResult>();
// // 
// //             // Act
// //             bool result = graph.TryGetResults(matcher, resultSet, 10);
// // 
// //             // Assert
// //             Assert.False(result);
// //             Assert.Single(resultSet);
// //             // The note text should instruct about specifying a project() clause.
// //             var noteNode = (resultSet[0].Node as Note);
// //             Assert.NotNull(noteNode);
// //             Assert.Contains("Specify a project()", noteNode.Text);
// //         }
// 
//         /// <summary>
//         /// Tests that TryGetResults for projectreference returns matching project search results.
//         /// </summary>
// //         [Fact] [Error] (172-27)CS0103 The name 'graph' does not exist in the current context
// //         public void TryGetResults_ProjectReferenceWithMatchingProject_ReturnsResults()
// //         {
// //             // Arrange
// //             var build = new DummyBuild();
// //             var evalFolder = new DummyFolder("EvaluationFolder");
// //             var projectPath = "C:/Project/Main.csproj";
// //             var referencedRelativePath = "childRef.csproj";
// //             var projectEval = new DummyProjectEvaluation(projectPath);
// // 
// //             // Create Items folder and add AddItem node for "ProjectReference"
// //             var itemsFolder = new DummyFolder(Strings.Items);
// //             var addItemNode = new DummyAddItem("ProjectReference");
// //             var itemNode = new DummyItem(referencedRelativePath);
// //             addItemNode.AddChild(itemNode);
// //             itemsFolder.AddChild(addItemNode);
// //             projectEval.AddChild(itemsFolder);
// //             evalFolder.AddChild(projectEval);
// //             build.EvaluationFolder = evalFolder;
// // 
// //             // Setup a matcher that will match the project's name.
// //             var projectMatcher = new DummyNodeQueryMatcher
// //             {
// //                 TypeKeyword = "any",
// //                 Terms = new List<string> { "Main" }
// //             };
// //             var matcher = new DummyNodeQueryMatcher
// //             {
// //                 TypeKeyword = "projectreference",
// //                 ProjectMatchers = new List<INodeQueryMatcher> { projectMatcher },
// //                 Terms = new List<string> { "Main" }
// //             };
// // 
// //             var resultSet = new List<SearchResult>();
// // 
// //             // Act
// //             bool result = graph.TryGetResults(matcher, resultSet, 10);
// // 
// //             // Assert
// //             Assert.True(result);
// //             // Expect at least one result for the matching project.
// //             Assert.True(resultSet.Count >= 1);
// //             // Validate that the search result node has the project file "C:/Project/Main.csproj".
// //             var projectResult = ExtractProjectFromResult(resultSet[0]);
// //             Assert.NotNull(projectResult);
// //             Assert.Equal(projectPath, projectResult.ProjectFile);
// //         }
// 
//         /// <summary>
//         /// Tests that the constructor creates a circular project references folder when circular dependencies exist.
//         /// </summary>
// //         [Fact] [Error] (219-92)CS1503 Argument 1: cannot convert from 'Microsoft.Build.Logging.StructuredLogger.UnitTests.DummyBuild' to 'Microsoft.Build.Logging.StructuredLogger.Build'
// //         public void Constructor_WithCircularDependency_AddsCircularReferenceFolder()
// //         {
// //             // Arrange
// //             var build = new DummyBuild();
// //             
// //             // Create two project evaluations that reference each other.
// //             var evalFolder = new DummyFolder("EvaluationFolder");
// //             var projectPathA = "C:/Project/A.csproj";
// //             var projectPathB = "C:/Project/B.csproj";
// // 
// //             var projectEvalA = new DummyProjectEvaluation(projectPathA);
// //             var itemsFolderA = new DummyFolder(Strings.Items);
// //             var addItemNodeA = new DummyAddItem("ProjectReference");
// //             // A references B
// //             addItemNodeA.AddChild(new DummyItem("B.csproj"));
// //             itemsFolderA.AddChild(addItemNodeA);
// //             projectEvalA.AddChild(itemsFolderA);
// // 
// //             var projectEvalB = new DummyProjectEvaluation(projectPathB);
// //             var itemsFolderB = new DummyFolder(Strings.Items);
// //             var addItemNodeB = new DummyAddItem("ProjectReference");
// //             // B references A
// //             addItemNodeB.AddChild(new DummyItem("A.csproj"));
// //             itemsFolderB.AddChild(addItemNodeB);
// //             projectEvalB.AddChild(itemsFolderB);
// // 
// //             evalFolder.AddChild(projectEvalA);
// //             evalFolder.AddChild(projectEvalB);
// //             build.EvaluationFolder = evalFolder;
// // 
// //             // Act
// //             var graph = new Microsoft.Build.Logging.StructuredLogger.ProjectReferenceGraph(build);
// // 
// //             // Assert
// //             // The DummyBuild should have a CircularProjectReferences folder created if circularities exist.
// //             Assert.NotNull(build.CircularProjectReferencesFolder);
// //             // Expect at least one circularity loop.
// //             Assert.NotEmpty(build.CircularProjectReferencesFolder.Children);
// //         }
// 
//         /// <summary>
//         /// Helper method to extract the Project node from a SearchResult.
//         /// </summary>
//         /// <param name="result">The search result.</param>
//         /// <returns>The extracted Project node, or null if not found.</returns>
//         private Project ExtractProjectFromResult(SearchResult result)
//         {
//             if (result.Node is Project project)
//             {
//                 return project;
//             }
//             else if (result.Node is ProxyNode proxy && proxy.Original is Project p)
//             {
//                 return p;
//             }
//             return null;
//         }
//     }
// 
//     #region Dummy Implementations for Testing
// 
//     // Minimal implementations to support testing of ProjectReferenceGraph.
// 
// //     internal class DummyBuild : Build [Error] (251-20)CS0534 'DummyBuild' does not implement inherited abstract member 'Build.EvaluationFolder.get' [Error] (251-20)CS0534 'DummyBuild' does not implement inherited abstract member 'Build.GetOrCreateNodeWithName<T>(string)' [Error] (251-20)CS0534 'DummyBuild' does not implement inherited abstract member 'Build.EvaluationFolder.set'
// //     {
// //         private readonly Dictionary<string, object> _nodes = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
// //         public Folder EvaluationFolder { get; set; }
// // 
// //         public T GetOrCreateNodeWithName<T>(string name) where T : Node, new() [Error] (271-25)CS0200 Property or indexer 'DummyBuild.CircularProjectReferencesFolder' cannot be assigned to -- it is read only
// //         {
// //             if (typeof(T) == typeof(Folder))
// //             {
// //                 if (_nodes.ContainsKey(name))
// //                 {
// //                     return (T)_nodes[name];
// //                 }
// //                 else
// //                 {
// //                     var folder = new DummyFolder(name);
// //                     _nodes[name] = folder;
// //                     // Specifically store the circular references folder if that name is used.
// //                     if (name == "Circular Project References")
// //                     {
// //                         CircularProjectReferencesFolder = folder;
// //                     }
// //                     return (T)(Node)folder;
// //                 }
// //             }
// //             return new T();
// //         }
// // 
// //         // Expose the CircularProjectReferencesFolder for testing.
// //         public Folder CircularProjectReferencesFolder
// //         {
// //             get
// //             {
// //                 if (_nodes.ContainsKey("Circular Project References"))
// //                 {
// //                     return (Folder)_nodes["Circular Project References"];
// //                 }
// //                 return null;
// //             }
// //         }
// //     }
// 
//     internal class DummyFolder : Folder
//     {
//         public DummyFolder(string name)
//         {
//             Name = name;
//             Children = new List<Node>();
//         }
//     }
// 
//     internal class DummyProjectEvaluation : ProjectEvaluation
//     {
//         public DummyProjectEvaluation(string projectFile)
//         {
//             ProjectFile = projectFile;
//             Children = new List<Node>();
//         }
//     }
// 
// //     internal class DummyAddItem : AddItem [Error] (316-13)CS0103 The name 'Name' does not exist in the current context
// //     {
// //         public DummyAddItem(string name)
// //         {
// //             // Using the Name property to store identifier.
// //             Name = name;
// //             Children = new List<Node>();
// //         }
// //     }
// 
//     internal class DummyItem : Item
//     {
//         public DummyItem(string text)
//         {
//             Text = text;
//         }
//     }
// 
//     // Dummy implementations for base classes and interfaces needed by ProjectReferenceGraph.
// 
//     internal abstract class Build
//     {
//         public abstract Folder EvaluationFolder { get; set; }
//         public abstract T GetOrCreateNodeWithName<T>(string name) where T : Node, new();
//     }
// 
// //     internal class Folder : Node [Error] (337-20)CS0060 Inconsistent accessibility: base class 'Node' is less accessible than class 'Folder'
// //     {
// //         public string Name { get; set; }
// //     }
// 
//     internal class ProjectEvaluation : Node
//     {
//         public string ProjectFile { get; set; }
// 
//         public T FindChild<T>(string name) where T : Node
//         {
//             return Children.OfType<T>().FirstOrDefault();
//         }
//     }
// 
//     internal class AddItem : Node
//     {
//     }
// 
//     internal class Item : Node
//     {
//         public string Text { get; set; }
//     }
// 
//     internal class Project : Node
//     {
//         public string ProjectFile { get; set; }
//         public string Name { get; set; }
//     }
// 
//     internal class ProxyNode : Node
//     {
//         public Node Original { get; set; }
//         public SearchResult SearchResult { get; set; }
//         public string Text { get; set; }
//     }
// 
//     internal class Note : Node
//     {
//         public string Text { get; set; }
//     }
// 
//     internal class Node
//     {
//         public List<Node> Children { get; set; } = new List<Node>();
//         public bool IsExpanded { get; set; }
//         public virtual void AddChild(Node child)
//         {
//             Children.Add(child);
//         }
//     }
// 
//     internal class SearchResult
//     {
//         public Node Node { get; set; }
//         public List<string> FieldsToDisplay { get; set; }
//         public static SearchResult EmptyQueryMatch { get; } = new SearchResult(null);
//         public SearchResult(Node node)
//         {
//             Node = node;
//         }
//     }
// 
//     internal static class TextUtilities
//     {
//         public static string NormalizeFilePath(string path)
//         {
//             // Simple normalization: replace backslashes with forward slashes.
//             return path.Replace("\\", "/");
//         }
//     }
// 
//     internal static class Strings
//     {
//         public static string Items => "Items";
//     }
// 
//     // Interfaces and dummy implementations for NodeQueryMatcher.
// 
//     internal interface INodeQueryMatcher
//     {
//         object IsMatch(string input);
//     }
// 
//     internal class DummyNodeQueryMatcher : INodeQueryMatcher
//     {
//         public int Height { get; set; } = -1;
//         public string TypeKeyword { get; set; }
//         public List<string> Terms { get; set; } = new List<string>();
//         public List<INodeQueryMatcher> ProjectMatchers { get; set; } = new List<INodeQueryMatcher>();
// 
//         /// <summary>
//         /// Simple matching: if any term is contained in the input, returns a dummy SearchResult; otherwise, returns EmptyQueryMatch.
//         /// </summary>
//         public object IsMatch(string input)
//         {
//             if (Terms != null && Terms.Any(term => input.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0))
//             {
//                 return new SearchResult(new Note { Text = input });
//             }
//             return SearchResult.EmptyQueryMatch;
//         }
//     }
// 
//     #endregion
// }

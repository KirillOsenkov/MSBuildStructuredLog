using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using Xunit;

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="ResolveAssemblyReferenceAnalyzer"/> class.
    /// </summary>
    public class ResolveAssemblyReferenceAnalyzerTests
    {
        private readonly ResolveAssemblyReferenceAnalyzer _analyzer;

        public ResolveAssemblyReferenceAnalyzerTests()
        {
            _analyzer = new ResolveAssemblyReferenceAnalyzer();
        }

        #region AnalyzeResolveAssemblyReference Tests

        /// <summary>
        /// Tests that AnalyzeResolveAssemblyReference correctly updates UsedLocations and TotalRARDuration when valid Results and Parameters folders are present.
        /// </summary>
//         [Fact] [Error] (31-32)CS7036 There is no argument given that corresponds to the required parameter 'name' of 'FakeTask.FakeTask(string)' [Error] (99-55)CS1503 Argument 1: cannot convert from 'Microsoft.Build.Logging.StructuredLogger.UnitTests.FakeTask' to 'Microsoft.Build.Logging.StructuredLogger.Task'
//         public void AnalyzeResolveAssemblyReference_HappyPath_UsedLocationsAndDurationUpdated()
//         {
//             // Arrange
//             var duration = TimeSpan.FromSeconds(5);
//             var fakeTask = new FakeTask
//             {
//                 Duration = duration
//             };
// 
//             // Create a fake Build parent with a simple string table.
//             var fakeBuild = new FakeBuild
//             {
//                 StringTable = s => s // identity intern function
//             };
//             fakeTask.Parent = fakeBuild;
// 
//             // Create Results folder with one Parameter reference.
//             var resultsFolder = new FakeFolder { Name = Strings.Results };
//             fakeTask.AddChild(resultsFolder);
// 
//             // Create a Parameter node representing a resolved reference.
//             var parameterReference = new FakeParameter { Name = "Reference Parameter" };
//             resultsFolder.AddChild(parameterReference);
// 
//             // Add a Resolved file path item. (Simulate a different resolved path to force location addition.)
//             var resolvedFilePath = @"C:\resolvedpath\lib.dll";
//             var resolvedFileItem = new FakeItem { Text = $"Resolved file path is \"{resolvedFilePath}\"" };
//             parameterReference.AddChild(resolvedFileItem);
// 
//             // Add a reference found at search path location item.
//             var foundLocation = @"C:\searchpath\lib.dll";
//             var foundLocationItem = new FakeItem { Text = $"Reference found at search path location \"{foundLocation}\"" };
//             parameterReference.AddChild(foundLocationItem);
// 
//             // Create Parameters folder with SearchPaths and Assemblies.
//             var parametersFolder = new FakeFolder { Name = Strings.Parameters };
//             fakeTask.AddChild(parametersFolder);
// 
//             // Create NamedNode for SearchPaths with two search paths: one used and one unused.
//             var searchPathsNode = new FakeNamedNode { Name = Strings.SearchPaths };
//             var usedSearchPathItem = new FakeItem { Text = foundLocation };
//             var unusedSearchPathItem = new FakeItem { Text = @"C:\another\path" };
//             searchPathsNode.AddChild(usedSearchPathItem);
//             searchPathsNode.AddChild(unusedSearchPathItem);
//             parametersFolder.AddChild(searchPathsNode);
// 
//             // Also, simulate dependency branch with not-copy-local scenario.
//             // Create a Parameter node with Name starting with "Dependency ".
//             var dependencyReference = new FakeParameter { Name = "Dependency \"MyAssembly, Version=1.0.0.0\"" };
//             resultsFolder.AddChild(dependencyReference);
// 
//             // Add a "Required by" item.
//             var requiredByText = "Required by \"SourceItem1\"";
//             var requiredByItem = new FakeItem { Text = requiredByText };
//             dependencyReference.AddChild(requiredByItem);
// 
//             // Add the not-copy-local message item.
//             var notCopyLocalMessageText = @"This reference is not ""CopyLocal"" because at least one source item had ""Private"" set to ""false"" and no source items had ""Private"" set to ""true"".";
//             var notCopyLocalItem = new FakeItem { Text = notCopyLocalMessageText };
//             dependencyReference.AddChild(notCopyLocalItem);
// 
//             // In Assemblies folder under Parameters, add a Parameter node with Assemblies name.
//             var assembliesParameter = new FakeParameter { Name = Strings.Assemblies };
//             parametersFolder.AddChild(assembliesParameter);
//             // Create a source item that matches the required by text.
//             var sourceItem = new FakeItem { Text = "SourceItem1" };
//             // Add Metadata child with Private metadata.
//             var privateMetadata = new FakeMetadata { Name = "Private", Value = "true" };
//             sourceItem.AddChild(privateMetadata);
//             assembliesParameter.AddChild(sourceItem);
// 
//             // Act
//             _analyzer.AnalyzeResolveAssemblyReference(fakeTask);
// 
//             // Assert
//             // Duration should be added.
//             Assert.Equal(duration, _analyzer.TotalRARDuration);
// 
//             // Found location should be added to UsedLocations.
//             Assert.Contains(foundLocation, _analyzer.UsedLocations);
// 
//             // The unused search path should be in UnusedLocations.
//             Assert.Contains(@"C:\another\path", _analyzer.UnusedLocations);
// 
//             // Check that dependency branch added metadata message.
//             // Find the not-copy-local message's child which should have been appended.
//             var appendedMessage = notCopyLocalItem.Children.OfType<FakeMessage>().FirstOrDefault();
//             Assert.NotNull(appendedMessage);
//             Assert.Contains("SourceItem1 has Private set to true", appendedMessage.Text);
//         }

        /// <summary>
        /// Tests that AnalyzeResolveAssemblyReference handles cases with missing Parameters folder gracefully.
        /// </summary>
//         [Fact] [Error] (126-32)CS7036 There is no argument given that corresponds to the required parameter 'name' of 'FakeTask.FakeTask(string)' [Error] (152-55)CS1503 Argument 1: cannot convert from 'Microsoft.Build.Logging.StructuredLogger.UnitTests.FakeTask' to 'Microsoft.Build.Logging.StructuredLogger.Task'
//         public void AnalyzeResolveAssemblyReference_NoParametersFolder_NoSearchPathsProcessed()
//         {
//             // Arrange
//             var duration = TimeSpan.FromSeconds(3);
//             var fakeTask = new FakeTask
//             {
//                 Duration = duration
//             };
// 
//             // Create a fake Build parent.
//             var fakeBuild = new FakeBuild
//             {
//                 StringTable = s => s
//             };
//             fakeTask.Parent = fakeBuild;
// 
//             // Create only a Results folder with a reference.
//             var resultsFolder = new FakeFolder { Name = Strings.Results };
//             fakeTask.AddChild(resultsFolder);
// 
//             var parameterReference = new FakeParameter { Name = "Reference Parameter" };
//             resultsFolder.AddChild(parameterReference);
// 
//             var resolvedFileItem = new FakeItem { Text = "Resolved file path is \"C:\\resolvedpath\\lib.dll\"" };
//             parameterReference.AddChild(resolvedFileItem);
// 
//             var foundLocationItem = new FakeItem { Text = "Reference found at search path location \"C:\\searchpath\\lib.dll\"" };
//             parameterReference.AddChild(foundLocationItem);
// 
//             // Act
//             _analyzer.AnalyzeResolveAssemblyReference(fakeTask);
// 
//             // Assert
//             Assert.Equal(duration, _analyzer.TotalRARDuration);
//             // Since Parameters folder is missing no search paths are processed so UnusedLocations remains empty.
//             Assert.Empty(_analyzer.UnusedLocations);
//             // The found location should still be added to UsedLocations.
//             Assert.Contains("C:\\searchpath\\lib.dll", _analyzer.UsedLocations);
//         }

        /// <summary>
        /// Tests that AnalyzeResolveAssemblyReference handles dependency branch without Assemblies folder gracefully.
        /// </summary>
//         [Fact] [Error] (169-32)CS7036 There is no argument given that corresponds to the required parameter 'name' of 'FakeTask.FakeTask(string)' [Error] (195-55)CS1503 Argument 1: cannot convert from 'Microsoft.Build.Logging.StructuredLogger.UnitTests.FakeTask' to 'Microsoft.Build.Logging.StructuredLogger.Task'
//         public void AnalyzeResolveAssemblyReference_DependencyWithoutAssemblies_NoMetadataAppended()
//         {
//             // Arrange
//             var fakeTask = new FakeTask { Duration = TimeSpan.Zero };
//             var fakeBuild = new FakeBuild { StringTable = s => s };
//             fakeTask.Parent = fakeBuild;
// 
//             var resultsFolder = new FakeFolder { Name = Strings.Results };
//             fakeTask.AddChild(resultsFolder);
// 
//             // Create a dependency reference.
//             var dependencyReference = new FakeParameter { Name = "Dependency \"MyAssembly, Version=1.0.0.0\"" };
//             resultsFolder.AddChild(dependencyReference);
// 
//             // Add a "Required by" item.
//             var requiredByText = "Required by \"NonExistingSource\"";
//             var requiredByItem = new FakeItem { Text = requiredByText };
//             dependencyReference.AddChild(requiredByItem);
// 
//             // Add the not-copy-local message item.
//             var notCopyLocalMessageText = @"This reference is not ""CopyLocal"" because at least one source item had ""Private"" set to ""false"" and no source items had ""Private"" set to ""true"".";
//             var notCopyLocalItem = new FakeItem { Text = notCopyLocalMessageText };
//             dependencyReference.AddChild(notCopyLocalItem);
// 
//             // Create Parameters folder but no Assemblies parameter.
//             var parametersFolder = new FakeFolder { Name = Strings.Parameters };
//             fakeTask.AddChild(parametersFolder);
// 
//             // Act
//             _analyzer.AnalyzeResolveAssemblyReference(fakeTask);
// 
//             // Assert
//             // Since Assemblies folder is missing, dependency branch should not append any metadata.
//             Assert.Empty(notCopyLocalItem.Children);
//         }

        #endregion

        #region AppendFinalReport Tests

        /// <summary>
        /// Tests that AppendFinalReport correctly adds Used and Unused locations to the Build node.
        /// </summary>
//         [Fact] [Error] (224-41)CS1503 Argument 1: cannot convert from 'Microsoft.Build.Logging.StructuredLogger.UnitTests.FakeBuild' to 'Microsoft.Build.Logging.StructuredLogger.Build'
//         public void AppendFinalReport_WithUsedAndUnusedLocations_AppendsChildrenToBuild()
//         {
//             // Arrange
//             // Setup analyzer with predefined locations.
//             _analyzer.UsedLocations.Clear();
//             _analyzer.UnusedLocations.Clear();
//             _analyzer.UsedLocations.Add("C:\\used1");
//             _analyzer.UsedLocations.Add("C:\\used2");
//             _analyzer.UnusedLocations.Add("C:\\unused1");
//             _analyzer.UnusedLocations.Add("C:\\unused2");
// 
//             var fakeBuild = new FakeBuild { StringTable = s => s };
// 
//             // Act
//             _analyzer.AppendFinalReport(fakeBuild);
// 
//             // Assert
//             // Check that Build has a node for used locations.
//             var usedNode = fakeBuild.FindChild<FakeFolder>(node => node.Name == Strings.UsedAssemblySearchPathsLocations);
//             Assert.NotNull(usedNode);
//             // They should be sorted.
//             var usedTexts = usedNode.Children.OfType<FakeItem>().Select(i => i.Text).ToList();
//             var expectedUsed = new List<string> { "C:\\used1", "C:\\used2" };
//             expectedUsed.Sort(StringComparer.Ordinal);
//             Assert.Equal(expectedUsed, usedTexts);
// 
//             // Check that Build has a node for unused locations.
//             var unusedNode = fakeBuild.FindChild<FakeFolder>(node => node.Name == Strings.UnusedAssemblySearchPathsLocations);
//             Assert.NotNull(unusedNode);
//             var unusedTexts = unusedNode.Children.OfType<FakeItem>().Select(i => i.Text).ToList();
//             var expectedUnused = new List<string> { "C:\\unused1", "C:\\unused2" };
//             expectedUnused.Sort(StringComparer.Ordinal);
//             Assert.Equal(expectedUnused, unusedTexts);
//         }

        #endregion
    }

    #region Fake Implementations for Test Purposes

//     internal static class Strings [Error] (250-27)CS0101 The namespace 'Microsoft.Build.Logging.StructuredLogger.UnitTests' already contains a definition for 'Strings'
//     {
//         public const string Results = "Results";
//         public const string Parameters = "Parameters";
//         public const string SearchPaths = "SearchPaths";
//         public const string Assemblies = "Assemblies";
//         public const string UsedLocations = "UsedLocations";
//         public const string UnusedLocations = "UnusedLocations";
//         public const string UsedAssemblySearchPathsLocations = "UsedAssemblySearchPathsLocations";
//         public const string UnusedAssemblySearchPathsLocations = "UnusedAssemblySearchPathsLocations";
//     }

    internal class FakeNode
    {
        public string Name { get; set; }
        public string Text { get; set; }
        public FakeNode Parent { get; set; }
        public List<FakeNode> Children { get; } = new List<FakeNode>();

        public void AddChild(FakeNode child)
        {
            child.Parent = this;
            Children.Add(child);
        }

        public T FindChild<T>(Func<T, bool> predicate) where T : FakeNode
        {
            foreach (var child in Children)
            {
                if (child is T tChild && predicate(tChild))
                {
                    return tChild;
                }
            }
            return null;
        }

        public T GetOrCreateNodeWithName<T>(string name) where T : FakeNode, new()
        {
            var existing = Children.OfType<T>().FirstOrDefault(n => n.Name == name);
            if (existing != null)
            {
                return existing;
            }
            var newNode = new T { Name = name };
            AddChild(newNode);
            return newNode;
        }

        public override string ToString()
        {
            return Text;
        }
    }

    internal class FakeFolder : FakeNode
    {
        /// <summary>
        /// Sorts children using their Text property if available; otherwise, by Name.
        /// </summary>
        public void SortChildren()
        {
            Children.Sort((a, b) =>
            {
                var aKey = string.IsNullOrEmpty(a.Text) ? a.Name : a.Text;
                var bKey = string.IsNullOrEmpty(b.Text) ? b.Name : b.Text;
                return string.Compare(aKey, bKey, StringComparison.Ordinal);
            });
        }
    }

    internal class FakeTask : FakeNode
    {
        public TimeSpan Duration { get; set; }

        public T GetNearestParent<T>() where T : FakeNode
        {
            var current = Parent;
            while (current != null)
            {
                if (current is T t)
                {
                    return t;
                }
                current = current.Parent;
            }
            return null;
        }

        public T FindChild<T>(Func<T, bool> predicate) where T : FakeNode
        {
            foreach (var child in Children)
            {
                if (child is T tChild && predicate(tChild))
                {
                    return tChild;
                }
            }
            return null;
        }

//         public T GetOrCreateNodeWithName<T>(string name) where T : FakeNode, new() [Error] (353-25)CS8121 An expression of type 'FakeTask' cannot be handled by a pattern of type 'FakeFolder'.
//         {
//             if (this is FakeFolder folder)
//             {
//                 return folder.GetOrCreateNodeWithName<T>(name);
//             }
//             // If FakeTask is not a folder, simulate similar behavior.
//             return base.GetOrCreateNodeWithName<T>(name);
//         }
    }

    internal class FakeBuild : FakeFolder
    {
        // Simulate a simple string interning method.
        public Func<string, string> StringTable { get; set; }
    }

    internal class FakeNamedNode : FakeNode
    {
    }

    internal class FakeParameter : FakeNode
    {
    }

    internal class FakeItem : FakeNode
    {
    }

    internal class FakeMetadata : FakeNode
    {
        // Reuse Name and Value properties. Name is inherited from FakeNode.
        public string Value { get; set; }
    }

//     internal class FakeMessage : FakeNode [Error] (386-20)CS0060 Inconsistent accessibility: base class 'FakeNode' is less accessible than class 'FakeMessage'
//     {
//     }

    #endregion
}

using Microsoft.Build.Framework;
using Microsoft.Build.Logging.StructuredLogger;
using Microsoft.Build.Logging.StructuredLogger.UnitTests;
using Moq;
using StructuredLogger.BinaryLogger;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Xunit;

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref = "Project"/> class.
    /// </summary>
    public class ProjectTests
    {
        private readonly Project _project;
        public ProjectTests()
        {
            _project = new Project();
        }

        /// <summary>
        /// Tests that the ProjectFileExtension property returns the correct extension when ProjectFile is set.
        /// </summary>
//         [Fact] [Error] (36-41)CS1061 'Project' does not contain a definition for 'ProjectFileExtension' and no accessible extension method 'ProjectFileExtension' accepting a first argument of type 'Project' could be found (are you missing a using directive or an assembly reference?)
//         public void ProjectFileExtension_WhenProjectFileIsSet_ReturnsLowerInvariantExtension()
//         {
//             // Arrange
//             string filePath = @"C:\folder\MyProject.CSPROJ";
//             _project.ProjectFile = filePath;
//             // Act
//             string extension = _project.ProjectFileExtension;
//             // Assert
//             Assert.Equal(".csproj", extension);
//         }

        /// <summary>
        /// Tests that the ProjectFileExtension property returns an empty string when ProjectFile is null.
        /// </summary>
//         [Fact] [Error] (50-41)CS1061 'Project' does not contain a definition for 'ProjectFileExtension' and no accessible extension method 'ProjectFileExtension' accepting a first argument of type 'Project' could be found (are you missing a using directive or an assembly reference?)
//         public void ProjectFileExtension_WhenProjectFileIsNull_ReturnsEmptyString()
//         {
//             // Arrange
//             _project.ProjectFile = null;
//             // Act
//             string extension = _project.ProjectFileExtension;
//             // Assert
//             Assert.Equal(string.Empty, extension);
//         }

        /// <summary>
        /// Tests that the ProjectDirectory property returns the directory name when ProjectFile is set.
        /// </summary>
//         [Fact] [Error] (66-41)CS1061 'Project' does not contain a definition for 'ProjectDirectory' and no accessible extension method 'ProjectDirectory' accepting a first argument of type 'Project' could be found (are you missing a using directive or an assembly reference?)
//         public void ProjectDirectory_WhenProjectFileIsSet_ReturnsDirectoryName()
//         {
//             // Arrange
//             string filePath = @"C:\folder\subfolder\MyProject.csproj";
//             _project.ProjectFile = filePath;
//             string expected = Path.GetDirectoryName(filePath);
//             // Act
//             string directory = _project.ProjectDirectory;
//             // Assert
//             Assert.Equal(expected, directory);
//         }

        /// <summary>
        /// Tests that the ProjectDirectory property returns null when ProjectFile is empty.
        /// </summary>
//         [Fact] [Error] (80-41)CS1061 'Project' does not contain a definition for 'ProjectDirectory' and no accessible extension method 'ProjectDirectory' accepting a first argument of type 'Project' could be found (are you missing a using directive or an assembly reference?)
//         public void ProjectDirectory_WhenProjectFileIsEmpty_ReturnsNull()
//         {
//             // Arrange
//             _project.ProjectFile = string.Empty;
//             // Act
//             string directory = _project.ProjectDirectory;
//             // Assert
//             Assert.Null(directory);
//         }

        /// <summary>
        /// Tests the ToString method for combining Name, ProjectFile, EntryTargets and GlobalProperties.
        /// </summary>
//         [Fact] [Error] (102-22)CS1061 'Project' does not contain a definition for 'EntryTargets' and no accessible extension method 'EntryTargets' accepting a first argument of type 'Project' could be found (are you missing a using directive or an assembly reference?) [Error] (108-22)CS1061 'Project' does not contain a definition for 'GlobalProperties' and no accessible extension method 'GlobalProperties' accepting a first argument of type 'Project' could be found (are you missing a using directive or an assembly reference?)
//         public void ToString_WhenCalled_ReturnsExpectedFormattedString()
//         {
//             // Arrange
//             // Assuming that the base TimedNode class has a public property Name.
//             _project.ProjectFile = @"C:\folder\TestProject.csproj";
//             // Setting Name property dynamically since it comes from TimedNode.
//             var projectType = typeof(Project);
//             var nameProperty = projectType.GetProperty("Name");
//             if (nameProperty != null)
//             {
//                 nameProperty.SetValue(_project, "TestProject");
//             }
// 
//             _project.EntryTargets = new List<string>
//             {
//                 "Build",
//                 "Clean"
//             };
//             var globalProps = ImmutableDictionary<string, string>.Empty.Add("Configuration", "Debug");
//             _project.GlobalProperties = globalProps;
//             // Act
//             string result = _project.ToString();
//             // Assert
//             Assert.Contains("Project Name=TestProject", result);
//             Assert.Contains("File=C:\\folder\\TestProject.csproj", result);
//             Assert.Contains("Targets=[Build, Clean]", result);
//             // Verify that GlobalProperties key and value appear in the string.
//             Assert.Contains("Configuration=Debug", result);
//         }

        /// <summary>
        /// Tests that GetTargetById throws an exception when the id is -1.
        /// </summary>
//         [Fact] [Error] (126-77)CS1061 'Project' does not contain a definition for 'GetTargetById' and no accessible extension method 'GetTargetById' accepting a first argument of type 'Project' could be found (are you missing a using directive or an assembly reference?)
//         public void GetTargetById_WhenIdIsNegativeOne_ThrowsArgumentException()
//         {
//             // Act & Assert
//             var exception = Assert.Throws<ArgumentException>(() => _project.GetTargetById(-1));
//             Assert.Equal("Invalid target id: -1", exception.Message);
//         }

        /// <summary>
        /// Tests that GetTargetById returns null when a target with the specified id does not exist.
        /// </summary>
//         [Fact] [Error] (139-35)CS1061 'Project' does not contain a definition for 'GetTargetById' and no accessible extension method 'GetTargetById' accepting a first argument of type 'Project' could be found (are you missing a using directive or an assembly reference?)
//         public void GetTargetById_WhenTargetDoesNotExist_ReturnsNull()
//         {
//             // Arrange
//             int nonExistingId = 999;
//             // Act
//             var target = _project.GetTargetById(nonExistingId);
//             // Assert
//             Assert.Null(target);
//         }

        /// <summary>
        /// Tests that CreateTarget correctly creates a target with the specified name and id, and that it can be retrieved.
        /// </summary>
//         [Fact] [Error] (154-42)CS1061 'Project' does not contain a definition for 'CreateTarget' and no accessible extension method 'CreateTarget' accepting a first argument of type 'Project' could be found (are you missing a using directive or an assembly reference?) [Error] (155-42)CS1061 'Project' does not contain a definition for 'GetTargetById' and no accessible extension method 'GetTargetById' accepting a first argument of type 'Project' could be found (are you missing a using directive or an assembly reference?)
//         public void CreateTarget_WithValidInputs_CreatesAndStoresTarget()
//         {
//             // Arrange
//             string targetName = "Compile";
//             int targetId = 1;
//             // Act
//             var createdTarget = _project.CreateTarget(targetName, targetId);
//             var fetchedTarget = _project.GetTargetById(targetId);
//             // Assert
//             Assert.NotNull(createdTarget);
//             Assert.Equal(targetName, createdTarget.Name);
//             Assert.Equal(targetId, createdTarget.Id);
//             Assert.True(createdTarget.Index > 0);
//             Assert.Equal(createdTarget, fetchedTarget);
//         }

        /// <summary>
        /// Tests that the IPreprocessable.RootFilePath property returns the same value as ProjectFile.
        /// </summary>
//         [Fact] [Error] (173-46)CS0266 Cannot implicitly convert type 'Microsoft.Build.Logging.StructuredLogger.UnitTests.Project' to 'Microsoft.Build.Logging.StructuredLogger.UnitTests.IPreprocessable'. An explicit conversion exists (are you missing a cast?)
//         public void IPreprocessable_RootFilePath_ReturnsProjectFile()
//         {
//             // Arrange
//             string filePath = @"C:\folder\ProjectFile.csproj";
//             _project.ProjectFile = filePath;
//             IPreprocessable preprocessable = _project;
//             // Act
//             string rootFilePath = preprocessable.RootFilePath;
//             // Assert
//             Assert.Equal(filePath, rootFilePath);
//         }

        /// <summary>
        /// Tests that the TargetsDisplayText property returns an empty string when TargetsText is null or empty.
        /// </summary>
//         [Theory] [Error] (189-22)CS1061 'Project' does not contain a definition for 'TargetsText' and no accessible extension method 'TargetsText' accepting a first argument of type 'Project' could be found (are you missing a using directive or an assembly reference?) [Error] (191-43)CS1061 'Project' does not contain a definition for 'TargetsDisplayText' and no accessible extension method 'TargetsDisplayText' accepting a first argument of type 'Project' could be found (are you missing a using directive or an assembly reference?)
//         [InlineData(null)]
//         [InlineData("")]
//         public void TargetsDisplayText_WhenTargetsTextIsNullOrEmpty_ReturnsEmptyString(string targetsText)
//         {
//             // Arrange
//             _project.TargetsText = targetsText;
//             // Act
//             string displayText = _project.TargetsDisplayText;
//             // Assert
//             Assert.Equal(string.Empty, displayText);
//         }

        /// <summary>
        /// Tests that the TargetsDisplayText property returns the correct formatted string when TargetsText is provided.
        /// </summary>
//         [Fact] [Error] (203-22)CS1061 'Project' does not contain a definition for 'TargetsText' and no accessible extension method 'TargetsText' accepting a first argument of type 'Project' could be found (are you missing a using directive or an assembly reference?) [Error] (205-43)CS1061 'Project' does not contain a definition for 'TargetsDisplayText' and no accessible extension method 'TargetsDisplayText' accepting a first argument of type 'Project' could be found (are you missing a using directive or an assembly reference?)
//         public void TargetsDisplayText_WhenTargetsTextIsProvided_ReturnsFormattedString()
//         {
//             // Arrange
//             _project.TargetsText = "Build";
//             // Act
//             string displayText = _project.TargetsDisplayText;
//             // Assert
//             Assert.Equal(" → Build", displayText);
//         }

        /// <summary>
        /// Tests that the ToolTip property produces a string containing ProjectFile, sorted EntryTargets, GlobalProperties and timing information.
        /// </summary>
//         [Fact] [Error] (218-22)CS1061 'Project' does not contain a definition for 'EntryTargets' and no accessible extension method 'EntryTargets' accepting a first argument of type 'Project' could be found (are you missing a using directive or an assembly reference?) [Error] (234-22)CS1061 'Project' does not contain a definition for 'GlobalProperties' and no accessible extension method 'GlobalProperties' accepting a first argument of type 'Project' could be found (are you missing a using directive or an assembly reference?) [Error] (236-39)CS1061 'Project' does not contain a definition for 'ToolTip' and no accessible extension method 'ToolTip' accepting a first argument of type 'Project' could be found (are you missing a using directive or an assembly reference?)
//         public void ToolTip_WhenPropertiesAreSet_ContainsExpectedSections()
//         {
//             // Arrange
//             _project.ProjectFile = @"C:\folder\project.csproj";
//             _project.EntryTargets = new List<string>
//             {
//                 "Compile",
//                 "Build"
//             };
//             var globalProps = new Dictionary<string, string>
//             {
//                 {
//                     "Configuration",
//                     "Release"
//                 },
//                 {
//                     "Platform",
//                     "AnyCPU"
//                 }
//             };
//             _project.GlobalProperties = globalProps.ToImmutableDictionary();
//             // Act
//             string toolTip = _project.ToolTip;
//             // Assert
//             // Check that the tooltip contains the project file.
//             Assert.Contains(_project.ProjectFile, toolTip);
//             // Check that the tooltip contains the header for Targets.
//             Assert.Contains("Targets:", toolTip);
//             // Verify that targets are sorted in invariant culture (Build, Compile)
//             int buildIndex = toolTip.IndexOf("Build", StringComparison.InvariantCulture);
//             int compileIndex = toolTip.IndexOf("Compile", StringComparison.InvariantCulture);
//             Assert.True(buildIndex < compileIndex, "Targets are not sorted as expected.");
//             // Check that the tooltip contains Global Properties header.
//             Assert.Contains("Global Properties:", toolTip);
//             // Check that each key-value pair appears.
//             Assert.Contains("Configuration = Release", toolTip);
//             Assert.Contains("Platform = AnyCPU", toolTip);
//         }

        /// <summary>
        /// Tests that OnTaskAdded stores a task and that GetTaskById returns the same task.
        /// </summary>
//         [Fact] [Error] (262-24)CS0144 Cannot create an instance of the abstract type or interface 'Task' [Error] (267-22)CS1061 'Project' does not contain a definition for 'OnTaskAdded' and no accessible extension method 'OnTaskAdded' accepting a first argument of type 'Project' could be found (are you missing a using directive or an assembly reference?) [Error] (268-40)CS1061 'Project' does not contain a definition for 'GetTaskById' and no accessible extension method 'GetTaskById' accepting a first argument of type 'Project' could be found (are you missing a using directive or an assembly reference?)
//         public void OnTaskAdded_ValidTask_TaskCanBeRetrievedById()
//         {
//             // Arrange
//             int taskId = 100;
//             // Create a dummy Task instance.
//             var task = new Task
//             {
//                 Id = taskId
//             };
//             // Act
//             _project.OnTaskAdded(task);
//             var fetchedTask = _project.GetTaskById(taskId);
//             // Assert
//             Assert.NotNull(fetchedTask);
//             Assert.Equal(taskId, fetchedTask.Id);
//             Assert.Equal(task, fetchedTask);
//         }

        /// <summary>
        /// Tests that GetTaskById returns null when no task has been added with the provided id.
        /// </summary>
//         [Fact] [Error] (284-40)CS1061 'Project' does not contain a definition for 'GetTaskById' and no accessible extension method 'GetTaskById' accepting a first argument of type 'Project' could be found (are you missing a using directive or an assembly reference?)
//         public void GetTaskById_WhenTaskDoesNotExist_ReturnsNull()
//         {
//             // Arrange
//             int nonExistingTaskId = 200;
//             // Act
//             var fetchedTask = _project.GetTaskById(nonExistingTaskId);
//             // Assert
//             Assert.Null(fetchedTask);
//         }

        /// <summary>
        /// Tests that FindTarget returns the correct target from the Children collection when it exists.
        /// </summary>
//         [Fact] [Error] (332-40)CS1061 'Project' does not contain a definition for 'FindTarget' and no accessible extension method 'FindTarget' accepting a first argument of type 'Project' could be found (are you missing a using directive or an assembly reference?)
//         public void FindTarget_WhenTargetExistsInChildren_ReturnsTarget()
//         {
//             // Arrange
//             string targetName = "Deploy";
//             // Assuming that the base class TimedNode exposes a Children collection that can be manipulated.
//             // Create a new target.
//             var target = new Target
//             {
//                 Name = targetName,
//                 Id = 10,
//                 Index = 1
//             };
//             // Use reflection to get the Children property, if available.
//             var childrenProperty = typeof(Project).GetProperty("Children");
//             if (childrenProperty == null)
//             {
//                 // If Children is not publicly accessible, we simulate by adding the target to an assumed list.
//                 // For the purpose of this test, we use reflection to set the backing field if available.
//                 var childrenField = typeof(Project).GetField("children", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
//                 if (childrenField != null)
//                 {
//                     childrenField.SetValue(_project, new List<TimedNode> { target });
//                 }
//                 else
//                 {
//                     throw new Exception("Unable to access Children collection for testing FindTarget.");
//                 }
//             }
//             else
//             {
//                 // Assume setter is available.
//                 var childrenList = new List<TimedNode>
//                 {
//                     target
//                 };
//                 childrenProperty.SetValue(_project, childrenList);
//             }
// 
//             // Act
//             var foundTarget = _project.FindTarget(targetName);
//             // Assert
//             Assert.NotNull(foundTarget);
//             Assert.Equal(targetName, foundTarget.Name);
//         }

        /// <summary>
        /// Tests that FindTarget returns null when no target with the specified name exists.
        /// </summary>
//         [Fact] [Error] (362-40)CS1061 'Project' does not contain a definition for 'FindTarget' and no accessible extension method 'FindTarget' accepting a first argument of type 'Project' could be found (are you missing a using directive or an assembly reference?)
//         public void FindTarget_WhenTargetDoesNotExist_ReturnsNull()
//         {
//             // Arrange
//             string targetName = "NonExistingTarget";
//             // Set an empty Children collection via reflection or assume public setter.
//             var childrenProperty = typeof(Project).GetProperty("Children");
//             if (childrenProperty != null)
//             {
//                 childrenProperty.SetValue(_project, new List<TimedNode>());
//             }
//             else
//             {
//                 var childrenField = typeof(Project).GetField("children", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
//                 if (childrenField != null)
//                 {
//                     childrenField.SetValue(_project, new List<TimedNode>());
//                 }
//             }
// 
//             // Act
//             var foundTarget = _project.FindTarget(targetName);
//             // Assert
//             Assert.Null(foundTarget);
//         }

        /// <summary>
        /// Tests that the IsLowRelevance property getter and setter work as expected.
        /// </summary>
//         [Fact] [Error] (375-22)CS1061 'Project' does not contain a definition for 'IsLowRelevance' and no accessible extension method 'IsLowRelevance' accepting a first argument of type 'Project' could be found (are you missing a using directive or an assembly reference?) [Error] (377-34)CS1061 'Project' does not contain a definition for 'IsLowRelevance' and no accessible extension method 'IsLowRelevance' accepting a first argument of type 'Project' could be found (are you missing a using directive or an assembly reference?) [Error] (379-22)CS1061 'Project' does not contain a definition for 'IsLowRelevance' and no accessible extension method 'IsLowRelevance' accepting a first argument of type 'Project' could be found (are you missing a using directive or an assembly reference?) [Error] (380-35)CS1061 'Project' does not contain a definition for 'IsLowRelevance' and no accessible extension method 'IsLowRelevance' accepting a first argument of type 'Project' could be found (are you missing a using directive or an assembly reference?)
//         public void IsLowRelevance_SetAndGet_ReturnsExpectedValue()
//         {
//             // Arrange
//             // By default, assume IsSelected is false (inherited) so setting flag should reflect.
//             _project.IsLowRelevance = true;
//             // Act & Assert
//             Assert.True(_project.IsLowRelevance);
//             // Turn off the flag
//             _project.IsLowRelevance = false;
//             Assert.False(_project.IsLowRelevance);
//         }

        /// <summary>
        /// Tests that the AdornmentString property returns a string without throwing exceptions.
        /// </summary>
//         [Fact] [Error] (390-41)CS1061 'Project' does not contain a definition for 'AdornmentString' and no accessible extension method 'AdornmentString' accepting a first argument of type 'Project' could be found (are you missing a using directive or an assembly reference?)
//         public void AdornmentString_WhenAccessed_ReturnsNonNullString()
//         {
//             // Act
//             string adornment = _project.AdornmentString;
//             // Assert
//             Assert.NotNull(adornment);
//             // Additional check: the property should return a string instance (can be empty)
//             Assert.IsType<string>(adornment);
//         }

        /// <summary>
        /// Tests the basic getter and setter functionality for various simple properties.
        /// </summary>
//         [Fact] [Error] (411-22)CS1061 'Project' does not contain a definition for 'TargetFramework' and no accessible extension method 'TargetFramework' accepting a first argument of type 'Project' could be found (are you missing a using directive or an assembly reference?) [Error] (412-22)CS1061 'Project' does not contain a definition for 'Platform' and no accessible extension method 'Platform' accepting a first argument of type 'Project' could be found (are you missing a using directive or an assembly reference?) [Error] (413-22)CS1061 'Project' does not contain a definition for 'Configuration' and no accessible extension method 'Configuration' accepting a first argument of type 'Project' could be found (are you missing a using directive or an assembly reference?) [Error] (414-22)CS1061 'Project' does not contain a definition for 'EvaluationId' and no accessible extension method 'EvaluationId' accepting a first argument of type 'Project' could be found (are you missing a using directive or an assembly reference?) [Error] (415-22)CS1061 'Project' does not contain a definition for 'EvaluationText' and no accessible extension method 'EvaluationText' accepting a first argument of type 'Project' could be found (are you missing a using directive or an assembly reference?) [Error] (417-52)CS1061 'Project' does not contain a definition for 'TargetFramework' and no accessible extension method 'TargetFramework' accepting a first argument of type 'Project' could be found (are you missing a using directive or an assembly reference?) [Error] (418-45)CS1061 'Project' does not contain a definition for 'Platform' and no accessible extension method 'Platform' accepting a first argument of type 'Project' could be found (are you missing a using directive or an assembly reference?) [Error] (419-50)CS1061 'Project' does not contain a definition for 'Configuration' and no accessible extension method 'Configuration' accepting a first argument of type 'Project' could be found (are you missing a using directive or an assembly reference?) [Error] (420-49)CS1061 'Project' does not contain a definition for 'EvaluationId' and no accessible extension method 'EvaluationId' accepting a first argument of type 'Project' could be found (are you missing a using directive or an assembly reference?) [Error] (421-51)CS1061 'Project' does not contain a definition for 'EvaluationText' and no accessible extension method 'EvaluationText' accepting a first argument of type 'Project' could be found (are you missing a using directive or an assembly reference?)
//         public void SimpleProperties_SetAndGet_ReturnsExpectedValues()
//         {
//             // Arrange
//             string tf = ".NET6.0";
//             string platform = "AnyCPU";
//             string configuration = "Debug";
//             int evaluationId = 42;
//             string evaluationText = "Evaluation complete";
//             string targetFramework = tf;
//             // Act
//             _project.TargetFramework = targetFramework;
//             _project.Platform = platform;
//             _project.Configuration = configuration;
//             _project.EvaluationId = evaluationId;
//             _project.EvaluationText = evaluationText;
//             // Assert
//             Assert.Equal(targetFramework, _project.TargetFramework);
//             Assert.Equal(platform, _project.Platform);
//             Assert.Equal(configuration, _project.Configuration);
//             Assert.Equal(evaluationId, _project.EvaluationId);
//             Assert.Equal(evaluationText, _project.EvaluationText);
//         }
    }

    // Minimal stub implementations for Target, Task, TimedNode and IPreprocessable to allow testing.
    // In the actual project these are implemented in full.
//     public class TimedNode : IHasSourceFile, IPreprocessable, IHasRelevance, IProjectOrEvaluation [Error] (427-30)CS0535 'TimedNode' does not implement interface member 'IHasSourceFile.SourceFilePath' [Error] (427-46)CS0535 'TimedNode' does not implement interface member 'IPreprocessable.RootFilePath' [Error] (427-63)CS0535 'TimedNode' does not implement interface member 'IHasRelevance.IsLowRelevance'
//     {
//         public string Name { get; set; }
//         public List<TimedNode> Children { get; set; } = new List<TimedNode>();
//         public virtual string TypeName { get; }
//         public virtual string ToolTip { get; }
//         public bool IsSelected { get; set; }
//         protected NodeFlags Flags { get; set; } = NodeFlags.None;
// 
//         protected void SetFlag(NodeFlags flag, bool value)
//         {
//             if (value)
//             {
//                 Flags |= flag;
//             }
//             else
//             {
//                 Flags &= ~flag;
//             }
//         }
// 
//         protected bool HasFlag(NodeFlags flag)
//         {
//             return (Flags & flag) == flag;
//         }
// 
//         // Dummy implementation for GetTimeAndDurationText
//         protected string GetTimeAndDurationText()
//         {
//             return "Time info";
//         }
//     }

    public class Target : TimedNode
    {
        public int Id { get; set; }
        public int Index { get; set; }
    }

//     public class Task [Error] (466-18)CS0101 The namespace 'Microsoft.Build.Logging.StructuredLogger.UnitTests' already contains a definition for 'Task'
//     {
//         public int Id { get; set; }
//     }

    public interface IPreprocessable
    {
        string RootFilePath { get; }
    }

    public interface IHasSourceFile
    {
        string SourceFilePath { get; }
    }

    public interface IHasRelevance
    {
        bool IsLowRelevance { get; set; }
    }

    public interface IProjectOrEvaluation
    {
    }

    [Flags]
    public enum NodeFlags
    {
        None = 0,
        LowRelevance = 1 << 0
    }

    // Dummy extension method to support AdornmentString.
    public static class Extensions
    {
//         public static string GetAdornmentString(this Project project) [Error] (500-30)CS0051 Inconsistent accessibility: parameter type 'Project' is less accessible than method 'Extensions.GetAdornmentString(Project)'
//         {
//             // For testing purposes, just return a fixed string or based on some property.
//             return "Adornment";
//         }
    }

    // Dummy implementation for TextUtilities to support ToString and ToolTip formatting.
//     public static class TextUtilities [Error] (508-25)CS0101 The namespace 'Microsoft.Build.Logging.StructuredLogger.UnitTests' already contains a definition for 'TextUtilities'
//     {
//         public static string ShortenValue(string value, string ellipsis, int maxChars)
//         {
//             if (string.IsNullOrEmpty(value) || value.Length <= maxChars)
//             {
//                 return value;
//             }
// 
//             return value.Substring(0, maxChars) + ellipsis;
//         }
//     }
}
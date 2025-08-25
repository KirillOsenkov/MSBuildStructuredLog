using System;
using Microsoft.Build.Logging.StructuredLogger;
using Moq;
using Xunit;

namespace Microsoft.Build.Logging.StructuredLogger.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="ProjectOrEvaluationHelper"/> class.
    /// </summary>
    public class ProjectOrEvaluationHelperTests
    {
        /// <summary>
        /// A simple test implementation of the IProjectOrEvaluation interface for testing purposes.
        /// </summary>
        private class TestProjectOrEvaluation : IProjectOrEvaluation
        {
            public string ProjectFile { get; set; }
            public string TargetFramework { get; set; }
            public string Platform { get; set; }
            public string Configuration { get; set; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProjectOrEvaluationHelperTests"/> class.
        /// Ensures the cache is cleared and default flag is reset before each test.
        /// </summary>
        public ProjectOrEvaluationHelperTests()
        {
            // Reset the static state before each test
            ProjectOrEvaluationHelper.ClearCache();
            ProjectOrEvaluationHelper.ShowConfigurationAndPlatform = false;
        }

        /// <summary>
        /// Tests that when ShowConfigurationAndPlatform is false, the GetAdornmentString extension method
        /// returns only the TargetFramework even if Configuration and Platform are provided.
        /// </summary>
//         [Fact] [Error] (53-29)CS1929 'ProjectOrEvaluationHelperTests.TestProjectOrEvaluation' does not contain a definition for 'GetAdornmentString' and the best extension method overload 'Extensions.GetAdornmentString(Project)' requires a receiver of type 'Microsoft.Build.Logging.StructuredLogger.UnitTests.Project'
//         public void GetAdornmentString_WhenShowConfigurationAndPlatformFalse_WithValidTargetFramework_ReturnsTargetFrameworkOnly()
//         {
//             // Arrange
//             ProjectOrEvaluationHelper.ShowConfigurationAndPlatform = false;
//             ProjectOrEvaluationHelper.ClearCache();
//             var project = new TestProjectOrEvaluation
//             {
//                 TargetFramework = "net5.0",
//                 Configuration = "Debug",
//                 Platform = "x64"
//             };
// 
//             // Act
//             string result = project.GetAdornmentString();
// 
//             // Assert
//             Assert.Equal("net5.0", result);
//         }

        /// <summary>
        /// Tests that when ShowConfigurationAndPlatform is true and all properties are provided,
        /// the GetAdornmentString extension method returns a composite string concatenated with commas.
        /// </summary>
//         [Fact] [Error] (77-29)CS1929 'ProjectOrEvaluationHelperTests.TestProjectOrEvaluation' does not contain a definition for 'GetAdornmentString' and the best extension method overload 'Extensions.GetAdornmentString(Project)' requires a receiver of type 'Microsoft.Build.Logging.StructuredLogger.UnitTests.Project'
//         public void GetAdornmentString_WhenShowConfigurationAndPlatformTrue_AllPropertiesPresent_ReturnsCompositeString()
//         {
//             // Arrange
//             ProjectOrEvaluationHelper.ShowConfigurationAndPlatform = true;
//             ProjectOrEvaluationHelper.ClearCache();
//             var project = new TestProjectOrEvaluation
//             {
//                 TargetFramework = "net5.0",
//                 Configuration = "Debug",
//                 Platform = "x64"
//             };
// 
//             // Act
//             string result = project.GetAdornmentString();
// 
//             // Assert
//             Assert.Equal("net5.0,Debug,x64", result);
//         }

        /// <summary>
        /// Tests that when ShowConfigurationAndPlatform is true but Configuration and Platform are empty,
        /// the GetAdornmentString extension method returns only the TargetFramework.
        /// </summary>
//         [Fact] [Error] (101-29)CS1929 'ProjectOrEvaluationHelperTests.TestProjectOrEvaluation' does not contain a definition for 'GetAdornmentString' and the best extension method overload 'Extensions.GetAdornmentString(Project)' requires a receiver of type 'Microsoft.Build.Logging.StructuredLogger.UnitTests.Project'
//         public void GetAdornmentString_WhenShowConfigurationAndPlatformTrue_WithEmptyConfigurationAndPlatform_ReturnsTargetFrameworkOnly()
//         {
//             // Arrange
//             ProjectOrEvaluationHelper.ShowConfigurationAndPlatform = true;
//             ProjectOrEvaluationHelper.ClearCache();
//             var project = new TestProjectOrEvaluation
//             {
//                 TargetFramework = "net5.0",
//                 Configuration = string.Empty,
//                 Platform = string.Empty
//             };
// 
//             // Act
//             string result = project.GetAdornmentString();
// 
//             // Assert
//             Assert.Equal("net5.0", result);
//         }

        /// <summary>
        /// Tests that when TargetFramework is empty and ShowConfigurationAndPlatform is false,
        /// the GetAdornmentString extension method returns an empty string.
        /// </summary>
//         [Fact] [Error] (125-29)CS1929 'ProjectOrEvaluationHelperTests.TestProjectOrEvaluation' does not contain a definition for 'GetAdornmentString' and the best extension method overload 'Extensions.GetAdornmentString(Project)' requires a receiver of type 'Microsoft.Build.Logging.StructuredLogger.UnitTests.Project'
//         public void GetAdornmentString_WhenTargetFrameworkEmptyAndShowConfigurationAndPlatformFalse_ReturnsEmptyString()
//         {
//             // Arrange
//             ProjectOrEvaluationHelper.ShowConfigurationAndPlatform = false;
//             ProjectOrEvaluationHelper.ClearCache();
//             var project = new TestProjectOrEvaluation
//             {
//                 TargetFramework = string.Empty,
//                 Configuration = "Debug",
//                 Platform = "x64"
//             };
// 
//             // Act
//             string result = project.GetAdornmentString();
// 
//             // Assert
//             Assert.Equal(string.Empty, result);
//         }

        /// <summary>
        /// Tests that when TargetFramework is empty and ShowConfigurationAndPlatform is true,
        /// the GetAdornmentString extension method returns a composite string of Configuration and Platform.
        /// </summary>
//         [Fact] [Error] (149-29)CS1929 'ProjectOrEvaluationHelperTests.TestProjectOrEvaluation' does not contain a definition for 'GetAdornmentString' and the best extension method overload 'Extensions.GetAdornmentString(Project)' requires a receiver of type 'Microsoft.Build.Logging.StructuredLogger.UnitTests.Project'
//         public void GetAdornmentString_WhenTargetFrameworkEmptyAndShowConfigurationAndPlatformTrue_ReturnsConfigurationAndPlatform()
//         {
//             // Arrange
//             ProjectOrEvaluationHelper.ShowConfigurationAndPlatform = true;
//             ProjectOrEvaluationHelper.ClearCache();
//             var project = new TestProjectOrEvaluation
//             {
//                 TargetFramework = string.Empty,
//                 Configuration = "Debug",
//                 Platform = "x64"
//             };
// 
//             // Act
//             string result = project.GetAdornmentString();
// 
//             // Assert
//             Assert.Equal("Debug,x64", result);
//         }

        /// <summary>
        /// Tests that multiple calls to GetAdornmentString with the same project (and thus same key)
        /// return the same result, demonstrating caching behavior.
        /// </summary>
//         [Fact] [Error] (173-32)CS1929 'ProjectOrEvaluationHelperTests.TestProjectOrEvaluation' does not contain a definition for 'GetAdornmentString' and the best extension method overload 'Extensions.GetAdornmentString(Project)' requires a receiver of type 'Microsoft.Build.Logging.StructuredLogger.UnitTests.Project' [Error] (174-33)CS1929 'ProjectOrEvaluationHelperTests.TestProjectOrEvaluation' does not contain a definition for 'GetAdornmentString' and the best extension method overload 'Extensions.GetAdornmentString(Project)' requires a receiver of type 'Microsoft.Build.Logging.StructuredLogger.UnitTests.Project'
//         public void GetAdornmentString_CalledMultipleTimesWithSameProject_ReturnsSameValue()
//         {
//             // Arrange
//             ProjectOrEvaluationHelper.ShowConfigurationAndPlatform = true;
//             ProjectOrEvaluationHelper.ClearCache();
//             var project = new TestProjectOrEvaluation
//             {
//                 TargetFramework = "net5.0",
//                 Configuration = "Debug",
//                 Platform = "x64"
//             };
// 
//             // Act
//             string firstCall = project.GetAdornmentString();
//             string secondCall = project.GetAdornmentString();
// 
//             // Assert
//             Assert.Equal(firstCall, secondCall);
//         }

        /// <summary>
        /// Tests that the ClearCache method resets the cached values such that changes in the ShowConfigurationAndPlatform flag
        /// are reflected in subsequent calls to GetAdornmentString.
        /// </summary>
//         [Fact] [Error] (199-36)CS1929 'ProjectOrEvaluationHelperTests.TestProjectOrEvaluation' does not contain a definition for 'GetAdornmentString' and the best extension method overload 'Extensions.GetAdornmentString(Project)' requires a receiver of type 'Microsoft.Build.Logging.StructuredLogger.UnitTests.Project' [Error] (204-32)CS1929 'ProjectOrEvaluationHelperTests.TestProjectOrEvaluation' does not contain a definition for 'GetAdornmentString' and the best extension method overload 'Extensions.GetAdornmentString(Project)' requires a receiver of type 'Microsoft.Build.Logging.StructuredLogger.UnitTests.Project'
//         public void ClearCache_WhenCalled_ResetsCacheAndReflectsChangesInShowConfigurationAndPlatform()
//         {
//             // Arrange
//             // Initially show configuration and platform.
//             ProjectOrEvaluationHelper.ShowConfigurationAndPlatform = true;
//             ProjectOrEvaluationHelper.ClearCache();
//             var project = new TestProjectOrEvaluation
//             {
//                 TargetFramework = "net5.0",
//                 Configuration = "Debug",
//                 Platform = "x64"
//             };
// 
//             // Act
//             string initialResult = project.GetAdornmentString();
// 
//             // Change behavior by setting flag to false and clearing cache.
//             ProjectOrEvaluationHelper.ShowConfigurationAndPlatform = false;
//             ProjectOrEvaluationHelper.ClearCache();
//             string newResult = project.GetAdornmentString();
// 
//             // Assert
//             Assert.Equal("net5.0,Debug,x64", initialResult);
//             Assert.Equal("net5.0", newResult);
//             Assert.NotEqual(initialResult, newResult);
//         }
    }
}

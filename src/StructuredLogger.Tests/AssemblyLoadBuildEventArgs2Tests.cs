using System;
using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.Build.Framework.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="AssemblyLoadBuildEventArgs"/> class.
    /// </summary>
    public class AssemblyLoadBuildEventArgsTests
    {
        /// <summary>
        /// Tests that the parameterless constructor sets properties to default values and that the Message property returns the correctly formatted string based on defaults.
        /// </summary>
        [Fact]
        public void Message_UsingParameterlessConstructor_ReturnsDefaultFormattedMessage()
        {
            // Arrange
            var eventArgs = new AssemblyLoadBuildEventArgs();

            // Act
            string actualMessage = eventArgs.Message;

            // Expected: since LoadingContext defaults to TaskRun, and others are null or default.
            string expectedMessage = string.Format(
                "Assembly loaded during {0}{1}: {2} (location: {3}, MVID: {4}, AppDomain: {5})",
                eventArgs.LoadingContext.ToString(),
                string.Empty,
                eventArgs.AssemblyName,
                eventArgs.AssemblyPath,
                eventArgs.MVID.ToString(),
                "[Default]");

            // Assert
            Assert.Equal(expectedMessage, actualMessage);
        }

        /// <summary>
        /// Tests that the parameterized constructor correctly assigns properties and that the Message property returns the formatted string when LoadingInitiator is null.
        /// </summary>
        [Fact]
        public void Message_WhenLoadingInitiatorIsNull_ReturnsFormattedMessageWithoutInitiator()
        {
            // Arrange
            AssemblyLoadingContext loadingContext = AssemblyLoadingContext.Evaluation;
            string? loadingInitiator = null;
            string? assemblyName = "TestAssembly";
            string assemblyPath = @"C:\Test\TestAssembly.dll";
            Guid mvid = Guid.NewGuid();
            string? customAppDomainDescriptor = null;

            var eventArgs = new AssemblyLoadBuildEventArgs(
                loadingContext,
                loadingInitiator,
                assemblyName,
                assemblyPath,
                mvid,
                customAppDomainDescriptor,
                MessageImportance.Low);

            // Act
            string actualMessage = eventArgs.Message;

            // Expected: loadingInitiator omitted when null, and customAppDomainDescriptor replaced with "[Default]".
            string expectedMessage = string.Format(
                "Assembly loaded during {0}{1}: {2} (location: {3}, MVID: {4}, AppDomain: {5})",
                loadingContext.ToString(),
                string.Empty,
                assemblyName,
                assemblyPath,
                mvid.ToString(),
                "[Default]");

            // Assert
            Assert.Equal(expectedMessage, actualMessage);
        }

        /// <summary>
        /// Tests that the parameterized constructor correctly assigns properties and that the Message property returns the formatted string when LoadingInitiator is provided.
        /// </summary>
        [Fact]
        public void Message_WhenLoadingInitiatorIsProvided_ReturnsFormattedMessageWithInitiator()
        {
            // Arrange
            AssemblyLoadingContext loadingContext = AssemblyLoadingContext.LoggerInitialization;
            string? loadingInitiator = "InitProc";
            string? assemblyName = "LoggerAssembly";
            string assemblyPath = @"D:\Assemblies\LoggerAssembly.dll";
            Guid mvid = Guid.NewGuid();
            string? customAppDomainDescriptor = "CustomDomain";

            var eventArgs = new AssemblyLoadBuildEventArgs(
                loadingContext,
                loadingInitiator,
                assemblyName,
                assemblyPath,
                mvid,
                customAppDomainDescriptor,
                MessageImportance.Low);

            // Act
            string actualMessage = eventArgs.Message;

            // Expected: loadingInitiator included inside parentheses.
            string expectedMessage = string.Format(
                "Assembly loaded during {0}{1}: {2} (location: {3}, MVID: {4}, AppDomain: {5})",
                loadingContext.ToString(),
                $" ({loadingInitiator})",
                assemblyName,
                assemblyPath,
                mvid.ToString(),
                customAppDomainDescriptor);

            // Assert
            Assert.Equal(expectedMessage, actualMessage);
        }

        /// <summary>
        /// Tests that calling the Message property multiple times returns the same cached result.
        /// </summary>
        [Fact]
        public void Message_MultipleCalls_ReturnsCachedMessage()
        {
            // Arrange
            AssemblyLoadingContext loadingContext = AssemblyLoadingContext.SdkResolution;
            string? loadingInitiator = "SDKInit";
            string? assemblyName = "SDKAssembly";
            string assemblyPath = @"E:\SDK\SDKAssembly.dll";
            Guid mvid = Guid.NewGuid();
            string? customAppDomainDescriptor = "SDKDomain";

            var eventArgs = new AssemblyLoadBuildEventArgs(
                loadingContext,
                loadingInitiator,
                assemblyName,
                assemblyPath,
                mvid,
                customAppDomainDescriptor,
                MessageImportance.Low);

            // Act
            string firstCall = eventArgs.Message;
            string secondCall = eventArgs.Message;

            // Assert
            Assert.Equal(firstCall, secondCall);
        }
    }
}

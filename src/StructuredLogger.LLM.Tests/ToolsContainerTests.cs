using FluentAssertions;
using Microsoft.Build.Logging.StructuredLogger;
using Microsoft.Extensions.AI;

namespace StructuredLogger.LLM.Tests;

/// <summary>
/// Tests for the IToolsContainer abstraction and tool executor implementations.
/// These tests verify that the refactored tool abstraction works correctly.
/// </summary>
public class ToolsContainerTests
{
    private static Build CreateMockBuild()
    {
        var build = new Build();
        build.Succeeded = true;
        return build;
    }

    [Fact]
    public void EmbeddedFilesToolExecutor_GetTools_ToolsHaveCorrectPhases()
    {
        // Arrange
        var build = CreateMockBuild();
        var executor = new EmbeddedFilesToolExecutor(build);

        // Act
        var tools = executor.GetTools().ToList();

        // Assert - All embedded file tools should be applicable to Research and Summarization
        foreach (var tool in tools)
        {
            tool.ApplicablePhases.Should().HaveFlag(AgentPhase.Research);
            tool.ApplicablePhases.Should().HaveFlag(AgentPhase.Summarization);
        }
    }

    [Fact]
    public void ToolsContainer_CanFilterByPhase()
    {
        // Arrange
        var build = CreateMockBuild();
        var executor = new BinlogToolExecutor(build);
        var phase = AgentPhase.Planning;

        // Act
        var planningTools = executor.GetTools()
            .Where(t => (t.ApplicablePhases & phase) != 0)
            .ToList();

        // Assert
        planningTools.Should().NotBeEmpty("Planning phase should have at least GetBuildSummary");
        planningTools.Should().Contain(t => t.Function.Name == "GetBuildSummary");
        planningTools.Should().Contain(t => t.Function.Name == "GetErrorsAndWarnings");
    }

    [Fact]
    public void ToolsContainer_ResearchPhaseHasMoreToolsThanPlanning()
    {
        // Arrange
        var build = CreateMockBuild();
        var executor = new BinlogToolExecutor(build);

        // Act
        var planningTools = executor.GetTools()
            .Where(t => (t.ApplicablePhases & AgentPhase.Planning) != 0)
            .ToList();
        
        var researchTools = executor.GetTools()
            .Where(t => (t.ApplicablePhases & AgentPhase.Research) != 0)
            .ToList();

        // Assert
        researchTools.Count.Should().BeGreaterThan(planningTools.Count,
            "Research phase should have more tools than planning");
    }

    [Fact]
    public void MonitoredAIFunction_WrapsToolCorrectly()
    {
        // Arrange
        var build = CreateMockBuild();
        var executor = new BinlogToolExecutor(build);
        var tool = executor.GetTools().First();

        // Act
        var monitored = new MonitoredAIFunction(tool.Function);

        // Assert
        monitored.Name.Should().Be(tool.Function.Name);
        monitored.Description.Should().Be(tool.Function.Description);
    }

    [Fact]
    public void MonitoredAIFunction_ThrowsOnNullFunction()
    {
        // Act & Assert
        var act = () => new MonitoredAIFunction(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void MultipleToolExecutors_CanBeEnumerated()
    {
        // Arrange
        var build = CreateMockBuild();
        IToolsContainer[] executors = new IToolsContainer[]
        {
            new BinlogToolExecutor(build),
            new EmbeddedFilesToolExecutor(build)
        };

        // Act
        var allTools = executors.SelectMany(e => e.GetTools()).ToList();

        // Assert
        allTools.Should().NotBeEmpty();
        allTools.Should().HaveCount(8, "BinlogToolExecutor(5) + EmbeddedFilesToolExecutor(3) = 8");
    }

    [Fact]
    public void ToolFunctions_HaveDescriptions()
    {
        // Arrange
        var build = CreateMockBuild();
        var executor = new BinlogToolExecutor(build);

        // Act
        var tools = executor.GetTools().ToList();

        // Assert
        foreach (var tool in tools)
        {
            tool.Function.Description.Should().NotBeNullOrWhiteSpace(
                $"Tool {tool.Function.Name} should have a description");
        }
    }
}

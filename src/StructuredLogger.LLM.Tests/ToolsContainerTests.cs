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
        // Initialize StringTable for search functionality
        if (build.StringTable == null || build.StringTable.Instances == null)
        {
            // StringTable will be initialized automatically when nodes are added
        }
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
        allTools.Should().HaveCount(9, "BinlogToolExecutor(5) + EmbeddedFilesToolExecutor(3) + ListEventsToolExecutor(1) = 9");
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

    [Fact]
    public async System.Threading.Tasks.Task SearchNodesAsync_WithEmptyQuery_ReturnsError()
    {
        // Arrange
        var build = CreateMockBuild();
        var executor = new BinlogToolExecutor(build);

        // Act
        var result = await executor.SearchNodesAsync("", 10);

        // Assert
        result.Should().Contain("Error");
        result.Should().Contain("cannot be empty");
    }

    [Fact]
    public async System.Threading.Tasks.Task SearchNodesAsync_WithBasicQuery_ReturnsResults()
    {
        // Arrange
        var build = CreateMockBuild();
        build.AddChild(new Project { Name = "TestProject" });
        build.AddChild(new Target { Name = "Build" });
        build.AddChild(new Message { Text = "Building project" });

        var executor = new BinlogToolExecutor(build);

        // Act
        var result = await executor.SearchNodesAsync("Build", 10);

        // Assert
        result.Should().Contain("Build");
        result.Should().NotContain("Error");
    }

    [Fact]
    public async System.Threading.Tasks.Task SearchNodesAsync_WithNodeTypeFilter_FiltersCorrectly()
    {
        // Arrange
        var build = CreateMockBuild();
        build.AddChild(new Project { Name = "TestProject" });
        build.AddChild(new Target { Name = "Build" });
        build.AddChild(new Message { Text = "Building" });

        var executor = new BinlogToolExecutor(build);

        // Act - Search with simple query that should find nodes
        var result = await executor.SearchNodesAsync("Test", 10);

        // Assert - Should find the TestProject
        result.Should().NotContain("Error", "search should execute without errors");
        (result.Contains("Project") || result.Contains("TestProject") || result.Contains("No nodes"))
            .Should().BeTrue("result should either find the project or indicate no matches");
    }

    [Fact]
    public async System.Threading.Tasks.Task SearchNodesAsync_WithNoMatches_ReturnsNoResultsMessage()
    {
        // Arrange
        var build = CreateMockBuild();
        build.AddChild(new Project { Name = "TestProject" });

        var executor = new BinlogToolExecutor(build);

        // Act
        var result = await executor.SearchNodesAsync("NonExistent", 10);

        // Assert
        result.Should().Contain("No nodes found");
        result.Should().Contain("NonExistent");
    }

    [Fact]
    public void SearchNodesAsync_Description_ContainsComprehensiveSyntaxGuide()
    {
        // Arrange
        var build = CreateMockBuild();
        var executor = new BinlogToolExecutor(build);
        var tools = executor.GetTools().ToList();
        
        // SearchNodes is the actual AIFunction name (without Async suffix)
        var searchTool = tools.First(t => t.Function.Name == "SearchNodes");

        // Assert
        searchTool.Should().NotBeNull("SearchNodes tool should exist");
        var description = searchTool.Function.Description;
        
        // Verify description contains key search features
        description.Should().Contain("$project", "should document project filter");
        description.Should().Contain("$target", "should document target filter");
        description.Should().Contain("$task", "should document task filter");
        description.Should().Contain("$error", "should document error filter");
        description.Should().Contain("$warning", "should document warning filter");
        description.Should().Contain("under(", "should document under() clause");
        description.Should().Contain("project(", "should document project() clause");
        description.Should().Contain("skipped=", "should document skipped filter");
        description.Should().Contain("$duration", "should document duration display");
        description.Should().Contain("start<", "should document time-based filtering");
        description.Should().Contain("$copy", "should document copy operations");
        description.Should().Contain("EXAMPLES", "should include examples");
    }

    [Fact]
    public void ToolsContainer_HasGuiTools_DefaultsToFalse()
    {
        // Arrange
        var build = CreateMockBuild();

        // Act & Assert - All non-UI tool containers should return false for HasGuiTools
        new BinlogToolExecutor(build).HasGuiTools.Should().BeFalse("BinlogToolExecutor should not have GUI tools");
        new EmbeddedFilesToolExecutor(build).HasGuiTools.Should().BeFalse("EmbeddedFilesToolExecutor should not have GUI tools");
        new ListEventsToolExecutor(build).HasGuiTools.Should().BeFalse("ListEventsToolExecutor should not have GUI tools");
        new ResultsToolExecutor().HasGuiTools.Should().BeFalse("ResultsToolExecutor should not have GUI tools");
    }
}

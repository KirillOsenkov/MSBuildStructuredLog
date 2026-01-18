using FluentAssertions;
using Xunit;

namespace BinlogTool.Tests;

public class PromptConfigurationTests
{
    [Fact]
    public void Parse_WithNoArguments_ReturnsError()
    {
        // Arrange
        var args = new[] { "prompt" };

        // Act
        var (config, errorMessage) = PromptConfiguration.Parse(args);

        // Assert
        config.Should().BeNull();
        errorMessage.Should().Be("Prompt text is required (or use -interactive mode)");
    }

    [Fact]
    public void Parse_WithSimplePrompt_ParsesCorrectly()
    {
        // Arrange
        var args = new[] { "prompt", "why", "is", "this", "slow" };

        // Act
        var (config, errorMessage) = PromptConfiguration.Parse(args);

        // Assert
        errorMessage.Should().BeNull();
        config.Should().NotBeNull();
        config!.PromptText.Should().Be("why is this slow");
        config.AgentMode.Should().BeTrue(); // Default
        config.Interactive.Should().BeFalse();
    }

    [Fact]
    public void Parse_WithModeOption_ParsesCorrectly()
    {
        // Arrange
        var args = new[] { "prompt", "-mode:singleshot", "count", "projects" };

        // Act
        var (config, errorMessage) = PromptConfiguration.Parse(args);

        // Assert
        errorMessage.Should().BeNull();
        config.Should().NotBeNull();
        config!.PromptText.Should().Be("count projects");
        config.AgentMode.Should().BeFalse();
    }

    [Fact]
    public void Parse_WithAgentMode_ParsesCorrectly()
    {
        // Arrange
        var args = new[] { "prompt", "-mode:agent", "analyze", "build" };

        // Act
        var (config, errorMessage) = PromptConfiguration.Parse(args);

        // Assert
        errorMessage.Should().BeNull();
        config.Should().NotBeNull();
        config!.AgentMode.Should().BeTrue();
    }

    [Fact]
    public void Parse_WithInvalidMode_ReturnsError()
    {
        // Arrange
        var args = new[] { "prompt", "-mode:invalid", "test" };

        // Act
        var (config, errorMessage) = PromptConfiguration.Parse(args);

        // Assert
        config.Should().BeNull();
        errorMessage.Should().Contain("Invalid mode");
    }

    [Fact]
    public void Parse_WithInteractiveFlag_ParsesCorrectly()
    {
        // Arrange
        var args = new[] { "prompt", "-interactive" };

        // Act
        var (config, errorMessage) = PromptConfiguration.Parse(args);

        // Assert
        errorMessage.Should().BeNull();
        config.Should().NotBeNull();
        config!.Interactive.Should().BeTrue();
        config.PromptText.Should().BeEmpty();
    }

    [Fact]
    public void Parse_WithVerboseFlag_SetsVerbosity()
    {
        // Arrange
        var args = new[] { "prompt", "-verbose", "test" };

        // Act
        var (config, errorMessage) = PromptConfiguration.Parse(args);

        // Assert
        errorMessage.Should().BeNull();
        config.Should().NotBeNull();
        config!.Verbosity.Should().Be(CliLogger.Verbosity.Verbose);
    }

    [Fact]
    public void Parse_WithQuietFlag_SetsVerbosity()
    {
        // Arrange
        var args = new[] { "prompt", "-quiet", "test" };

        // Act
        var (config, errorMessage) = PromptConfiguration.Parse(args);

        // Assert
        errorMessage.Should().BeNull();
        config.Should().NotBeNull();
        config!.Verbosity.Should().Be(CliLogger.Verbosity.Quiet);
    }

    [Fact]
    public void Parse_WithBinlogPath_ParsesCorrectly()
    {
        // Arrange
        var args = new[] { "prompt", "-binlog:test.binlog", "analyze" };

        // Act
        var (config, errorMessage) = PromptConfiguration.Parse(args);

        // Assert
        errorMessage.Should().BeNull();
        config.Should().NotBeNull();
        config!.BinlogPaths.Should().ContainSingle().Which.Should().Be("test.binlog");
    }

    [Fact]
    public void Parse_WithQuotedBinlogPath_RemovesQuotes()
    {
        // Arrange
        var args = new[] { "prompt", "-binlog:\"C:\\My Path\\build.binlog\"", "analyze" };

        // Act
        var (config, errorMessage) = PromptConfiguration.Parse(args);

        // Assert
        errorMessage.Should().BeNull();
        config.Should().NotBeNull();
        config!.BinlogPaths.Should().ContainSingle().Which.Should().Be("C:\\My Path\\build.binlog");
    }

    [Fact]
    public void Parse_WithRecurseFlag_SetsRecurse()
    {
        // Arrange
        var args = new[] { "prompt", "--recurse", "test" };

        // Act
        var (config, errorMessage) = PromptConfiguration.Parse(args);

        // Assert
        errorMessage.Should().BeNull();
        config.Should().NotBeNull();
        config!.Recurse.Should().BeTrue();
    }

    [Fact]
    public void Parse_WithLLMEndpoint_ParsesCorrectly()
    {
        // Arrange
        var args = new[] { "prompt", "-llm-endpoint:https://test.com", "query" };

        // Act
        var (config, errorMessage) = PromptConfiguration.Parse(args);

        // Assert
        errorMessage.Should().BeNull();
        config.Should().NotBeNull();
        config!.Endpoint.Should().Be("https://test.com");
    }

    [Fact]
    public void Parse_WithLLMModel_ParsesCorrectly()
    {
        // Arrange
        var args = new[] { "prompt", "-llm-model:gpt-4", "query" };

        // Act
        var (config, errorMessage) = PromptConfiguration.Parse(args);

        // Assert
        errorMessage.Should().BeNull();
        config.Should().NotBeNull();
        config!.Model.Should().Be("gpt-4");
    }

    [Fact]
    public void Parse_WithLLMApiKey_ParsesCorrectly()
    {
        // Arrange
        var args = new[] { "prompt", "-llm-api-key:secret123", "query" };

        // Act
        var (config, errorMessage) = PromptConfiguration.Parse(args);

        // Assert
        errorMessage.Should().BeNull();
        config.Should().NotBeNull();
        config!.ApiKey.Should().Be("secret123");
    }

    [Fact]
    public void Parse_WithMultipleOptions_ParsesAllCorrectly()
    {
        // Arrange
        var args = new[] 
        { 
            "prompt", 
            "-binlog:test.binlog", 
            "--recurse",
            "-mode:singleshot",
            "-verbose",
            "-llm-endpoint:https://test.com",
            "-llm-model:gpt-4",
            "-llm-api-key:key123",
            "what", "failed"
        };

        // Act
        var (config, errorMessage) = PromptConfiguration.Parse(args);

        // Assert
        errorMessage.Should().BeNull();
        config.Should().NotBeNull();
        config!.BinlogPaths.Should().ContainSingle().Which.Should().Be("test.binlog");
        config.Recurse.Should().BeTrue();
        config.AgentMode.Should().BeFalse();
        config.Verbosity.Should().Be(CliLogger.Verbosity.Verbose);
        config.Endpoint.Should().Be("https://test.com");
        config.Model.Should().Be("gpt-4");
        config.ApiKey.Should().Be("key123");
        config.PromptText.Should().Be("what failed");
    }

    [Fact]
    public void Parse_WithPromptContainingDashes_TreatsThemAsPrompt()
    {
        // Arrange
        var args = new[] { "prompt", "-mode:singleshot", "count", "projects", "-arg:value" };

        // Act
        var (config, errorMessage) = PromptConfiguration.Parse(args);

        // Assert
        errorMessage.Should().BeNull();
        config.Should().NotBeNull();
        config!.PromptText.Should().Be("count projects -arg:value");
    }

    [Fact]
    public void Parse_WithHelpFlag_ReturnsNullWithoutError()
    {
        // Arrange
        var args = new[] { "prompt", "-help" };

        // Act
        var (config, errorMessage) = PromptConfiguration.Parse(args);

        // Assert
        config.Should().BeNull();
        errorMessage.Should().BeNull(); // Signals to show help
    }

    [Fact]
    public void Parse_WithUnknownOption_ReturnsError()
    {
        // Arrange
        var args = new[] { "prompt", "-unknown:value", "test" };

        // Act
        var (config, errorMessage) = PromptConfiguration.Parse(args);

        // Assert
        config.Should().BeNull();
        errorMessage.Should().Contain("Unknown option");
    }

    [Fact]
    public void ToLLMConfiguration_WithNoOverrides_UsesEnvironmentVariables()
    {
        // Arrange
        var config = new PromptConfiguration
        {
            PromptText = "test",
            AgentMode = true
        };

        // Act
        var llmConfig = config.ToLLMConfiguration();

        // Assert
        llmConfig.Should().NotBeNull();
        llmConfig.AgentMode.Should().BeTrue();
    }

    [Fact]
    public void ToLLMConfiguration_WithOverrides_UsesOverrides()
    {
        // Arrange
        var config = new PromptConfiguration
        {
            PromptText = "test",
            Endpoint = "https://test.com",
            Model = "test-model",
            ApiKey = "test-key",
            AgentMode = false
        };

        // Act
        var llmConfig = config.ToLLMConfiguration();

        // Assert
        llmConfig.Should().NotBeNull();
        llmConfig.Endpoint.Should().Be("https://test.com");
        llmConfig.ModelName.Should().Be("test-model");
        llmConfig.ApiKey.Should().Be("test-key");
        llmConfig.AgentMode.Should().BeFalse();
    }
}

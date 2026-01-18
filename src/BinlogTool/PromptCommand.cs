using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Logging.StructuredLogger;
using StructuredLogger.LLM;
using StructuredLogger.LLM.Logging;

namespace BinlogTool
{
    /// <summary>
    /// Command for LLM-powered binlog analysis via prompts.
    /// </summary>
    public class PromptCommand
    {
        private CliLogger logger;
        private PromptProgressReporter reporter;
        private CancellationTokenSource cancellationTokenSource;

        public async Task<int> Execute(string[] args)
        {
            // Parse configuration
            var (config, errorMessage) = PromptConfiguration.Parse(args);

            if (config == null)
            {
                if (errorMessage == null)
                {
                    // Help requested
                    PromptConfiguration.ShowHelp();
                    return 0;
                }
                else
                {
                    // Parse error
                    Console.Error.WriteLine($"Error: {errorMessage}");
                    Console.Error.WriteLine("Use 'binlogtool prompt -help' for usage information.");
                    return -3;
                }
            }

            // Initialize logger
            logger = new CliLogger(config.Verbosity);
            reporter = new PromptProgressReporter(logger);

            // Setup cancellation (Ctrl+C)
            cancellationTokenSource = new CancellationTokenSource();
            Console.CancelKeyPress += OnCancelKeyPress;

            try
            {
                // Discover binlog files
                var binlogFiles = DiscoverBinlogFiles(config);
                if (binlogFiles.Count == 0)
                {
                    logger.LogError("No binlog files found.");
                    logger.LogSystem("Searched in: " + Environment.CurrentDirectory);
                    if (config.Recurse)
                    {
                        logger.LogSystem("Recursion: enabled");
                    }
                    return -1;
                }

                logger.LogVerbose($"Found {binlogFiles.Count} binlog file(s):");
                foreach (var file in binlogFiles)
                {
                    logger.LogVerbose($"  - {file}");
                }

                // Load all discovered binlogs into a multi-build context
                var buildContext = new MultiBuildContext();
                var maxBinlogs = config.MaxBinlogs;
                var filesToLoad = binlogFiles.Take(maxBinlogs).ToList();

                if (binlogFiles.Count > maxBinlogs)
                {
                    logger.LogSystem($"Limiting to first {maxBinlogs} binlog files (use -max-binlogs: to change).");
                }

                logger.LogSystem($"Loading {filesToLoad.Count} binlog file(s)...");

                foreach (var binlogPath in filesToLoad)
                {
                    try
                    {
                        var build = BinaryLog.ReadBuild(binlogPath);
                        var buildId = buildContext.AddBuild(build);
                        logger.LogVerbose($"  ✓ Loaded: {System.IO.Path.GetFileName(binlogPath)} as [{buildId}]");
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning($"  ✗ Failed to load: {System.IO.Path.GetFileName(binlogPath)} - {ex.Message}");
                    }
                }

                if (buildContext.BuildCount == 0)
                {
                    logger.LogError("No binlog files could be loaded.");
                    return -1;
                }

                // Display loaded builds summary
                logger.LogSystem("");
                logger.LogSystem("Loaded builds:");
                foreach (var buildInfo in buildContext.GetAllBuilds())
                {
                    var primary = buildInfo.IsPrimary ? " [PRIMARY]" : "";
                    var status = buildInfo.Succeeded ? "✓" : "✗";
                    logger.LogSystem($"  [{buildInfo.BuildId}] {buildInfo.FriendlyName}{primary} - {status} {buildInfo.DurationText}");
                    logger.LogVerbose($"         Path: {buildInfo.FullPath}");
                }
                logger.LogSystem("");

                // If specific primary requested, set it
                if (!string.IsNullOrEmpty(config.PrimaryBuildId))
                {
                    try
                    {
                        buildContext.SetPrimaryBuild(config.PrimaryBuildId);
                        logger.LogVerbose($"Primary build set to: {config.PrimaryBuildId}");
                    }
                    catch (ArgumentException)
                    {
                        logger.LogWarning($"Requested primary build '{config.PrimaryBuildId}' not found. Using default.");
                    }
                }

                // Configure LLM
                var llmConfig = config.ToLLMConfiguration();

                // If GitHub Copilot is not configured (no API key), trigger device flow
                if (!llmConfig.IsConfigured && llmConfig.Type == LLMConfiguration.ClientType.GitHubCopilot)
                {
                    logger.LogSystem("GitHub Copilot selected but no API key provided. Initiating device flow authentication...");
                    logger.LogSystem("");

                    try
                    {
                        using var authenticator = new StructuredLogger.LLM.Clients.GitHub.GitHubDeviceFlowAuthenticator();
                        var githubToken = await authenticator.AuthenticateAsync(cancellationTokenSource.Token);

                        // Update configuration with obtained token
                        llmConfig.ApiKey = githubToken;

                        logger.LogSystem("");
                        logger.LogSystem("✓ Authentication successful!");
                        logger.LogSystem("");
                    }
                    catch (Exception ex)
                    {
                        logger.LogError($"Authentication failed: {ex.Message}");
                        return -2;
                    }
                }

                // Check if configuration is complete
                if (!llmConfig.IsConfigured)
                {
                    logger.LogError("LLM is not configured.");
                    logger.LogSystem("Please set these environment variables:");
                    logger.LogSystem("  LLM_ENDPOINT - LLM service endpoint URL");
                    logger.LogSystem("  LLM_MODEL - Model name (e.g., claude-sonnet-4-5-2, gpt-4)");
                    logger.LogSystem("  LLM_API_KEY - API key for authentication");
                    logger.LogSystem("Or use command-line options: -llm-endpoint, -llm-model, -llm-api-key");
                    logger.LogSystem("");
                    logger.LogSystem("For GitHub Copilot, set LLM_ENDPOINT to 'github-copilot' and device flow will be used.");
                    return -2;
                }

                logger.LogSystem($"LLM configured: {llmConfig.ModelName} ({llmConfig.Type})");

                // Execute based on mode
                if (config.Interactive)
                {
                    return await ExecuteInteractiveMode(buildContext, llmConfig, cancellationTokenSource.Token);
                }
                else
                {
                    return await ExecuteSinglePrompt(buildContext, llmConfig, config.PromptText, cancellationTokenSource.Token);
                }
            }
            catch (OperationCanceledException)
            {
                logger.LogSystem("Operation cancelled by user.");
                return -5;
            }
            catch (Exception ex)
            {
                logger.LogError($"Unexpected error: {ex.Message}");
                logger.LogVerbose(ex.StackTrace);
                return -4;
            }
            finally
            {
                Console.CancelKeyPress -= OnCancelKeyPress;
                cancellationTokenSource?.Dispose();
            }
        }

        private List<string> DiscoverBinlogFiles(PromptConfiguration config)
        {
            // If explicit binlog paths provided, use them
            if (config.BinlogPaths.Any())
            {
                var result = new List<string>();
                foreach (var path in config.BinlogPaths)
                {
                    result.AddRange(BinlogDiscovery.DiscoverBinlogs(path, config.Recurse));
                }
                return result;
            }

            // Otherwise, auto-discover in current directory
            return BinlogDiscovery.DiscoverBinlogs(null, config.Recurse);
        }

        private async Task<int> ExecuteSinglePrompt(
            MultiBuildContext buildContext,
            LLMConfiguration llmConfig,
            string promptText,
            CancellationToken cancellationToken)
        {
            logger.LogSystem($"Mode: {(llmConfig.AgentMode ? "Agent" : "Single-Shot")}");
            logger.LogSystem($"Prompt: {promptText}");
            logger.LogSystem("");

            try
            {
                if (llmConfig.AgentMode)
                {
                    // Agent mode - multi-step reasoning
                    var loggerAdapter = new CliLoggerAdapter(logger);
                    var agenticService = await AgenticLLMChatService.CreateAsync(buildContext, llmConfig, loggerAdapter, cancellationToken);

                    // Subscribe to events
                    agenticService.ProgressUpdated += reporter.OnAgentProgress;
                    agenticService.MessageAdded += reporter.OnMessage;
                    agenticService.ToolCallExecuting += reporter.OnToolCallStarted;
                    agenticService.ToolCallExecuted += reporter.OnToolCallCompleted;
                    agenticService.RequestRetrying += reporter.OnRetrying;

                    var result = await agenticService.ExecuteAgenticWorkflowAsync(promptText, cancellationToken);

                    logger.LogSystem("");
                    logger.LogSystem("=== Final Answer ===");
                    logger.LogResponse(result);
                }
                else
                {
                    // Single-shot mode - direct Q&A
                    var loggerAdapter = new CliLoggerAdapter(logger);
                    var chatService = await LLMChatService.CreateAsync(buildContext, llmConfig, loggerAdapter, cancellationToken);

                    // Subscribe to events
                    chatService.MessageAdded += reporter.OnMessage;
                    chatService.ToolCallExecuting += reporter.OnToolCallStarted;
                    chatService.ToolCallExecuted += reporter.OnToolCallCompleted;
                    chatService.RequestRetrying += reporter.OnRetrying;

                    var result = await chatService.SendMessageAsync(promptText, cancellationToken);

                    logger.LogSystem("");
                    logger.LogResponse(result);
                }

                return 0;
            }
            catch (OperationCanceledException)
            {
                throw; // Re-throw to be handled by outer catch
            }
            catch (Exception ex)
            {
                logger.LogError($"LLM execution failed: {ex.Message}");
                logger.LogVerbose(ex.StackTrace);
                return -4;
            }
        }

        private async Task<int> ExecuteInteractiveMode(
            MultiBuildContext buildContext,
            LLMConfiguration llmConfig,
            CancellationToken cancellationToken)
        {
            logger.LogSystem("Entering interactive mode. Type 'exit' or 'quit' to leave, 'clear' to clear history.");
            logger.LogSystem($"Mode: {(llmConfig.AgentMode ? "Agent" : "Single-Shot")} (use '/mode agent' or '/mode singleshot' to switch)");
            logger.LogSystem($"Builds loaded: {buildContext.BuildCount} (use '.builds' to list, '.primary <id>' to change primary)");
            logger.LogSystem("");

            LLMChatService chatService = null;
            AgenticLLMChatService agenticService = null;

            // Initialize appropriate service
            async System.Threading.Tasks.Task InitializeServicesAsync()
            {
                chatService?.Dispose();
                agenticService?.Dispose();

                var loggerAdapter = new CliLoggerAdapter(logger);

                if (llmConfig.AgentMode)
                {
                    agenticService = await AgenticLLMChatService.CreateAsync(buildContext, llmConfig, loggerAdapter, cancellationToken);

                    // Register AskUser tool for interactive user clarification
                    agenticService.RegisterToolContainer(new AskUserToolExecutor(new ConsoleUserInteraction()));

                    agenticService.ProgressUpdated += reporter.OnAgentProgress;
                    agenticService.MessageAdded += reporter.OnMessage;
                    agenticService.ToolCallExecuting += reporter.OnToolCallStarted;
                    agenticService.ToolCallExecuted += reporter.OnToolCallCompleted;
                    agenticService.RequestRetrying += reporter.OnRetrying;
                }
                else
                {
                    chatService = await LLMChatService.CreateAsync(buildContext, llmConfig, loggerAdapter, cancellationToken);

                    // Register AskUser tool for interactive user clarification
                    chatService.RegisterToolContainer(new AskUserToolExecutor(new ConsoleUserInteraction()));

                    chatService.MessageAdded += reporter.OnMessage;
                    chatService.ToolCallExecuting += reporter.OnToolCallStarted;
                    chatService.ToolCallExecuted += reporter.OnToolCallCompleted;
                    chatService.RequestRetrying += reporter.OnRetrying;
                }
            }

            await InitializeServicesAsync();

            while (!cancellationToken.IsCancellationRequested)
            {
                // Prompt for input with build context
                Console.Write($"[{buildContext.PrimaryBuildId}]> ");
                var input = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(input))
                {
                    continue;
                }

                // Handle dot commands for build management
                if (input.StartsWith("."))
                {
                    if (HandleDotCommand(input, buildContext))
                        continue;
                }

                // Handle commands
                if (input.Equals("exit", StringComparison.OrdinalIgnoreCase) ||
                    input.Equals("quit", StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogSystem("Exiting interactive mode.");
                    break;
                }

                if (input.Equals("clear", StringComparison.OrdinalIgnoreCase))
                {
                    chatService?.ClearConversation();
                    logger.LogSystem("Conversation history cleared.");
                    continue;
                }

                if (input.StartsWith("/mode ", StringComparison.OrdinalIgnoreCase))
                {
                    var mode = input.Substring("/mode ".Length).Trim();
                    if (mode.Equals("agent", StringComparison.OrdinalIgnoreCase))
                    {
                        llmConfig.AgentMode = true;
                        await InitializeServicesAsync();
                        logger.LogSystem("Switched to Agent mode.");
                    }
                    else if (mode.Equals("singleshot", StringComparison.OrdinalIgnoreCase))
                    {
                        llmConfig.AgentMode = false;
                        await InitializeServicesAsync();
                        logger.LogSystem("Switched to Single-Shot mode.");
                    }
                    else
                    {
                        logger.LogError($"Unknown mode: {mode}. Use 'agent' or 'singleshot'.");
                    }
                    continue;
                }

                // Execute prompt
                try
                {
                    if (llmConfig.AgentMode)
                    {
                        var result = await agenticService.ExecuteAgenticWorkflowAsync(input, cancellationToken);
                        logger.LogSystem("");
                        logger.LogResponse(result);
                    }
                    else
                    {
                        var result = await chatService.SendMessageAsync(input, cancellationToken);
                        logger.LogSystem("");
                        logger.LogResponse(result);
                    }

                    Console.WriteLine();
                }
                catch (OperationCanceledException)
                {
                    logger.LogSystem("Operation cancelled.");
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError($"Error: {ex.Message}");
                    logger.LogVerbose(ex.StackTrace);
                }
            }

            // Cleanup
            chatService?.Dispose();
            agenticService?.Dispose();

            return 0;
        }

        /// <summary>
        /// Handles dot commands for build management in interactive mode.
        /// </summary>
        private bool HandleDotCommand(string input, MultiBuildContext context)
        {
            var parts = input.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            var command = parts[0].ToLowerInvariant();
            var arg = parts.Length > 1 ? parts[1] : null;

            switch (command)
            {
                case ".builds":
                    // List all loaded builds
                    logger.LogSystem("Loaded builds:");
                    foreach (var buildInfo in context.GetAllBuilds())
                    {
                        var primary = buildInfo.IsPrimary ? " [PRIMARY]" : "";
                        var status = buildInfo.Succeeded ? "✓" : "✗";
                        logger.LogSystem($"  [{buildInfo.BuildId}] {buildInfo.FriendlyName}{primary} - {status} {buildInfo.DurationText}");
                        logger.LogVerbose($"         Path: {buildInfo.FullPath}");
                    }
                    return true;

                case ".primary":
                    // Switch primary build
                    if (string.IsNullOrEmpty(arg))
                    {
                        logger.LogSystem($"Current primary: {context.PrimaryBuildId}");
                        logger.LogSystem("Usage: .primary <build_id>");
                    }
                    else
                    {
                        try
                        {
                            context.SetPrimaryBuild(arg);
                            logger.LogSystem($"Primary build changed to: {arg}");
                        }
                        catch (ArgumentException ex)
                        {
                            logger.LogError(ex.Message);
                        }
                    }
                    return true;

                case ".add":
                    // Add another binlog
                    if (string.IsNullOrEmpty(arg))
                    {
                        logger.LogSystem("Usage: .add <path_to_binlog>");
                    }
                    else
                    {
                        try
                        {
                            var build = BinaryLog.ReadBuild(arg);
                            var buildId = context.AddBuild(build);
                            logger.LogSystem($"Added build: [{buildId}] from {System.IO.Path.GetFileName(arg)}");
                        }
                        catch (Exception ex)
                        {
                            logger.LogError($"Failed to load binlog: {ex.Message}");
                        }
                    }
                    return true;

                case ".remove":
                    // Remove a build
                    if (string.IsNullOrEmpty(arg))
                    {
                        logger.LogSystem("Usage: .remove <build_id>");
                    }
                    else
                    {
                        try
                        {
                            context.RemoveBuild(arg);
                            logger.LogSystem($"Removed build: {arg}");
                        }
                        catch (ArgumentException ex)
                        {
                            logger.LogError(ex.Message);
                        }
                        catch (InvalidOperationException ex)
                        {
                            logger.LogError(ex.Message);
                        }
                    }
                    return true;

                case ".help":
                    logger.LogSystem("Interactive Commands:");
                    logger.LogSystem("  .builds          - List all loaded builds");
                    logger.LogSystem("  .primary <id>    - Set primary build");
                    logger.LogSystem("  .add <path>      - Add another binlog file");
                    logger.LogSystem("  .remove <id>     - Remove a build");
                    logger.LogSystem("  .help            - Show this help");
                    logger.LogSystem("  exit/quit        - Exit interactive mode");
                    logger.LogSystem("  clear            - Clear chat history");
                    logger.LogSystem("  /mode agent      - Switch to Agent mode");
                    logger.LogSystem("  /mode singleshot - Switch to Single-Shot mode");
                    return true;

                default:
                    logger.LogWarning($"Unknown command: {command}. Type .help for available commands.");
                    return true;
            }
        }

        private void OnCancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true; // Prevent immediate termination
            cancellationTokenSource?.Cancel();
            logger?.LogSystem("Cancelling...");
        }
    }
}

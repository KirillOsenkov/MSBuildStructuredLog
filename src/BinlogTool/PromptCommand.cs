using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Logging.StructuredLogger;
using StructuredLogger.LLM;

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

                // For now, use the first binlog file
                // TODO: Support multiple binlogs
                var binlogPath = binlogFiles[0];
                if (binlogFiles.Count > 1)
                {
                    logger.LogSystem($"Multiple binlogs found. Using: {System.IO.Path.GetFileName(binlogPath)}");
                }

                // Load binlog
                logger.LogSystem($"Loading binlog: {System.IO.Path.GetFileName(binlogPath)}");
                Build build;
                try
                {
                    build = BinaryLog.ReadBuild(binlogPath);
                }
                catch (Exception ex)
                {
                    logger.LogError($"Failed to load binlog: {ex.Message}");
                    return -1;
                }

                logger.LogVerbose($"Build loaded: {build.Succeeded} (Duration: {build.DurationText})");

                // Configure LLM
                var llmConfig = config.ToLLMConfiguration();
                if (!llmConfig.IsConfigured)
                {
                    logger.LogError("LLM is not configured.");
                    logger.LogSystem("Please set these environment variables:");
                    logger.LogSystem("  LLM_ENDPOINT - LLM service endpoint URL");
                    logger.LogSystem("  LLM_MODEL - Model name (e.g., claude-sonnet-4-5-2, gpt-4)");
                    logger.LogSystem("  LLM_API_KEY - API key for authentication");
                    logger.LogSystem("Or use command-line options: -llm-endpoint, -llm-model, -llm-api-key");
                    return -2;
                }

                logger.LogSystem($"LLM configured: {llmConfig.ModelName} ({llmConfig.Type})");

                // Execute based on mode
                if (config.Interactive)
                {
                    return await ExecuteInteractiveMode(build, llmConfig, cancellationTokenSource.Token);
                }
                else
                {
                    return await ExecuteSinglePrompt(build, llmConfig, config.PromptText, cancellationTokenSource.Token);
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
            Build build, 
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
                    var agenticService = new AgenticLLMChatService(build, llmConfig);
                    
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
                    var chatService = new LLMChatService(build, llmConfig);
                    
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
            Build build,
            LLMConfiguration llmConfig,
            CancellationToken cancellationToken)
        {
            logger.LogSystem("Entering interactive mode. Type 'exit' or 'quit' to leave, 'clear' to clear history.");
            logger.LogSystem($"Mode: {(llmConfig.AgentMode ? "Agent" : "Single-Shot")} (use '/mode agent' or '/mode singleshot' to switch)");
            logger.LogSystem("");

            LLMChatService chatService = null;
            AgenticLLMChatService agenticService = null;

            // Initialize appropriate service
            void InitializeServices()
            {
                chatService?.Dispose();
                agenticService?.Dispose();

                if (llmConfig.AgentMode)
                {
                    agenticService = new AgenticLLMChatService(build, llmConfig);
                    agenticService.ProgressUpdated += reporter.OnAgentProgress;
                    agenticService.MessageAdded += reporter.OnMessage;
                    agenticService.ToolCallExecuting += reporter.OnToolCallStarted;
                    agenticService.ToolCallExecuted += reporter.OnToolCallCompleted;
                    agenticService.RequestRetrying += reporter.OnRetrying;
                }
                else
                {
                    chatService = new LLMChatService(build, llmConfig);
                    chatService.MessageAdded += reporter.OnMessage;
                    chatService.ToolCallExecuting += reporter.OnToolCallStarted;
                    chatService.ToolCallExecuted += reporter.OnToolCallCompleted;
                    chatService.RequestRetrying += reporter.OnRetrying;
                }
            }

            InitializeServices();

            while (!cancellationToken.IsCancellationRequested)
            {
                // Prompt for input
                Console.Write("> ");
                var input = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(input))
                {
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
                        InitializeServices();
                        logger.LogSystem("Switched to Agent mode.");
                    }
                    else if (mode.Equals("singleshot", StringComparison.OrdinalIgnoreCase))
                    {
                        llmConfig.AgentMode = false;
                        InitializeServices();
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

        private void OnCancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true; // Prevent immediate termination
            cancellationTokenSource?.Cancel();
            logger?.LogSystem("Cancelling...");
        }
    }
}

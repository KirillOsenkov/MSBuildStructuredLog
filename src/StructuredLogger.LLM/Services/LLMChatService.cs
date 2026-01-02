using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Logging.StructuredLogger;
using Microsoft.Extensions.AI;
using StructuredLogger.LLM.Logging;

namespace StructuredLogger.LLM
{
    /// <summary>
    /// Main service for LLM Chat functionality.
    /// Orchestrates chat history, LLM communication, and tool execution.
    /// </summary>
    public class LLMChatService : IDisposable
    {
        private readonly BinlogContextProvider contextProvider;
        private readonly List<IToolsContainer> toolContainers;
        private MultiProviderLLMClient? llmClient;
        private readonly LLMConfiguration configuration;
        private readonly List<ChatMessage> chatHistory;
        private BaseNode? currentSelectedNode;
        private readonly ILLMLogger? logger;

        // Token management settings
        private const int MaxPromptTokens = 180000; // Leave buffer below 200k limit
        private const int EstimatedTokensPerMessage = 500; // Conservative estimate
        private const int MaxChatHistoryMessages = 20; // Keep recent context

        public event EventHandler<ChatMessageViewModel>? MessageAdded;
        public event EventHandler? ConversationCleared;
        public event EventHandler<ToolCallInfo>? ToolCallExecuting;
        public event EventHandler<ToolCallInfo>? ToolCallExecuted;
        public event EventHandler<ResilienceEventArgs>? RequestRetrying;

        public bool IsConfigured => configuration?.IsConfigured ?? false;
        public string ConfigurationStatus => configuration?.GetConfigurationStatus() ?? "Not initialized";

        public LLMChatService(Build build, LLMConfiguration? config = null, ILLMLogger? logger = null)
        {
            this.contextProvider = new BinlogContextProvider(build);
            this.toolContainers = new List<IToolsContainer>();
            this.chatHistory = new List<ChatMessage>();
            this.configuration = config ?? LLMConfiguration.LoadFromEnvironment();
            this.logger = logger;

            // Register default tool executors
            RegisterToolContainer(new BinlogToolExecutor(build));
            RegisterToolContainer(new EmbeddedFilesToolExecutor(build));
            RegisterToolContainer(new ListEventsToolExecutor(build));

            if (configuration.IsConfigured)
            {
                InitializeLLMClient();
            }
        }

        /// <summary>
        /// Registers an additional tool executor with this service.
        /// Used to add UI-specific tools after service construction.
        /// </summary>
        public void RegisterToolContainer(IToolsContainer container)
        {
            if (container == null)
            {
                throw new ArgumentNullException(nameof(container));
            }

            toolContainers.Add(container);
        }

        private void InitializeLLMClient()
        {
            if (configuration.IsConfigured)
            {
                try
                {
                    var client = new MultiProviderLLMClient(configuration, logger: logger);
                    this.llmClient = client;
                    
                    // For non-GitHub Copilot, client is ready immediately
                    // For GitHub Copilot, InitializeAsync must be called before use
                    if (client.IsInitialized)
                    {
                        SubscribeToResilienceEvents(client);
                    }
                }
                catch (Exception ex)
                {
                    logger?.LogError($"Failed to initialize LLM client: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Initializes the LLM client asynchronously.
        /// Required for GitHub Copilot clients (for device flow authentication).
        /// No-op for other providers (already initialized synchronously).
        /// </summary>
        public async System.Threading.Tasks.Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (llmClient != null && !llmClient.IsInitialized)
            {
                await llmClient.InitializeAsync(cancellationToken);
                SubscribeToResilienceEvents(llmClient);
            }
        }

        private void SubscribeToResilienceEvents(MultiProviderLLMClient client)
        {
            // Subscribe to resilience events
            if (client.ResilientClient != null)
            {
                client.ResilientClient.RequestRetrying += (sender, e) => RequestRetrying?.Invoke(this, e);
            }
        }

        public void SetSelectedNode(BaseNode node)
        {
            currentSelectedNode = node;
        }

        public void ClearConversation()
        {
            chatHistory.Clear();
            ConversationCleared?.Invoke(this, EventArgs.Empty);
        }

        public LLMConfiguration GetConfiguration()
        {
            return configuration;
        }

        public void Reconfigure(LLMConfiguration newConfig)
        {
            if (newConfig == null)
            {
                throw new ArgumentNullException(nameof(newConfig));
            }

            // Update configuration properties
            configuration.Endpoint = newConfig.Endpoint;
            configuration.ApiKey = newConfig.ApiKey;
            configuration.ModelName = newConfig.ModelName;
            configuration.AutoSendOnEnter = newConfig.AutoSendOnEnter;
            configuration.AgentMode = newConfig.AgentMode;
            configuration.UpdateType();

            // Reinitialize with new settings
            InitializeLLMClient();

            // Keep chat history - don't clear
        }

        private string GetSystemPrompt()
        {
            var overview = contextProvider is BinlogContextProvider provider
                ? provider.GetBuildOverview()
                : "Build log loaded";

            return @"You are an expert assistant helping developers analyze their MSBuild build logs (.binlog files).
You have access to tools that can query the build data including projects, targets, tasks, errors, warnings, and timing information.
When the user asks questions, you must use the available tools to retrieve accurate information from the build log - as you do not have information about their builds in your training set.
Be concise and helpful. Format your responses clearly.

Available context:
" + overview;
        }

        /// <summary>
        /// Trims chat history to stay within token limits by keeping only recent messages.
        /// </summary>
        private List<ChatMessage> GetTrimmedChatHistory()
        {
            if (chatHistory.Count <= MaxChatHistoryMessages)
            {
                return new List<ChatMessage>(chatHistory);
            }

            // Keep the most recent messages
            var trimmedHistory = chatHistory
                .Skip(chatHistory.Count - MaxChatHistoryMessages)
                .ToList();

            logger?.LogVerbose(
                $"Chat history trimmed from {chatHistory.Count} to {trimmedHistory.Count} messages");

            return trimmedHistory;
        }

        private AIFunction[] GetAvailableTools(AgentPhase phase = AgentPhase.All)
        {
            try
            {
                var tools = new List<AIFunction>();

                // Enumerate all tool executors and get their tools
                foreach (var executor in toolContainers)
                {
                    foreach (var (function, applicablePhases) in executor.GetTools())
                    {
                        // Filter by phase
                        if ((applicablePhases & phase) == 0)
                        {
                            continue; // Skip tools not applicable to this phase
                        }

                        // Wrap with monitoring
                        var monitoredFunction = new MonitoredAIFunction(function);
                        monitoredFunction.ToolCallStarted += OnToolCallStarted;
                        monitoredFunction.ToolCallCompleted += OnToolCallCompleted;
                        tools.Add(monitoredFunction);
                    }
                }

                logger?.LogVerbose($"Registered {tools.Count} tools for phase {phase}:");
                foreach (var tool in tools)
                {
                    logger?.LogVerbose($"  - {tool.Name}: {tool.Description ?? "(no description)"}");
                }

                return tools.ToArray();
            }
            catch (Exception ex)
            {
                logger?.LogError($"Error creating tools: {ex.Message}");
                logger?.LogVerbose(ex.StackTrace ?? "(no stack trace)");
                return Array.Empty<AIFunction>();
            }
        }

        private void OnToolCallStarted(object? sender, ToolCallInfo toolCallInfo)
        {
            // Raise event for UI consumption
            ToolCallExecuting?.Invoke(this, toolCallInfo);
        }

        private void OnToolCallCompleted(object? sender, ToolCallInfo toolCallInfo)
        {
            // Raise event for UI consumption
            ToolCallExecuted?.Invoke(this, toolCallInfo);
        }

        public async Task<string> SendMessageAsync(string userMessage, CancellationToken cancellationToken = default)
        {
            if (!IsConfigured)
            {
                var errorMsg = "LLM is not configured. Please set the required environment variables:\n" +
                             "- LLM_ENDPOINT (your Azure endpoint)\n" +
                             "- LLM_API_KEY (your API key)\n" +
                             "- LLM_MODEL (model name, e.g., gpt-4)\n\n" +
                             "Or use 'Configure' menu to configure/login.";
                
                MessageAdded?.Invoke(this, new ChatMessageViewModel("System", errorMsg, isError: true));
                return errorMsg;
            }

            if (string.IsNullOrWhiteSpace(userMessage))
            {
                return string.Empty;
            }

            // Add user message to UI
            MessageAdded?.Invoke(this, new ChatMessageViewModel("User", userMessage));

            // Add to chat history
            chatHistory.Add(new ChatMessage(ChatRole.User, userMessage));

            try
            {
                // Prepare messages with system prompt
                var messages = new List<ChatMessage>();
                
                // Add system prompt only at the start
                if (chatHistory.Count == 1)
                {
                    var systemPrompt = GetSystemPrompt();
                    if (currentSelectedNode != null && contextProvider is BinlogContextProvider provider)
                    {
                        systemPrompt += "\n\n" + provider.GetSelectedNodeContext(currentSelectedNode);
                    }
                    messages.Add(new ChatMessage(ChatRole.System, systemPrompt));
                }

                // Add trimmed chat history to stay within token limits
                var trimmedHistory = GetTrimmedChatHistory();
                messages.AddRange(trimmedHistory);

                // Get tools
                var tools = GetAvailableTools();

                // Send to LLM with tools
                // UseFunctionInvocation() in the client builder will automatically handle tool calls
                var options = new ChatOptions
                {
                    Tools = tools,
                    Temperature = 0.7f
                };

                logger?.LogVerbose($"ChatOptions.Tools count: {options.Tools?.Count ?? 0}");

                var response = await llmClient!.CompleteChatAsync(
                    messages, 
                    options, 
                    cancellationToken);

                // With UseFunctionInvocation(), the response already includes tool execution results
                var finalResponse = response.Text ?? string.Empty;

                // Add assistant response to history and UI
                chatHistory.Add(new ChatMessage(ChatRole.Assistant, finalResponse));
                MessageAdded?.Invoke(this, new ChatMessageViewModel("Assistant", finalResponse));

                return finalResponse;
            }
            catch (OperationCanceledException)
            {
                var cancelMsg = "Request cancelled.";
                MessageAdded?.Invoke(this, new ChatMessageViewModel("System", cancelMsg));
                return cancelMsg;
            }
            catch (Exception ex)
            {
                var errorMsg = $"Error: {ex.Message}";
                MessageAdded?.Invoke(this, new ChatMessageViewModel("System", errorMsg, isError: true));
                return errorMsg;
            }
        }

        public void Dispose()
        {
            if (llmClient is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}

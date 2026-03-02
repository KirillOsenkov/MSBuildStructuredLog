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
    /// Supports multiple binlog files through MultiBuildContext.
    /// </summary>
    public class LLMChatService : IDisposable
    {
        private readonly MultiBuildContext buildContext;
        private readonly MultiBuildContextProvider contextProvider;
        private readonly List<IToolsContainer> toolContainers;
        private MultiProviderLLMClient? llmClient;
        private readonly LLMConfiguration configuration;
        private readonly List<ChatMessage> chatHistory;
        private BaseNode? currentSelectedNode;
        private readonly ILLMLogger? logger;

        // Context compaction settings
        private const int MaxChatHistoryMessages = 20; // Keep recent context
        private const int CompactionThreshold = 16;    // Trigger compaction at this count
        private const int RecentMessagesToKeep = 6;    // Keep this many recent messages after compaction

        public event EventHandler<ChatMessageViewModel>? MessageAdded;
        public event EventHandler? ConversationCleared;
        public event EventHandler<ToolCallInfo>? ToolCallExecuting;
        public event EventHandler<ToolCallInfo>? ToolCallExecuted;
        public event EventHandler<ResilienceEventArgs>? RequestRetrying;
        public event EventHandler<string>? ConversationCompacted;

        public bool IsConfigured => configuration?.IsConfigured ?? false;
        public string ConfigurationStatus => configuration?.GetConfigurationStatus() ?? "Not initialized";

        /// <summary>
        /// Creates a new LLMChatService with multi-build support.
        /// </summary>
        private LLMChatService(MultiBuildContext context, LLMConfiguration config, ILLMLogger? logger)
        {
            this.buildContext = context ?? throw new ArgumentNullException(nameof(context));
            this.contextProvider = new MultiBuildContextProvider(context);
            this.toolContainers = new List<IToolsContainer>();
            this.chatHistory = new List<ChatMessage>();
            this.configuration = config;
            this.logger = logger;

            // Register default tool executors with multi-build context
            RegisterToolContainer(new BinlogToolExecutor(context));
            RegisterToolContainer(new EmbeddedFilesToolExecutor(context));
            RegisterToolContainer(new ListEventsToolExecutor(context));
            RegisterToolContainer(new ResultsToolExecutor());
        }

        /// <summary>
        /// Creates a new LLMChatService for a single build (backward compatibility).
        /// </summary>
        private LLMChatService(Build build, LLMConfiguration config, ILLMLogger? logger)
            : this(CreateSingleBuildContext(build), config, logger)
        {
        }

        private static MultiBuildContext CreateSingleBuildContext(Build build)
        {
            if (build == null)
            {
                throw new ArgumentNullException(nameof(build));
            }
            var context = new MultiBuildContext();
            context.AddBuild(build);
            return context;
        }

        /// <summary>
        /// Creates and initializes a new instance of LLMChatService with multi-build support.
        /// </summary>
        /// <param name="context">The multi-build context containing loaded builds.</param>
        /// <param name="config">Optional LLM configuration. If null, loads from environment.</param>
        /// <param name="logger">Optional logger for diagnostics.</param>
        /// <param name="cancellationToken">Cancellation token for async initialization.</param>
        /// <returns>A fully initialized LLMChatService instance.</returns>
        public static async System.Threading.Tasks.Task<LLMChatService> CreateAsync(
            MultiBuildContext context,
            LLMConfiguration? config = null,
            ILLMLogger? logger = null,
            CancellationToken cancellationToken = default)
        {
            var configuration = config ?? LLMConfiguration.LoadFromEnvironment();
            var service = new LLMChatService(context, configuration, logger);

            await service.InitializeLLMClientAsync(cancellationToken);

            return service;
        }

        /// <summary>
        /// Creates and initializes a new instance of LLMChatService for a single build.
        /// </summary>
        /// <param name="build">The build to analyze.</param>
        /// <param name="config">Optional LLM configuration. If null, loads from environment.</param>
        /// <param name="logger">Optional logger for diagnostics.</param>
        /// <param name="cancellationToken">Cancellation token for async initialization.</param>
        /// <returns>A fully initialized LLMChatService instance.</returns>
        public static async System.Threading.Tasks.Task<LLMChatService> CreateAsync(
            Build build,
            LLMConfiguration? config = null,
            ILLMLogger? logger = null,
            CancellationToken cancellationToken = default)
        {
            var configuration = config ?? LLMConfiguration.LoadFromEnvironment();
            var service = new LLMChatService(build, configuration, logger);

            await service.InitializeLLMClientAsync(cancellationToken);

            return service;
        }

        /// <summary>
        /// Adds a build to the context. Can be called after service creation.
        /// </summary>
        public string AddBuild(Build build, string? friendlyName = null)
        {
            return buildContext.AddBuild(build, friendlyName);
        }

        /// <summary>
        /// Removes a build from the context.
        /// </summary>
        public void RemoveBuild(string buildId)
        {
            buildContext.RemoveBuild(buildId);
        }

        /// <summary>
        /// Sets the primary build.
        /// </summary>
        public void SetPrimaryBuild(string buildId)
        {
            buildContext.SetPrimaryBuild(buildId);
        }

        /// <summary>
        /// Gets info about all loaded builds.
        /// </summary>
        public IEnumerable<BuildInfo> GetLoadedBuilds()
        {
            return buildContext.GetAllBuilds();
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

            // Prevent duplicate registration
            if (!toolContainers.Contains(container))
            {
                toolContainers.Add(container);
            }
        }

        private async System.Threading.Tasks.Task InitializeLLMClientAsync(CancellationToken cancellationToken)
        {
            if (!configuration.IsConfigured)
            {
                return;
            }

            try
            {
                var client = new MultiProviderLLMClient(configuration, logger: logger);
                await client.InitializeAsync(cancellationToken);
                this.llmClient = client;

                SubscribeToResilienceEvents(client);
            }
            catch (Exception ex)
            {
                logger?.LogError($"Failed to initialize LLM client: {ex.Message}");
                throw;
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

        /// <summary>
        /// Seeds the in-memory chat history from previously persisted entries.
        /// This allows the LLM to have context from prior conversations.
        /// </summary>
        public void SeedChatHistory(IEnumerable<ChatHistoryEntry> entries)
        {
            foreach (var entry in entries)
            {
                var role = entry.Role == "User" ? ChatRole.User : ChatRole.Assistant;
                chatHistory.Add(new ChatMessage(role, entry.Content));
            }
        }

        public LLMConfiguration GetConfiguration()
        {
            return configuration;
        }

        public async System.Threading.Tasks.Task ReconfigureAsync(LLMConfiguration newConfig, CancellationToken cancellationToken = default)
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
            await InitializeLLMClientAsync(cancellationToken);

            // Keep chat history - don't clear
        }

        private string GetSystemPrompt()
        {
            var overview = contextProvider.GetAllBuildsOverview();
            var buildCount = buildContext.BuildCount;

            string basePrompt;
            if (buildCount > 1)
            {
                basePrompt = $@"You are an expert assistant helping developers analyze MSBuild build logs (.binlog files).
Multiple binlog files are loaded. Always clarify which build you're referring to using the build ID or friendly name.

You have access to tools that can query build data including projects, targets, tasks, errors, warnings, and timing information.

IMPORTANT: Tools accept an optional `buildId` parameter to target a specific build. If omitted, tools operate on the PRIMARY build.

{overview}

Guidelines for multi-build analysis:
- Always mention which build your findings come from (e.g., ""In the Tests build..."")
- If a question could apply to multiple builds, check all relevant builds
- Use ListBuilds tool to see available builds and their IDs
- Consider comparing results across builds when relevant
- Be explicit about the build context to avoid confusion";
            }
            else
            {
                basePrompt = $@"You are an expert assistant helping developers analyze their MSBuild build log (.binlog file).
You have access to tools that can query the build data including projects, targets, tasks, errors, warnings, and timing information.
When the user asks questions, you must use the available tools to retrieve accurate information from the build log - as you do not have information about their builds in your training set.
Be concise and helpful. Format your responses clearly.

Available context:
{overview}";
            }

            if (HasGuiManipulationTools())
            {
                basePrompt += @"

You have access to GUI manipulation tools that can help users visualize and explore their build:
- Use tools like SelectNodeByTextAsync, SelectErrorAsync, SelectWarningAsync to navigate the UI to relevant nodes
- Use OpenTimelineAsync, OpenTracingAsync, PerformSearchAsync to switch views and help users explore data
- Use these tools proactively when they can help clarify or support your findings
- These tools make your responses interactive - leverage them to provide a better user experience

When answering questions, consider:
- Which errors/warnings should be highlighted for the user to see?
- What nodes or files would help illustrate the answer?
- Would timeline or tracing views provide useful context?
- Should the user see specific search results?

Use GUI tools to make your insights actionable and immediately explorable by the user.";
            }

            return basePrompt;
        }

        /// <summary>
        /// Trims chat history to stay within token limits by keeping only recent messages.
        /// Used as a fallback when compaction hasn't run yet.
        /// </summary>
        private List<ChatMessage> GetTrimmedChatHistory()
        {
            if (chatHistory.Count <= MaxChatHistoryMessages)
            {
                return chatHistory;
            }

            // Keep the most recent messages
            var trimmedHistory = chatHistory
                .Skip(chatHistory.Count - MaxChatHistoryMessages)
                .ToList();

            logger?.LogVerbose(
                $"Chat history trimmed from {chatHistory.Count} to {trimmedHistory.Count} messages");

            return trimmedHistory;
        }

        /// <summary>
        /// Compacts the conversation by summarizing older messages into a single summary.
        /// This preserves context while reducing token usage.
        /// </summary>
        private async System.Threading.Tasks.Task CompactChatHistoryAsync(CancellationToken cancellationToken)
        {
            if (llmClient == null || chatHistory.Count < CompactionThreshold)
            {
                return;
            }

            var messagesToSummarize = chatHistory.Count - RecentMessagesToKeep;
            if (messagesToSummarize <= 2)
            {
                return;
            }

            logger?.LogVerbose($"Compacting chat history: summarizing {messagesToSummarize} older messages, keeping {RecentMessagesToKeep} recent.");

            try
            {
                // Build the summarization request
                var olderMessages = chatHistory.Take(messagesToSummarize).ToList();

                var conversationText = string.Join("\n", olderMessages.Select(m =>
                {
                    var role = m.Role == ChatRole.User ? "User" : "Assistant";
                    return $"{role}: {m.Text}";
                }));

                var summaryPrompt = new List<ChatMessage>
                {
                    new ChatMessage(ChatRole.System,
                        "You are a conversation summarizer. Summarize the following conversation between a user and an assistant about MSBuild build logs. " +
                        "Preserve key facts, findings, errors discussed, and any conclusions. Be concise but retain all important technical details. " +
                        "Write the summary in third person (e.g., 'The user asked about..., The analysis found...')"),
                    new ChatMessage(ChatRole.User,
                        $"Please summarize this conversation:\n\n{conversationText}")
                };

                var options = new ChatOptions { Temperature = 0.3f };
                var response = await llmClient.CompleteChatAsync(summaryPrompt, options, cancellationToken);
                var summary = response.Text ?? string.Empty;

                if (string.IsNullOrWhiteSpace(summary))
                {
                    return;
                }

                // Replace older messages with a single summary message
                var recentMessages = chatHistory.Skip(messagesToSummarize).ToList();
                chatHistory.Clear();
                chatHistory.Add(new ChatMessage(ChatRole.Assistant,
                    $"[Conversation Summary]\n{summary}"));
                chatHistory.AddRange(recentMessages);

                logger?.LogVerbose($"Chat history compacted to {chatHistory.Count} messages (1 summary + {recentMessages.Count} recent).");

                ConversationCompacted?.Invoke(this, summary);
            }
            catch (Exception ex)
            {
                logger?.LogError($"Failed to compact chat history: {ex.Message}");
                // Compaction failure is non-critical - fall back to simple trimming
            }
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
                        var monitoredFunction = new MonitoredAIFunction(function, logger);
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

        /// <summary>
        /// Checks if any registered tool containers provide GUI manipulation tools.
        /// </summary>
        private bool HasGuiManipulationTools()
        {
            return toolContainers.Any(container => container.HasGuiTools);
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
                // Compact older messages into a summary if history is getting long
                await CompactChatHistoryAsync(cancellationToken);

                // Prepare messages with system prompt
                var messages = new List<ChatMessage>();

                // Add system prompt only at the start
                if (chatHistory.Count == 1)
                {
                    var systemPrompt = GetSystemPrompt();
                    if (currentSelectedNode != null)
                    {
                        systemPrompt += "\n\n" + contextProvider.GetSelectedNodeContext(currentSelectedNode, buildContext.PrimaryBuildId);
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

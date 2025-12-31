using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Logging.StructuredLogger;
using Microsoft.Extensions.AI;
using StructuredLogViewer.Controls;

namespace StructuredLogViewer.LLM
{
    /// <summary>
    /// Represents a chat message in the conversation.
    /// </summary>
    public class ChatMessageViewModel
    {
        public string Role { get; set; } // "User", "Assistant", "System"
        public string Content { get; set; }
        public DateTime Timestamp { get; set; }
        public bool IsError { get; set; }

        public ChatMessageViewModel(string role, string content, bool isError = false)
        {
            Role = role;
            Content = content;
            Timestamp = DateTime.Now;
            IsError = isError;
        }
    }

    /// <summary>
    /// Main service for LLM Chat functionality.
    /// Orchestrates chat history, LLM communication, and tool execution.
    /// </summary>
    public class LLMChatService : IDisposable
    {
        private readonly Build build;
        private readonly BinlogContextProvider contextProvider;
        private readonly AsyncBinlogToolExecutor toolExecutor;
        private readonly AsyncBinlogUIInteractionExecutor uiInteractionExecutor;
        private readonly AsyncEmbeddedFilesToolExecutor embeddedFilesExecutor;
        private AzureFoundryLLMClient llmClient;
        private readonly LLMConfiguration configuration;
        private readonly List<ChatMessage> chatHistory;
        private BaseNode currentSelectedNode;

        // Token management settings
        private const int MaxPromptTokens = 180000; // Leave buffer below 200k limit
        private const int EstimatedTokensPerMessage = 500; // Conservative estimate
        private const int MaxChatHistoryMessages = 20; // Keep recent context

        public event EventHandler<ChatMessageViewModel> MessageAdded;
        public event EventHandler ConversationCleared;
        public event EventHandler<ToolCallInfo> ToolCallExecuting;
        public event EventHandler<ToolCallInfo> ToolCallExecuted;
        public event EventHandler<ResilienceEventArgs> RequestRetrying;

        public bool IsConfigured => configuration?.IsConfigured ?? false;
        public string ConfigurationStatus => configuration?.GetConfigurationStatus() ?? "Not initialized";

        public LLMChatService(Build build, BuildControl buildControl)
        {
            this.build = build ?? throw new ArgumentNullException(nameof(build));
            this.contextProvider = new BinlogContextProvider(build);
            this.toolExecutor = new AsyncBinlogToolExecutor(build);
            this.uiInteractionExecutor = buildControl != null ? new AsyncBinlogUIInteractionExecutor(build, buildControl) : null;
            this.embeddedFilesExecutor = new AsyncEmbeddedFilesToolExecutor(build);
            this.chatHistory = new List<ChatMessage>();
            this.configuration = LLMConfiguration.LoadFromEnvironment();

            InitializeLLMClient();
        }

        private void InitializeLLMClient()
        {
            if (configuration.IsConfigured)
            {
                try
                {
                    llmClient = new AzureFoundryLLMClient(configuration);
                    
                    // Subscribe to resilience events
                    if (llmClient.ResilientClient != null)
                    {
                        llmClient.ResilientClient.RequestRetrying += (sender, e) => RequestRetrying?.Invoke(this, e);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to initialize LLM client: {ex.Message}");
                }
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
                throw new ArgumentNullException(nameof(newConfig));

            // Update configuration properties
            configuration.Endpoint = newConfig.Endpoint;
            configuration.ApiKey = newConfig.ApiKey;
            configuration.ModelName = newConfig.ModelName;
            configuration.AutoSendOnEnter = newConfig.AutoSendOnEnter;
            configuration.AgentMode = newConfig.AgentMode;
            configuration.UpdateType();

            // Dispose old client
            llmClient = null;

            // Reinitialize with new settings
            InitializeLLMClient();

            // Keep chat history - don't clear
        }

        private string GetSystemPrompt()
        {
            return @"You are an expert assistant helping developers analyze their MSBuild build logs (.binlog files).
You have access to tools that can query the build data including projects, targets, tasks, errors, warnings, and timing information.
When the user asks questions, you must use the available tools to retrieve accurate information from the build log - as you do not have information about their builds in your training set.
Be concise and helpful. Format your responses clearly.

Available context:
" + contextProvider.GetBuildOverview();
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

            System.Diagnostics.Debug.WriteLine(
                $"Chat history trimmed from {chatHistory.Count} to {trimmedHistory.Count} messages");

            return trimmedHistory;
        }

        private AIFunction[] GetAvailableTools()
        {
            var baseFunctions = new List<AIFunction>();

            try
            {
                // Register tools from BinlogToolExecutor - let AIFunctionFactory use the [Description] attributes
                baseFunctions.Add(AIFunctionFactory.Create(toolExecutor.GetBuildSummary));
                baseFunctions.Add(AIFunctionFactory.Create(toolExecutor.SearchNodes));
                baseFunctions.Add(AIFunctionFactory.Create(toolExecutor.GetErrorsAndWarnings));
                baseFunctions.Add(AIFunctionFactory.Create(toolExecutor.GetProjects));
                baseFunctions.Add(AIFunctionFactory.Create(toolExecutor.GetProjectTargets));
                // Embedded files tools
                baseFunctions.Add(AIFunctionFactory.Create(embeddedFilesExecutor.ListEmbeddedFiles));
                baseFunctions.Add(AIFunctionFactory.Create(embeddedFilesExecutor.SearchEmbeddedFiles));
                baseFunctions.Add(AIFunctionFactory.Create(embeddedFilesExecutor.ReadEmbeddedFileLines));

                // Register UI interaction tools if available
                if (uiInteractionExecutor != null)
                {
                    baseFunctions.Add(AIFunctionFactory.Create(uiInteractionExecutor.SelectNodeByText));
                    baseFunctions.Add(AIFunctionFactory.Create(uiInteractionExecutor.SelectError));
                    baseFunctions.Add(AIFunctionFactory.Create(uiInteractionExecutor.SelectWarning));
                    baseFunctions.Add(AIFunctionFactory.Create(uiInteractionExecutor.SelectProject));
                    baseFunctions.Add(AIFunctionFactory.Create(uiInteractionExecutor.OpenFile));
                    baseFunctions.Add(AIFunctionFactory.Create(uiInteractionExecutor.OpenTimeline));
                    baseFunctions.Add(AIFunctionFactory.Create(uiInteractionExecutor.OpenTracing));
                    baseFunctions.Add(AIFunctionFactory.Create(uiInteractionExecutor.PerformSearch));
                    baseFunctions.Add(AIFunctionFactory.Create(uiInteractionExecutor.OpenPropertiesAndItems));
                    baseFunctions.Add(AIFunctionFactory.Create(uiInteractionExecutor.OpenFindInFiles));
                    baseFunctions.Add(AIFunctionFactory.Create(uiInteractionExecutor.FocusSearch));
                }

                // Wrap all functions with monitoring
                var tools = new List<AIFunction>();
                foreach (var baseFunction in baseFunctions)
                {
                    var monitoredFunction = new MonitoredAIFunction(baseFunction);
                    monitoredFunction.ToolCallStarted += OnToolCallStarted;
                    monitoredFunction.ToolCallCompleted += OnToolCallCompleted;
                    tools.Add(monitoredFunction);
                }

                System.Diagnostics.Debug.WriteLine($"Registered {tools.Count} tools:");
                foreach (var tool in tools)
                {
                    System.Diagnostics.Debug.WriteLine($"  - {tool.Name}: {tool.Description}");
                }

                return tools.ToArray();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating tools: {ex.Message}");
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
                return Array.Empty<AIFunction>();
            }
        }

        private void OnToolCallStarted(object sender, ToolCallInfo toolCallInfo)
        {
            // Raise event for UI consumption
            ToolCallExecuting?.Invoke(this, toolCallInfo);
        }

        private void OnToolCallCompleted(object sender, ToolCallInfo toolCallInfo)
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
                             "- LLM_MODEL (model name, e.g., gpt-4)";
                
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
                    if (currentSelectedNode != null)
                    {
                        systemPrompt += "\n\n" + contextProvider.GetSelectedNodeContext(currentSelectedNode);
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

                System.Diagnostics.Debug.WriteLine($"ChatOptions.Tools count: {options.Tools?.Count ?? 0}");

                var response = await llmClient.ChatClient.GetResponseAsync(
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
            llmClient?.Dispose();
        }
    }
}

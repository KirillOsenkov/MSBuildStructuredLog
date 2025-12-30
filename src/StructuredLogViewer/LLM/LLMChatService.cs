using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Logging.StructuredLogger;
using Microsoft.Extensions.AI;

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
        private readonly BinlogToolExecutor toolExecutor;
        private AzureFoundryLLMClient llmClient;
        private readonly LLMConfiguration configuration;
        private readonly List<ChatMessage> chatHistory;
        private BaseNode currentSelectedNode;

        public event EventHandler<ChatMessageViewModel> MessageAdded;
        public event EventHandler ConversationCleared;

        public bool IsConfigured => configuration?.IsConfigured ?? false;
        public string ConfigurationStatus => configuration?.GetConfigurationStatus() ?? "Not initialized";

        public LLMChatService(Build build)
        {
            this.build = build ?? throw new ArgumentNullException(nameof(build));
            this.contextProvider = new BinlogContextProvider(build);
            this.toolExecutor = new BinlogToolExecutor(build);
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

        private string GetSystemPrompt()
        {
            return @"You are an expert assistant helping developers analyze MSBuild build logs (.binlog files).
You have access to tools that can query the build data including projects, targets, tasks, errors, warnings, and timing information.
When the user asks questions, use the available tools to retrieve accurate information from the build log.
Be concise and helpful. Format your responses clearly.

Available context:
" + contextProvider.GetBuildOverview();
        }

        private AIFunction[] GetAvailableTools()
        {
            var tools = new List<AIFunction>();

            try
            {
                // Register tools from BinlogToolExecutor
                tools.Add(AIFunctionFactory.Create(
                    toolExecutor.GetBuildSummary,
                    name: "GetBuildSummary",
                    description: "Gets a summary of the build including status, duration, errors and warnings count"));

                tools.Add(AIFunctionFactory.Create(
                    toolExecutor.SearchNodes,
                    name: "SearchNodes",
                    description: "Searches for nodes in the build tree by text or pattern"));

                tools.Add(AIFunctionFactory.Create(
                    toolExecutor.GetErrorsAndWarnings,
                    name: "GetErrorsAndWarnings",
                    description: "Gets all errors and/or warnings from the build"));

                tools.Add(AIFunctionFactory.Create(
                    toolExecutor.GetProjects,
                    name: "GetProjects",
                    description: "Gets list of all projects built with their status and duration"));

                tools.Add(AIFunctionFactory.Create(
                    toolExecutor.GetProjectTargets,
                    name: "GetProjectTargets",
                    description: "Gets targets executed in a specific project"));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating tools: {ex.Message}");
            }

            return tools.ToArray();
        }

        public async Task<string> SendMessageAsync(string userMessage, CancellationToken cancellationToken = default)
        {
            if (!IsConfigured)
            {
                var errorMsg = "LLM is not configured. Please set the required environment variables:\n" +
                             "- AZURE_FOUNDRY_ENDPOINT\n" +
                             "- AZURE_FOUNDRY_API_KEY\n" +
                             "- AZURE_FOUNDRY_MODEL_NAME (optional, defaults to gpt-4)";
                
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

                // Add all chat history
                messages.AddRange(chatHistory);

                // Get tools
                var tools = GetAvailableTools();

                // Send to LLM with tools
                var options = new ChatOptions
                {
                    Tools = tools,
                    Temperature = 0.7f
                };

                var response = await llmClient.ChatClient.CompleteAsync(
                    messages, 
                    options, 
                    cancellationToken);

                // Process response - handle tool calls
                string finalResponse = await ProcessResponseWithToolCalls(
                    response, 
                    messages, 
                    options,
                    cancellationToken);

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

        private async Task<string> ProcessResponseWithToolCalls(
            ChatCompletion response,
            List<ChatMessage> messages,
            ChatOptions options,
            CancellationToken cancellationToken,
            int maxIterations = 5)
        {
            var iterations = 0;
            var currentResponse = response;

            while (iterations < maxIterations)
            {
                // Check if response has tool calls
                var toolCalls = currentResponse.Message.Contents
                    .OfType<FunctionCallContent>()
                    .ToList();

                if (!toolCalls.Any())
                {
                    // No tool calls, return the text response
                    return currentResponse.Message.Text ?? string.Empty;
                }

                // Add assistant message with tool calls to history
                messages.Add(currentResponse.Message);

                // Execute each tool call
                foreach (var toolCall in toolCalls)
                {
                    try
                    {
                        // Try to find and invoke the matching function
                        var function = options.Tools?.Cast<AIFunction>().FirstOrDefault(t => 
                            t.Metadata?.Name == toolCall.Name);
                        
                        if (function != null)
                        {
                            // Parse arguments - FunctionCallContent.Arguments is typically a JsonElement or dictionary
                            var argsDict = new Dictionary<string, object>();
                            
                            // Invoke the function
                            var result = await function.InvokeAsync(argsDict, cancellationToken);
                            
                            // Add tool result to messages
                            var resultMessage = new ChatMessage(ChatRole.Tool, result?.ToString() ?? "null");
                            messages.Add(resultMessage);

                            System.Diagnostics.Debug.WriteLine($"Tool {toolCall.Name} returned: {result}");
                        }
                        else
                        {
                            var errorResult = $"Tool {toolCall.Name} not found";
                            messages.Add(new ChatMessage(ChatRole.Tool, errorResult));
                            System.Diagnostics.Debug.WriteLine(errorResult);
                        }
                    }
                    catch (Exception ex)
                    {
                        var errorResult = $"Error executing tool {toolCall.Name}: {ex.Message}";
                        messages.Add(new ChatMessage(ChatRole.Tool, errorResult));
                        System.Diagnostics.Debug.WriteLine(errorResult);
                    }
                }

                // Get next response from LLM with tool results
                currentResponse = await llmClient.ChatClient.CompleteAsync(
                    messages,
                    options,
                    cancellationToken);

                iterations++;
            }

            // Max iterations reached, return what we have
            return currentResponse.Message.Text ?? "Maximum tool call iterations reached.";
        }

        public void Dispose()
        {
            llmClient?.Dispose();
        }
    }
}

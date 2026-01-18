using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using StructuredLogger.LLM.Logging;

namespace StructuredLogger.LLM.Clients.GitHub
{
    /// <summary>
    /// GitHub Copilot chat client implementing IChatClient interface.
    /// Provides integration with Microsoft.Extensions.AI abstractions.
    /// </summary>
    public class GitHubCopilotChatClient : IChatClient
    {
        private readonly GitHubCopilotTokenProvider tokenProvider;
        private readonly string modelName;
        private readonly HttpClient httpClient;
        private readonly ILLMLogger logger;

        public ChatClientMetadata Metadata { get; }

        public GitHubCopilotChatClient(
            GitHubCopilotTokenProvider tokenProvider, 
            string modelName,
            ILLMLogger? logger = null)
        {
            this.tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
            this.modelName = modelName ?? throw new ArgumentNullException(nameof(modelName));
            this.httpClient = new HttpClient();
            this.logger = logger ?? NullLLMLogger.Instance;
            
            this.Metadata = new("GitHubCopilot", new Uri("https://api.githubcopilot.com"), modelName);
        }

        public async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            // Ensure token is fresh
            var copilotToken = await tokenProvider.RefreshIfNeededAsync(cancellationToken);

            // Build request
            var request = BuildRequest(messages, options, stream: false);

            // Send request
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{copilotToken.BaseUrl}/chat/completions");
            
            // Use TryAddWithoutValidation because Copilot token contains special characters (semicolons)
            httpRequest.Headers.TryAddWithoutValidation("Authorization", $"Bearer {copilotToken.Token}");
            httpRequest.Headers.TryAddWithoutValidation("Accept", "application/json");
            
            // Required Copilot API headers
            httpRequest.Headers.TryAddWithoutValidation("User-Agent", "MSBuildStructuredLogViewer/1.0");
            httpRequest.Headers.TryAddWithoutValidation("Editor-Version", "vscode/1.107.0");
            httpRequest.Headers.TryAddWithoutValidation("Editor-Plugin-Version", "copilot-chat/0.35.0");
            httpRequest.Headers.TryAddWithoutValidation("Copilot-Integration-Id", "vscode-chat");
            httpRequest.Headers.TryAddWithoutValidation("openai-intent", "conversation-panel");
            httpRequest.Headers.TryAddWithoutValidation("x-request-id", Guid.NewGuid().ToString());
            httpRequest.Headers.TryAddWithoutValidation("X-Initiator", "user");
            httpRequest.Content = new StringContent(
                JsonSerializer.Serialize(request),
                Encoding.UTF8,
                "application/json");

            var httpResponse = await httpClient.SendAsync(httpRequest, cancellationToken);
            
            if (!httpResponse.IsSuccessStatusCode)
            {
                var errorContent = await httpResponse.Content.ReadAsStringAsync();
                logger.LogError($"[GitHubCopilot] API Error {(int)httpResponse.StatusCode}: {errorContent}");
                logger.LogError($"[GitHubCopilot] Request had {request.Messages.Count} messages");
                throw new HttpRequestException($"GitHub Copilot API returned {(int)httpResponse.StatusCode} ({httpResponse.ReasonPhrase}): {errorContent}");
            }

            var responseJson = await httpResponse.Content.ReadAsStringAsync();
            var copilotResponse = JsonSerializer.Deserialize<ChatCompletionResponse>(responseJson);

            if (copilotResponse == null || copilotResponse.Choices.Count == 0)
            {
                throw new InvalidOperationException("Invalid response from GitHub Copilot");
            }

            // Convert to ChatResponse
            return ConvertToResponse(copilotResponse);
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // Ensure token is fresh
            var copilotToken = await tokenProvider.RefreshIfNeededAsync(cancellationToken);

            // Build request
            var request = BuildRequest(messages, options, stream: true);

            // Send request
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{copilotToken.BaseUrl}/chat/completions");
            
            // Use TryAddWithoutValidation because Copilot token contains special characters (semicolons)
            httpRequest.Headers.TryAddWithoutValidation("Authorization", $"Bearer {copilotToken.Token}");
            httpRequest.Headers.TryAddWithoutValidation("Accept", "text/event-stream");
            
            // Required Copilot API headers
            httpRequest.Headers.TryAddWithoutValidation("User-Agent", "MSBuildStructuredLogViewer/1.0");
            httpRequest.Headers.TryAddWithoutValidation("Editor-Version", "vscode/1.107.0");
            httpRequest.Headers.TryAddWithoutValidation("Editor-Plugin-Version", "copilot-chat/0.35.0");
            httpRequest.Headers.TryAddWithoutValidation("Copilot-Integration-Id", "vscode-chat");
            httpRequest.Headers.TryAddWithoutValidation("openai-intent", "conversation-panel");
            httpRequest.Headers.TryAddWithoutValidation("x-request-id", Guid.NewGuid().ToString());
            httpRequest.Headers.TryAddWithoutValidation("X-Initiator", "user");
            httpRequest.Content = new StringContent(
                JsonSerializer.Serialize(request),
                Encoding.UTF8,
                "application/json");

            var httpResponse = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            httpResponse.EnsureSuccessStatusCode();

            var stream = await httpResponse.Content.ReadAsStreamAsync();
            using var reader = new System.IO.StreamReader(stream);

            while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: "))
                {
                    continue;
                }

                var data = line.Substring("data: ".Length).Trim();
                if (data == "[DONE]")
                {
                    break;
                }

                ChatCompletionChunk? chunk;
                try
                {
                    chunk = JsonSerializer.Deserialize<ChatCompletionChunk>(data);
                }
                catch
                {
                    continue; // Skip malformed chunks
                }

                if (chunk == null || chunk.Choices.Count == 0)
                {
                    continue;
                }

                var update = ConvertToUpdate(chunk);
                if (update != null)
                {
                    yield return update;
                }
            }
        }

        private ChatCompletionRequest BuildRequest(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options,
            bool stream)
        {
            // Merge consecutive assistant messages to avoid API errors
            // The FunctionInvokingChatClient sometimes creates multiple assistant messages
            // for multiple tool calls, but the API requires they be combined into one
            var messagesList = messages.ToList();
            var mergedMessages = MergeConsecutiveAssistantMessages(messagesList).ToList();
            
            // Expand tool messages - each FunctionResultContent needs its own API message
            var expandedMessages = ExpandToolMessages(mergedMessages).ToList();
            
            // Debug logging
            logger.LogVerbose($"[GitHubCopilot] Messages before merge: {messagesList.Count}, after merge: {mergedMessages.Count}, after expand: {expandedMessages.Count}");
            for (int i = 0; i < Math.Min(expandedMessages.Count, 10); i++) // Limit to first 10 for brevity
            {
                var msg = expandedMessages[i];
                var toolCallCount = msg.Contents?.OfType<FunctionCallContent>().Count() ?? 0;
                var toolResultCount = msg.Contents?.OfType<FunctionResultContent>().Count() ?? 0;
                var hasText = !string.IsNullOrEmpty(msg.Text) || msg.Contents?.OfType<TextContent>().Any() == true;
                logger.LogVerbose($"  [{i}] Role: {msg.Role}, ToolCalls: {toolCallCount}, ToolResults: {toolResultCount}, HasText: {hasText}");
            }
            
            var request = new ChatCompletionRequest
            {
                Model = modelName,
                Messages = expandedMessages.Select(ConvertMessage).ToList(),
                Stream = stream
            };

            if (options != null)
            {
                request.Temperature = options.Temperature;
                request.TopP = options.TopP;
                request.MaxTokens = options.MaxOutputTokens;

                // Convert tools if present
                if (options.Tools != null && options.Tools.Count > 0)
                {
                    request.Tools = options.Tools.Select(ConvertTool).ToList();
                    
                    // Set tool_choice to "auto" to enable tool calling
                    request.ToolChoice = "auto";
                }
            }

            return request;
        }

        private IEnumerable<ChatMessage> MergeConsecutiveAssistantMessages(IEnumerable<ChatMessage> messages)
        {
            var result = new List<ChatMessage>();
            ChatMessage? pendingAssistant = null;
            var pendingToolCalls = new List<FunctionCallContent>();
            string? pendingText = null;

            foreach (var message in messages)
            {
                if (message.Role == ChatRole.Assistant)
                {
                    // Collect tool calls from this assistant message
                    var toolCalls = message.Contents?.OfType<FunctionCallContent>().ToList() ?? new List<FunctionCallContent>();
                    var textContent = message.Contents?.OfType<TextContent>().FirstOrDefault();
                    var messageText = textContent?.Text ?? message.Text;

                    if (toolCalls.Any() || !string.IsNullOrEmpty(messageText))
                    {
                        // Accumulate tool calls and text
                        pendingToolCalls.AddRange(toolCalls);
                        if (!string.IsNullOrEmpty(messageText))
                        {
                            // If we already have pending text, append with newline
                            if (!string.IsNullOrEmpty(pendingText))
                            {
                                pendingText += "\n" + messageText;
                            }
                            else
                            {
                                pendingText = messageText;
                            }
                        }
                        if (pendingAssistant == null)
                        {
                            pendingAssistant = message; // Keep first assistant for properties
                        }
                    }
                    // Skip empty assistant messages (neither text nor tool calls)
                }
                else
                {
                    // Non-assistant message: flush pending assistant message if any
                    if (pendingAssistant != null && (pendingToolCalls.Any() || !string.IsNullOrEmpty(pendingText)))
                    {
                        // Create merged assistant message
                        var contents = new List<AIContent>();
                        if (!string.IsNullOrEmpty(pendingText))
                        {
                            contents.Add(new TextContent(pendingText));
                        }
                        contents.AddRange(pendingToolCalls);

                        result.Add(new ChatMessage(ChatRole.Assistant, contents)
                        {
                            AdditionalProperties = pendingAssistant.AdditionalProperties
                        });

                        // Reset pending state
                        pendingAssistant = null;
                        pendingToolCalls.Clear();
                        pendingText = null;
                    }

                    // Add the non-assistant message
                    result.Add(message);
                }
            }

            // Flush any remaining assistant message
            if (pendingAssistant != null && (pendingToolCalls.Any() || !string.IsNullOrEmpty(pendingText)))
            {
                var contents = new List<AIContent>();
                if (!string.IsNullOrEmpty(pendingText))
                {
                    contents.Add(new TextContent(pendingText));
                }
                contents.AddRange(pendingToolCalls);

                result.Add(new ChatMessage(ChatRole.Assistant, contents)
                {
                    AdditionalProperties = pendingAssistant.AdditionalProperties
                });
            }

            return result;
        }

        private IEnumerable<ChatMessage> ExpandToolMessages(IEnumerable<ChatMessage> messages)
        {
            // Each FunctionResultContent must be a separate message with its own tool_call_id
            // The API requires one tool message per tool_call_id in the assistant's tool_calls
            foreach (var message in messages)
            {
                if (message.Role == ChatRole.Tool && message.Contents != null)
                {
                    var functionResults = message.Contents.OfType<FunctionResultContent>().ToList();
                    if (functionResults.Count > 1)
                    {
                        // Split into multiple messages, one per function result
                        foreach (var result in functionResults)
                        {
                            yield return new ChatMessage(ChatRole.Tool, new List<AIContent> { result })
                            {
                                AdditionalProperties = message.AdditionalProperties
                            };
                        }
                    }
                    else
                    {
                        // Single result or no results - keep as is
                        yield return message;
                    }
                }
                else
                {
                    // Non-tool message - keep as is
                    yield return message;
                }
            }
        }

        private ChatMessageDto ConvertMessage(ChatMessage message)
        {
            var dto = new ChatMessageDto
            {
                Role = message.Role.Value.ToLowerInvariant()
            };

            // Handle content
            if (message.Contents != null)
            {
                var textContent = message.Contents
                    .OfType<TextContent>()
                    .FirstOrDefault();

                if (textContent != null)
                {
                    dto.Content = textContent.Text;
                }
            }
            else if (message.Text != null)
            {
                dto.Content = message.Text;
            }

            // Handle tool calls
            var toolCalls = message.Contents?.OfType<FunctionCallContent>().ToList();
            if (toolCalls != null && toolCalls.Count > 0)
            {
                dto.ToolCalls = toolCalls.Select(tc => new ToolCallDto
                {
                    Id = tc.CallId ?? Guid.NewGuid().ToString(),
                    Type = "function",
                    Function = new FunctionCallDto
                    {
                        Name = tc.Name,
                        Arguments = JsonSerializer.Serialize(tc.Arguments)
                    }
                }).ToList();
            }

            // Handle tool results
            var toolResult = message.Contents?.OfType<FunctionResultContent>().FirstOrDefault();
            if (toolResult != null)
            {
                dto.ToolCallId = toolResult.CallId;
                dto.Content = toolResult.Result?.ToString() ?? string.Empty;
            }

            return dto;
        }

        private ToolDto ConvertTool(AITool tool)
        {
            if (tool is AIFunction function)
            {
                return new ToolDto
                {
                    Type = "function",
                    Function = new FunctionDto
                    {
                        Name = function.Name,
                        Description = function.Description,
                        Parameters = function.JsonSchema
                    }
                };
            }

            throw new NotSupportedException($"Tool type {tool.GetType().Name} is not supported");
        }

        private ChatResponse ConvertToResponse(ChatCompletionResponse copilotResponse)
        {
            var chatResponse = new ChatResponse(new List<ChatMessage>())
            {
                CreatedAt = copilotResponse.Created.HasValue ? DateTimeOffset.FromUnixTimeSeconds(copilotResponse.Created.Value) : null,
                ResponseId = copilotResponse.Id,
                ModelId = copilotResponse.Model
            };

            // Process all choices (not just the first one)
            if (copilotResponse.Choices != null)
            {
                foreach (var choice in copilotResponse.Choices)
                {
                    if (choice.Message == null)
                    {
                        continue;
                    }

                    var message = choice.Message;
                    var contentsList = new List<AIContent>();

                    // Handle tool calls first (they may come without text content)
                    if (message.ToolCalls != null && message.ToolCalls.Count > 0)
                    {
                        foreach (var toolCall in message.ToolCalls)
                        {
                            try
                            {
                                var arguments = JsonSerializer.Deserialize<IDictionary<string, object?>>(toolCall.Function.Arguments);
                                contentsList.Add(new FunctionCallContent(
                                    toolCall.Id,
                                    toolCall.Function.Name,
                                    arguments));
                            }
                            catch (JsonException ex)
                            {
                                logger?.LogError($"Failed to deserialize tool call arguments: {ex.Message}");
                            }
                        }
                    }

                    // Add text content if present (may be null when tool calls are present)
                    if (!string.IsNullOrEmpty(message.Content))
                    {
                        contentsList.Add(new TextContent(message.Content));
                    }

                    var chatMessage = new ChatMessage(new ChatRole(message.Role ?? "assistant"), contentsList);

                    // Add any additional properties from the choice to the message
                    if (choice.AdditionalProperties != null)
                    {
                        if (chatMessage.AdditionalProperties == null)
                        {
                            chatMessage.AdditionalProperties = new AdditionalPropertiesDictionary();
                        }
                        foreach (var kvp in choice.AdditionalProperties)
                        {
                            chatMessage.AdditionalProperties[kvp.Key] = kvp.Value;
                        }
                    }

                    // Preserve reasoning_opaque for round-tripping
                    if (!string.IsNullOrEmpty(message.ReasoningOpaque))
                    {
                        if (chatMessage.AdditionalProperties == null)
                        {
                            chatMessage.AdditionalProperties = new AdditionalPropertiesDictionary();
                        }
                        chatMessage.AdditionalProperties["reasoning_opaque"] = message.ReasoningOpaque;
                    }

                    // Add message additional properties
                    if (message.AdditionalProperties != null)
                    {
                        if (chatMessage.AdditionalProperties == null)
                        {
                            chatMessage.AdditionalProperties = new AdditionalPropertiesDictionary();
                        }
                        foreach (var kvp in message.AdditionalProperties)
                        {
                            chatMessage.AdditionalProperties[kvp.Key] = kvp.Value;
                        }
                    }

                    chatResponse.Messages.Add(chatMessage);
                }

                // Set the finish reason from the last choice (if any)
                var lastChoice = copilotResponse.Choices.LastOrDefault();
                if (lastChoice != null && !string.IsNullOrEmpty(lastChoice.FinishReason))
                {
                    chatResponse.FinishReason = ConvertFinishReason(lastChoice.FinishReason);
                }
            }

            // Add usage information
            if (copilotResponse.Usage != null)
            {
                chatResponse.Usage = new UsageDetails
                {
                    InputTokenCount = copilotResponse.Usage.PromptTokens,
                    OutputTokenCount = copilotResponse.Usage.CompletionTokens,
                    TotalTokenCount = copilotResponse.Usage.TotalTokens
                };
            }

            // Add system_fingerprint if present
            if (!string.IsNullOrEmpty(copilotResponse.SystemFingerprint))
            {
                if (chatResponse.AdditionalProperties == null)
                {
                    chatResponse.AdditionalProperties = new AdditionalPropertiesDictionary();
                }
                chatResponse.AdditionalProperties["system_fingerprint"] = copilotResponse.SystemFingerprint;
            }

            // Merge any additional properties from the API response
            if (copilotResponse.AdditionalProperties != null)
            {
                if (chatResponse.AdditionalProperties == null)
                {
                    chatResponse.AdditionalProperties = new AdditionalPropertiesDictionary();
                }
                foreach (var kvp in copilotResponse.AdditionalProperties)
                {
                    chatResponse.AdditionalProperties[kvp.Key] = kvp.Value;
                }
            }

            return chatResponse;
        }

        private ChatResponseUpdate? ConvertToUpdate(ChatCompletionChunk chunk)
        {
            var choice = chunk.Choices[0];
            var delta = choice.Delta;

            var contentsList = new List<AIContent>();
            
            if (!string.IsNullOrEmpty(delta.Content))
            {
                contentsList.Add(new TextContent(delta.Content));
            }

            // Handle tool calls in streaming
            if (delta.ToolCalls != null && delta.ToolCalls.Count > 0)
            {
                foreach (var toolCall in delta.ToolCalls)
                {
                    if (!string.IsNullOrEmpty(toolCall.Function.Name))
                    {
                        var arguments = string.IsNullOrEmpty(toolCall.Function.Arguments) 
                            ? new Dictionary<string, object?>() 
                            : JsonSerializer.Deserialize<IDictionary<string, object?>>(toolCall.Function.Arguments);
                        
                        contentsList.Add(new FunctionCallContent(
                            toolCall.Id,
                            toolCall.Function.Name,
                            arguments));
                    }
                }
            }

            ChatRole? role = null;
            if (!string.IsNullOrEmpty(delta.Role))
            {
                role = new ChatRole(delta.Role!);
            }

            var update = new ChatResponseUpdate
            {
                Role = role,
                Contents = contentsList,
                FinishReason = ConvertFinishReason(choice.FinishReason),
                ModelId = chunk.Model
            };

            return update;
        }

        private ChatFinishReason? ConvertFinishReason(string? finishReason)
        {
            return finishReason?.ToLowerInvariant() switch
            {
                "stop" => ChatFinishReason.Stop,
                "length" => ChatFinishReason.Length,
                "tool_calls" => ChatFinishReason.ToolCalls,
                "content_filter" => ChatFinishReason.ContentFilter,
                _ => null
            };
        }

        public void Dispose()
        {
            httpClient?.Dispose();
            tokenProvider?.Dispose();
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
        {
            return serviceType == typeof(GitHubCopilotTokenProvider) ? tokenProvider : null;
        }
    }
}

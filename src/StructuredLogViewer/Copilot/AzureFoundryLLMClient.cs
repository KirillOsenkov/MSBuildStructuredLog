using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.AI.Inference;
using Azure.AI.OpenAI;
using Azure.Core;
using Microsoft.Extensions.AI;

namespace StructuredLogViewer.Copilot
{
    /// <summary>
    /// Wrapper for Azure AI clients (OpenAI, Inference, or Anthropic) implementing IChatClient.
    /// </summary>
    public class AzureFoundryLLMClient : IDisposable
    {
        private readonly IChatClient chatClient;
        private readonly string modelName;
        private readonly ClientType clientType;
        private bool disposed;

        private enum ClientType
        {
            AzureOpenAI,
            AzureInference,
            Anthropic
        }

        public AzureFoundryLLMClient(CopilotConfiguration config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            if (!config.IsConfigured)
                throw new InvalidOperationException("Copilot configuration is incomplete.");

            modelName = config.ModelName;
            var endpoint = new Uri(config.Endpoint);
            var credential = new AzureKeyCredential(config.ApiKey);

            // Detect which client to use based on endpoint and model
            bool isAnthropic = config.Endpoint.Contains("/anthropic/", StringComparison.OrdinalIgnoreCase) ||
                              modelName.StartsWith("claude", StringComparison.OrdinalIgnoreCase);

            if (isAnthropic)
            {
                // Anthropic models in Azure AI Foundry
                // The Anthropic.SDK package doesn't easily support custom endpoints
                // Create a simple HTTP-based client wrapper instead
                clientType = ClientType.Anthropic;
                chatClient = new AnthropicHttpChatClient(config.Endpoint, config.ApiKey, modelName);
            }
            else if (config.UseAzureOpenAI)
            {
                // Azure OpenAI client
                clientType = ClientType.AzureOpenAI;
                var openAIClient = new AzureOpenAIClient(endpoint, credential);
                var openAIChatClient = openAIClient.GetChatClient(modelName);
                chatClient = openAIChatClient.AsChatClient();
            }
            else
            {
                // Azure AI Inference client for GitHub Models or other inference endpoints
                clientType = ClientType.AzureInference;
                var inferenceClient = new ChatCompletionsClient(endpoint, credential);
                chatClient = inferenceClient.AsChatClient(modelName);
            }
        }

        public IChatClient ChatClient => chatClient;

        public async Task<string> SendMessageAsync(
            string userMessage, 
            string systemPrompt = null,
            CancellationToken cancellationToken = default)
        {
            var messages = new List<ChatMessage>();

            if (!string.IsNullOrEmpty(systemPrompt))
            {
                messages.Add(new ChatMessage(Microsoft.Extensions.AI.ChatRole.System, systemPrompt));
            }

            messages.Add(new ChatMessage(Microsoft.Extensions.AI.ChatRole.User, userMessage));

            try
            {
                var response = await chatClient.CompleteAsync(messages, cancellationToken: cancellationToken);
                return response.Message.Text ?? string.Empty;
            }
            catch (Exception ex)
            {
                return $"Error communicating with LLM: {ex.Message}";
            }
        }

        public async Task<string> SendMessageWithToolsAsync(
            IList<ChatMessage> messages,
            AIFunction[] tools,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var options = new ChatOptions
                {
                    Tools = tools
                };

                var response = await chatClient.CompleteAsync(messages, options, cancellationToken);

                // Handle tool calls if present
                if (response.Message.Contents.Count > 0)
                {
                    foreach (var content in response.Message.Contents)
                    {
                        if (content is FunctionCallContent functionCall)
                        {
                            // Tool call detected - return info about it
                            return $"[Tool Call: {functionCall.Name}]";
                        }
                    }
                }

                return response.Message.Text ?? string.Empty;
            }
            catch (Exception ex)
            {
                return $"Error communicating with LLM: {ex.Message}";
            }
        }

        public void Dispose()
        {
            if (!disposed)
            {
                disposed = true;
            }
        }
    }

    /// <summary>
    /// Simple HTTP-based client for Anthropic models in Azure AI Foundry.
    /// </summary>
    internal class AnthropicHttpChatClient : IChatClient
    {
        private readonly string _endpoint;
        private readonly string _apiKey;
        private readonly string _modelName;
        private readonly System.Net.Http.HttpClient _httpClient;

        public AnthropicHttpChatClient(string endpoint, string apiKey, string modelName)
        {
            _endpoint = endpoint?.TrimEnd('/') ?? throw new ArgumentNullException(nameof(endpoint));
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            _modelName = modelName ?? throw new ArgumentNullException(nameof(modelName));
            _httpClient = new System.Net.Http.HttpClient();
        }

        public ChatClientMetadata Metadata => new ChatClientMetadata("Anthropic", new Uri("https://anthropic.com"), _modelName);

        public async Task<ChatCompletion> CompleteAsync(
            IList<Microsoft.Extensions.AI.ChatMessage> chatMessages,
            ChatOptions options = null,
            CancellationToken cancellationToken = default)
        {
            // Build Anthropic API request
            var messages = new List<object>();
            string systemPrompt = null;

            foreach (var msg in chatMessages)
            {
                if (msg.Role == Microsoft.Extensions.AI.ChatRole.System)
                {
                    systemPrompt = msg.Text;
                }
                else
                {
                    messages.Add(new
                    {
                        role = msg.Role.Value == "user" ? "user" : "assistant",
                        content = msg.Text
                    });
                }
            }

            var requestBody = new
            {
                model = _modelName,
                messages = messages,
                max_tokens = options?.MaxOutputTokens ?? 1024,
                system = systemPrompt
            };

            var json = System.Text.Json.JsonSerializer.Serialize(requestBody);
            var content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, $"{_endpoint}/v1/messages");
            request.Headers.Add("x-api-key", _apiKey);
            request.Headers.Add("anthropic-version", "2023-06-01");
            request.Content = content;

            var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            var responseObj = System.Text.Json.JsonDocument.Parse(responseJson);

            // Extract text from response
            var textContent = responseObj.RootElement
                .GetProperty("content")[0]
                .GetProperty("text")
                .GetString();

            var chatMessage = new Microsoft.Extensions.AI.ChatMessage(Microsoft.Extensions.AI.ChatRole.Assistant, textContent ?? string.Empty);
            return new ChatCompletion(chatMessage);
        }

        public IAsyncEnumerable<StreamingChatCompletionUpdate> CompleteStreamingAsync(
            IList<Microsoft.Extensions.AI.ChatMessage> chatMessages,
            ChatOptions options = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("Streaming not yet implemented for Anthropic HTTP client");
        }

        public TService GetService<TService>(object key = null) where TService : class
        {
            return this as TService;
        }

        object IChatClient.GetService(Type serviceType, object serviceKey)
        {
            return serviceType?.IsInstanceOfType(this) == true ? this : null;
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}

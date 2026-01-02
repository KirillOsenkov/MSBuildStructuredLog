using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Anthropic;
using Azure;
using Azure.AI.Inference;
using Azure.AI.OpenAI;
using Azure.Core;
using Microsoft.Extensions.AI;
using StructuredLogger.LLM.Clients.GitHub;
using StructuredLogger.LLM.Logging;

namespace StructuredLogger.LLM
{
    /// <summary>
    /// Multi-provider LLM client supporting Azure OpenAI, Azure AI Inference, Anthropic, and GitHub Copilot.
    /// Provides automatic retry and resilience logic.
    /// </summary>
    public class MultiProviderLLMClient : IDisposable
    {
        private IChatClient? chatClient;
        private ResilientChatClient? resilientClient;
        private readonly string modelName;
        private readonly LLMConfiguration.ClientType clientType;
        private readonly LLMConfiguration configuration;
        private readonly DeviceCodeCallback? deviceCodeCallback;
        private readonly ILLMLogger? logger;
        private bool isInitialized;

        /// <summary>
        /// Initializes a new MultiProviderLLMClient.
        /// For GitHub Copilot, call InitializeAsync() before using the client.
        /// For other providers, initialization is synchronous and happens in constructor.
        /// </summary>
        public MultiProviderLLMClient(
            LLMConfiguration config, 
            DeviceCodeCallback? deviceCodeCallback = null,
            ILLMLogger? logger = null)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            if (!config.IsConfigured)
            {
                throw new InvalidOperationException("LLM configuration is incomplete.");
            }

            this.configuration = config;
            this.modelName = config.ModelName;
            this.clientType = config.Type;
            this.deviceCodeCallback = deviceCodeCallback;
            this.logger = logger;

            // For non-GitHub Copilot providers, initialize synchronously
            if (config.Type != LLMConfiguration.ClientType.GitHubCopilot)
            {
                InitializeSynchronousClient(config);
                isInitialized = true;
            }
        }

        /// <summary>
        /// Initializes the client asynchronously.
        /// Required for GitHub Copilot (for token exchange and device flow).
        /// No-op for other providers (already initialized in constructor).
        /// </summary>
        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (isInitialized)
            {
                return; // Already initialized
            }

            if (configuration.Type == LLMConfiguration.ClientType.GitHubCopilot)
            {
                await InitializeGitHubCopilotAsync(cancellationToken);
                isInitialized = true;
            }
        }

        private void InitializeSynchronousClient(LLMConfiguration config)
        {
            var endpoint = new Uri(config.Endpoint);
            var credential = new AzureKeyCredential(config.ApiKey);

            if (config.Type == LLMConfiguration.ClientType.Anthropic)
            {
                chatClient = new AnthropicClient(
                    new Anthropic.Core.ClientOptions()
                    {
                        BaseUrl = endpoint,
                        APIKey = config.ApiKey,
                    })
                    .AsIChatClient(modelName);
            }
            else if (config.Type == LLMConfiguration.ClientType.AzureOpenAI)
            {
                var openAIClient = new AzureOpenAIClient(endpoint, credential);
                chatClient = openAIClient.GetChatClient(modelName).AsIChatClient();
            }
            else
            {
                var inferenceClient = new ChatCompletionsClient(endpoint, credential);
                chatClient = inferenceClient.AsIChatClient(modelName);
            }

            WrapWithResilienceAndFunctionInvocation();
        }

        private async Task InitializeGitHubCopilotAsync(CancellationToken cancellationToken)
        {
            var builder = GitHubCopilotClientBuilder.FromConfiguration(configuration);

            if (deviceCodeCallback != null)
            {
                builder.WithDeviceCodeCallback(deviceCodeCallback);
            }

            if (logger != null)
            {
                builder.WithLogger(logger);
            }

            chatClient = await builder.BuildAsync(cancellationToken);
            WrapWithResilienceAndFunctionInvocation();
        }

        private void WrapWithResilienceAndFunctionInvocation()
        {
            if (chatClient == null)
            {
                throw new InvalidOperationException("Chat client not initialized");
            }

            // Wrap with resilient client for automatic retry on rate limits and transient errors
            resilientClient = new ResilientChatClient(chatClient, maxRetries: 10);
            chatClient = resilientClient;
            
            // Apply function invocation after resilient wrapper
            chatClient = new ChatClientBuilder(chatClient).UseFunctionInvocation().Build();
        }

        public string ProviderName => clientType switch
        {
            LLMConfiguration.ClientType.AzureOpenAI => "Azure OpenAI",
            LLMConfiguration.ClientType.AzureInference => "Azure AI Inference",
            LLMConfiguration.ClientType.Anthropic => "Anthropic",
            LLMConfiguration.ClientType.GitHubCopilot => "GitHub Copilot",
            _ => "Unknown"
        };

        public IChatClient ChatClient
        {
            get
            {
                if (!isInitialized)
                {
                    throw new InvalidOperationException("Client not initialized. Call InitializeAsync() first for GitHub Copilot clients.");
                }
                return chatClient!;
            }
        }

        /// <summary>
        /// Access to the resilient client for event subscription
        /// </summary>
        public ResilientChatClient ResilientClient
        {
            get
            {
                if (!isInitialized)
                {
                    throw new InvalidOperationException("Client not initialized. Call InitializeAsync() first for GitHub Copilot clients.");
                }
                return resilientClient!;
            }
        }

        public async Task<ChatResponse> CompleteChatAsync(
            IList<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            if (!isInitialized)
            {
                throw new InvalidOperationException("Client not initialized. Call InitializeAsync() first for GitHub Copilot clients.");
            }
            return await chatClient!.GetResponseAsync(messages, options, cancellationToken);
        }

        public IAsyncEnumerable<ChatResponseUpdate> CompleteChatStreamingAsync(
            IList<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            if (!isInitialized)
            {
                throw new InvalidOperationException("Client not initialized. Call InitializeAsync() first for GitHub Copilot clients.");
            }
            return chatClient!.GetStreamingResponseAsync(messages, options, cancellationToken);
        }

        public void Dispose()
        {
            chatClient?.Dispose();
        }
    }
}

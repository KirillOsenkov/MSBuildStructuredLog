using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Anthropic;
using Azure;
using Azure.AI.Inference;
using Azure.AI.OpenAI;
using Azure.Core;
using Microsoft.Extensions.AI;

namespace StructuredLogViewer.LLM
{
    /// <summary>
    /// Wrapper for Azure AI clients (OpenAI, Inference, or Anthropic) implementing IChatClient.
    /// </summary>
    public class AzureFoundryLLMClient : IDisposable
    {
        private readonly IChatClient chatClient;
        private readonly string modelName;

        public AzureFoundryLLMClient(LLMConfiguration config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            if (!config.IsConfigured)
            {
                throw new InvalidOperationException("LLM configuration is incomplete.");
            }

            modelName = config.ModelName;
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

            // Wrap with resilient client for automatic retry on rate limits and transient errors
            chatClient = new ResilientChatClient(chatClient, maxRetries: 3);
            
            // Apply function invocation after resilient wrapper
            chatClient = new ChatClientBuilder(chatClient).UseFunctionInvocation().Build();
        }

        public IChatClient ChatClient => chatClient;

        public void Dispose()
        {
            chatClient.Dispose();
        }
    }
}

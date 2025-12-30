using System;

namespace StructuredLogViewer.LLM
{
    /// <summary>
    /// Configuration for LLM Chat integration.
    /// Reads settings from environment variables.
    /// Automatically detects which provider to use based on endpoint and model.
    /// </summary>
    public class LLMConfiguration
    {
        private const string LLMEndpointEnvVar = "LLM_ENDPOINT";
        private const string LLMApiKeyEnvVar = "LLM_API_KEY";
        private const string LLMModelEnvVar = "LLM_MODEL";

        public enum ClientType
        {
            Unknown,
            AzureOpenAI,
            AzureInference,
            Anthropic
        }

        public string Endpoint { get; set; }
        public string ApiKey { get; set; }
        public string ModelName { get; set; }
        public ClientType Type { get; set; }

        public bool IsConfigured => 
            !string.IsNullOrWhiteSpace(Endpoint) && 
            !string.IsNullOrWhiteSpace(ApiKey) && 
            !string.IsNullOrWhiteSpace(ModelName);

        public static LLMConfiguration LoadFromEnvironment()
        {
            var endpoint = Environment.GetEnvironmentVariable(LLMEndpointEnvVar);
            var apiKey = Environment.GetEnvironmentVariable(LLMApiKeyEnvVar);
            var model = Environment.GetEnvironmentVariable(LLMModelEnvVar);

            return new LLMConfiguration
            {
                Endpoint = endpoint,
                ApiKey = apiKey,
                ModelName = model ?? "gpt-4",
                Type = DetectClientType(endpoint, model)
            };
        }

        private static ClientType DetectClientType(string endpoint, string model)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
                return ClientType.Unknown;

            // Detect which client to use based on endpoint and model
            if (endpoint.Contains("/anthropic/", StringComparison.OrdinalIgnoreCase) ||
                              model.StartsWith("claude", StringComparison.OrdinalIgnoreCase))
            {
                return ClientType.Anthropic;
            }


            // Check endpoint domain for Azure OpenAI Service
            if (endpoint.Contains("cognitiveservices.azure.com", StringComparison.OrdinalIgnoreCase) ||
                endpoint.Contains("openai.azure.com", StringComparison.OrdinalIgnoreCase))
            {
                return ClientType.AzureOpenAI;
            }

            // Default: Azure AI Inference for other endpoints
            return ClientType.AzureInference;
        }

        public string GetConfigurationStatus()
        {
            if (IsConfigured)
            {
                var provider = Type switch
                {
                    ClientType.AzureOpenAI => "Azure OpenAI",
                    ClientType.AzureInference => "Azure AI Foundry/Inference",
                    ClientType.Anthropic => "Anthropic",
                    _ => "Unknown Provider"
                };

                return $"Connected to {ModelName} at {provider}";
            }

            return "LLM not configured. Set:\n" +
                   "  LLM_ENDPOINT (e.g., https://your-resource.openai.azure.com/)\n" +
                   "  LLM_API_KEY (your API key)\n" +
                   "  LLM_MODEL (e.g., gpt-4, claude-sonnet-4-5-2)\n\n" +
                   "The system will automatically detect the provider based on the endpoint.";
        }
    }
}

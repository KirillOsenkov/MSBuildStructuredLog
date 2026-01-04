using System;
using StructuredLogger.LLM.Logging;

namespace StructuredLogger.LLM
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
            Anthropic,
            GitHubCopilot
        }

        public string Endpoint { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string ModelName { get; set; } = string.Empty;
        public ClientType Type { get; private set; }
        public bool AutoSendOnEnter { get; set; } = true;
        public bool AgentMode { get; set; } = true;
        public LoggingLevel LoggingLevel { get; set; } = LoggingLevel.Normal;
        
        /// <summary>
        /// List of available models fetched from the provider (e.g., GitHub Copilot).
        /// Used to persist the model list across dialog reopenings.
        /// </summary>
        public System.Collections.Generic.List<string>? AvailableModels { get; set; }

        public LLMConfiguration()
        {
            // Default constructor
            AutoSendOnEnter = true;
            AgentMode = true;
            LoggingLevel = LoggingLevel.Normal;
        }

        public void UpdateType()
        {
            Type = DetectClientType(Endpoint, ModelName);
        }

        public bool IsConfigured
        {
            get
            {
                // Basic requirements
                if (string.IsNullOrWhiteSpace(Endpoint) || string.IsNullOrWhiteSpace(ModelName))
                {
                    return false;
                }

                // All other providers require an API key
                return !string.IsNullOrWhiteSpace(ApiKey);
            }
        }

        public static LLMConfiguration LoadFromEnvironment()
        {
            var endpoint = Environment.GetEnvironmentVariable(LLMEndpointEnvVar);
            var apiKey = Environment.GetEnvironmentVariable(LLMApiKeyEnvVar);
            var model = Environment.GetEnvironmentVariable(LLMModelEnvVar);

            var config = new LLMConfiguration
            {
                Endpoint = endpoint ?? string.Empty,
                ApiKey = apiKey ?? string.Empty,
                ModelName = model ?? GetDefaultModelForEndpoint(endpoint)
            };
            config.UpdateType();
            return config;
        }

        private static string GetDefaultModelForEndpoint(string? endpoint)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                return "gpt-4";
            }

            // GitHub Copilot uses Claude Sonnet 4.5 by default
            if (IsGitHubCopilotEndpoint(endpoint!))
            {
                return "claude-sonnet-4.5";
            }

            return "gpt-4";
        }

        private static ClientType DetectClientType(string endpoint, string model)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                return ClientType.Unknown;
            }

            // Detect GitHub Copilot
            if (IsGitHubCopilotEndpoint(endpoint))
            {
                return ClientType.GitHubCopilot;
            }

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

        private static bool IsGitHubCopilotEndpoint(string endpoint)
        {
            return endpoint.Contains("githubcopilot.com", StringComparison.OrdinalIgnoreCase) ||
                   endpoint.Equals("github-copilot", StringComparison.OrdinalIgnoreCase) ||
                   endpoint.Equals("copilot", StringComparison.OrdinalIgnoreCase);
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
                    ClientType.GitHubCopilot => "GitHub Copilot",
                    _ => "Unknown Provider"
                };

                return $"Connected to {ModelName} at {provider}";
            }

            return "LLM not configured. Set:\n" +
                   "  LLM_ENDPOINT (e.g., https://your-resource.openai.azure.com/ or 'github-copilot')\n" +
                   "  LLM_API_KEY (your API key or GitHub token, optional for Copilot device flow)\n" +
                   "  LLM_MODEL (e.g., gpt-4, claude-sonnet-4-5-2)\n\n" +
                   "The system will automatically detect the provider based on the endpoint.\n" +
                   "GUI/CLI provides additional configuration options.";
        }

        /// <summary>
        /// Loads configuration from persisted settings.
        /// </summary>
        public static LLMConfiguration LoadFromPersisted()
        {
            // This will be implemented in the UI layer that has access to SettingsService
            return new LLMConfiguration();
        }

        /// <summary>
        /// Saves configuration to persisted settings.
        /// </summary>
        public void SaveToPersisted()
        {
            // This will be implemented in the UI layer that has access to SettingsService
        }
    }
}

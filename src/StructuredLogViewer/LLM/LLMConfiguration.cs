using System;

namespace StructuredLogViewer.LLM
{
    /// <summary>
    /// Configuration for LLM Chat integration.
    /// Reads settings from environment variables.
    /// Supports both Azure OpenAI and Azure AI Foundry/Inference.
    /// </summary>
    public class LLMConfiguration
    {
        // Azure OpenAI (recommended for most users)
        private const string AzureOpenAIEndpointEnvVar = "AZURE_OPENAI_ENDPOINT";
        private const string AzureOpenAIApiKeyEnvVar = "AZURE_OPENAI_API_KEY";
        private const string AzureOpenAIDeploymentEnvVar = "AZURE_OPENAI_DEPLOYMENT";
        
        // Azure AI Foundry/Inference (alternative)
        private const string AzureFoundryEndpointEnvVar = "AZURE_FOUNDRY_ENDPOINT";
        private const string AzureFoundryApiKeyEnvVar = "AZURE_FOUNDRY_API_KEY";
        private const string AzureFoundryModelNameEnvVar = "AZURE_FOUNDRY_MODEL_NAME";

        public string Endpoint { get; set; }
        public string ApiKey { get; set; }
        public string ModelName { get; set; }
        public bool UseAzureOpenAI { get; set; }

        public bool IsConfigured => 
            !string.IsNullOrWhiteSpace(Endpoint) && 
            !string.IsNullOrWhiteSpace(ApiKey) && 
            !string.IsNullOrWhiteSpace(ModelName);

        public static LLMConfiguration LoadFromEnvironment()
        {
            // Try Azure OpenAI first (most common)
            var azureOpenAIEndpoint = Environment.GetEnvironmentVariable(AzureOpenAIEndpointEnvVar);
            var azureOpenAIKey = Environment.GetEnvironmentVariable(AzureOpenAIApiKeyEnvVar);
            var azureOpenAIDeployment = Environment.GetEnvironmentVariable(AzureOpenAIDeploymentEnvVar);

            if (!string.IsNullOrWhiteSpace(azureOpenAIEndpoint) && 
                !string.IsNullOrWhiteSpace(azureOpenAIKey))
            {
                return new LLMConfiguration
                {
                    Endpoint = azureOpenAIEndpoint,
                    ApiKey = azureOpenAIKey,
                    ModelName = azureOpenAIDeployment ?? "gpt-4",
                    UseAzureOpenAI = true
                };
            }

            // Fall back to Azure AI Foundry/Inference
            var foundryEndpoint = Environment.GetEnvironmentVariable(AzureFoundryEndpointEnvVar);
            var foundryKey = Environment.GetEnvironmentVariable(AzureFoundryApiKeyEnvVar);
            var foundryModel = Environment.GetEnvironmentVariable(AzureFoundryModelNameEnvVar);

            // Auto-detect: if endpoint contains "cognitiveservices.azure.com" or "openai.azure.com",
            // it's actually Azure OpenAI, not Azure AI Inference
            bool isActuallyOpenAI = !string.IsNullOrWhiteSpace(foundryEndpoint) &&
                                   (foundryEndpoint.Contains("cognitiveservices.azure.com", StringComparison.OrdinalIgnoreCase) ||
                                    foundryEndpoint.Contains("openai.azure.com", StringComparison.OrdinalIgnoreCase));

            return new LLMConfiguration
            {
                Endpoint = foundryEndpoint,
                ApiKey = foundryKey,
                ModelName = foundryModel ?? "gpt-4",
                UseAzureOpenAI = isActuallyOpenAI
            };
        }

        public string GetConfigurationStatus()
        {
            if (IsConfigured)
            {
                var provider = UseAzureOpenAI ? "Azure OpenAI" : "Azure AI Foundry";
                return $"Connected to {ModelName} at {provider}";
            }

            return "LLM not configured. Set either:\n" +
                   "  Azure OpenAI: AZURE_OPENAI_ENDPOINT, AZURE_OPENAI_API_KEY, AZURE_OPENAI_DEPLOYMENT\n" +
                   "  or Azure AI Foundry: AZURE_FOUNDRY_ENDPOINT, AZURE_FOUNDRY_API_KEY, AZURE_FOUNDRY_MODEL_NAME";
        }
    }
}

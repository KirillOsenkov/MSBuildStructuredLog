using System;
using System.Threading;
using System.Threading.Tasks;
using StructuredLogger.LLM.Logging;

namespace StructuredLogger.LLM.Clients.GitHub
{
    /// <summary>
    /// Builder for creating GitHub Copilot chat clients with proper initialization.
    /// </summary>
    public class GitHubCopilotClientBuilder
    {
        private string? githubToken;
        private string modelName = "claude-sonnet-4.5";
        private CopilotAccountType accountType = CopilotAccountType.Individual;
        private DeviceCodeCallback? deviceCodeCallback;
        private ILLMLogger? logger;

        /// <summary>
        /// Sets the GitHub access token (skips device flow if provided).
        /// </summary>
        public GitHubCopilotClientBuilder WithGitHubToken(string token)
        {
            this.githubToken = token;
            return this;
        }

        /// <summary>
        /// Sets the model name (default: claude-sonnet-4.5).
        /// </summary>
        public GitHubCopilotClientBuilder WithModel(string model)
        {
            this.modelName = model;
            return this;
        }

        /// <summary>
        /// Sets the Copilot account type (default: Individual).
        /// </summary>
        public GitHubCopilotClientBuilder WithAccountType(CopilotAccountType type)
        {
            this.accountType = type;
            return this;
        }

        /// <summary>
        /// Sets the device code callback for authentication UI.
        /// If not set, device code will be printed to console.
        /// </summary>
        public GitHubCopilotClientBuilder WithDeviceCodeCallback(DeviceCodeCallback callback)
        {
            this.deviceCodeCallback = callback;
            return this;
        }

        /// <summary>
        /// Sets the logger for debugging and error reporting.
        /// If not set, uses NullLLMLogger (silent).
        /// </summary>
        public GitHubCopilotClientBuilder WithLogger(ILLMLogger logger)
        {
            this.logger = logger;
            return this;
        }

        /// <summary>
        /// Builds and initializes the GitHub Copilot client.
        /// If no GitHub token is provided, initiates device flow authentication.
        /// </summary>
        public async Task<GitHubCopilotChatClient> BuildAsync(CancellationToken cancellationToken = default)
        {
            // If no token provided, use device flow
            if (string.IsNullOrWhiteSpace(githubToken))
            {
                using var authenticator = new GitHubDeviceFlowAuthenticator(deviceCodeCallback);
                githubToken = await authenticator.AuthenticateAsync(cancellationToken);
            }

            // Create token provider and get initial Copilot token
            var tokenProvider = new GitHubCopilotTokenProvider(githubToken!, accountType, logger);
            await tokenProvider.GetCopilotTokenAsync(cancellationToken);

            // Create client with logger
            return new GitHubCopilotChatClient(tokenProvider, modelName, logger);
        }

        /// <summary>
        /// Creates a builder from LLMConfiguration.
        /// </summary>
        public static GitHubCopilotClientBuilder FromConfiguration(LLMConfiguration config)
        {
            var builder = new GitHubCopilotClientBuilder()
                .WithModel(config.ModelName);

            if (!string.IsNullOrWhiteSpace(config.ApiKey))
            {
                builder.WithGitHubToken(config.ApiKey);
            }

            return builder;
        }
    }
}

using System;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace StructuredLogger.LLM.Clients.GitHub
{
    /// <summary>
    /// Manages GitHub Copilot tokens - exchanges GitHub access tokens for Copilot tokens
    /// and handles token refresh.
    /// </summary>
    public class GitHubCopilotTokenProvider : IDisposable
    {
        private const string CopilotTokenUrl = "https://api.github.com/copilot_internal/v2/token";
        private const string GitHubApiVersion = "2022-11-28";

        private readonly string githubAccessToken;
        private readonly CopilotAccountType accountType;
        private readonly HttpClient httpClient;
        private CopilotToken? currentToken;

        public event EventHandler<CopilotToken>? TokenRefreshed;

        /// <summary>
        /// Initializes a new instance of the GitHubCopilotTokenProvider.
        /// </summary>
        public GitHubCopilotTokenProvider(string githubAccessToken, CopilotAccountType accountType = CopilotAccountType.Individual)
        {
            if (string.IsNullOrWhiteSpace(githubAccessToken))
                throw new ArgumentException("GitHub access token is required.", nameof(githubAccessToken));

            this.githubAccessToken = githubAccessToken;
            this.accountType = accountType;
            this.httpClient = new HttpClient();
        }

        /// <summary>
        /// Gets a Copilot token by exchanging the GitHub access token.
        /// </summary>
        /// <exception cref="UnauthorizedAccessException">Thrown when the GitHub access token is invalid or expired.</exception>
        public async Task<CopilotToken> GetCopilotTokenAsync(CancellationToken cancellationToken = default)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, CopilotTokenUrl);
            request.Headers.Add("Authorization", $"Bearer {githubAccessToken}");
            request.Headers.Add("Accept", "application/json");
            request.Headers.Add("User-Agent", "MSBuildStructuredLogViewer");
            request.Headers.Add("X-GitHub-Api-Version", GitHubApiVersion);

            var response = await httpClient.SendAsync(request, cancellationToken);
            
            // Handle expired or invalid GitHub access token
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized || 
                response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                throw new UnauthorizedAccessException(
                    "GitHub access token is invalid or expired. Please re-authenticate using the GitHub Login button in the configuration dialog.");
            }
            
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var token = JsonSerializer.Deserialize<CopilotToken>(json);

            if (token == null)
            {
                throw new InvalidOperationException("Failed to deserialize Copilot token response");
            }

            // Extract base URL from token
            token.BaseUrl = ExtractBaseUrlFromToken(token.Token, accountType);

            currentToken = token;
            System.Diagnostics.Debug.WriteLine($"Copilot token obtained. Expires at: {token.ExpiresAt}");
            
            return token;
        }

        /// <summary>
        /// Extracts the base URL from the Copilot token's proxy-ep parameter.
        /// Token format: tid=...;exp=...;proxy-ep=proxy.individual.githubcopilot.com;...
        /// </summary>
        private string ExtractBaseUrlFromToken(string token, CopilotAccountType accountType)
        {
            // Try to extract from proxy-ep parameter
            var match = Regex.Match(token, @"proxy-ep=([^;]+)");
            if (match.Success)
            {
                var proxyHost = match.Groups[1].Value;
                // Convert proxy.X.githubcopilot.com to api.X.githubcopilot.com
                var apiHost = proxyHost.Replace("proxy.", "api.");
                return $"https://{apiHost}";
            }

            // Fallback based on account type
            return accountType switch
            {
                CopilotAccountType.Business => "https://api.business.githubcopilot.com",
                CopilotAccountType.Enterprise => "https://api.enterprise.githubcopilot.com",
                _ => "https://api.individual.githubcopilot.com"
            };
        }

        /// <summary>
        /// Gets the current Copilot token (may be expired).
        /// </summary>
        public CopilotToken? GetCurrentToken() => currentToken;

        /// <summary>
        /// Refreshes the token if needed.
        /// </summary>
        public async Task<CopilotToken> RefreshIfNeededAsync(CancellationToken cancellationToken = default)
        {
            if (currentToken == null || currentToken.IsExpired)
            {
                var newToken = await GetCopilotTokenAsync(cancellationToken);
                TokenRefreshed?.Invoke(this, newToken);
                return newToken;
            }

            return currentToken;
        }

        public void Dispose()
        {
            httpClient?.Dispose();
        }
    }
}

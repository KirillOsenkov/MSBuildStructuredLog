using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace StructuredLogger.LLM.Clients.GitHub
{
    /// <summary>
    /// Callback for displaying device code to user.
    /// Parameters: userCode, verificationUrl
    /// </summary>
    public delegate void DeviceCodeCallback(string userCode, string verificationUrl);

    /// <summary>
    /// Implements GitHub OAuth Device Code Flow for authentication.
    /// See: https://docs.github.com/en/apps/oauth-apps/building-oauth-apps/authorizing-oauth-apps#device-flow
    /// </summary>
    public class GitHubDeviceFlowAuthenticator : IDisposable
    {
        private const string GitHubClientId = "Iv1" + "." + "b507" + "a08c87ecfe98";
        private const string GitHubDeviceCodeUrl = "https://github.com/login/device/code";
        private const string GitHubAccessTokenUrl = "https://github.com/login/oauth/access_token";

        private readonly DeviceCodeCallback? deviceCodeCallback;
        private readonly HttpClient httpClient;

        /// <summary>
        /// Initializes a new instance of the GitHubDeviceFlowAuthenticator.
        /// </summary>
        /// <param name="deviceCodeCallback">Optional callback invoked with (userCode, verificationUrl) to display to user. If null, info is written to console.</param>
        public GitHubDeviceFlowAuthenticator(DeviceCodeCallback? deviceCodeCallback = null)
        {
            this.deviceCodeCallback = deviceCodeCallback;
            this.httpClient = new HttpClient();
        }

        /// <summary>
        /// Authenticates user via device code flow.
        /// </summary>
        public async Task<string> AuthenticateAsync(CancellationToken cancellationToken = default)
        {
            // Step 1: Request device code
            var deviceCodeResponse = await RequestDeviceCodeAsync(cancellationToken);

            // Step 2: Display device code to user
            if (deviceCodeCallback != null)
            {
                deviceCodeCallback(deviceCodeResponse.UserCode, deviceCodeResponse.VerificationUri);
            }
            else
            {
                // Fallback to console output
                Console.WriteLine();
                Console.WriteLine("=".PadRight(70, '='));
                Console.WriteLine("GitHub Authentication Required");
                Console.WriteLine("=".PadRight(70, '='));
                Console.WriteLine();
                Console.WriteLine($"Please visit: {deviceCodeResponse.VerificationUri}");
                Console.WriteLine($"And enter code: {deviceCodeResponse.UserCode}");
                Console.WriteLine();
                Console.WriteLine("Waiting for authorization...");
                Console.WriteLine();
            }

            // Step 3: Poll for access token
            var accessToken = await PollForAccessTokenAsync(deviceCodeResponse, cancellationToken);

            return accessToken;
        }

        /// <summary>
        /// Requests a device code from GitHub.
        /// </summary>
        private async Task<DeviceCodeResponse> RequestDeviceCodeAsync(CancellationToken cancellationToken)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, GitHubDeviceCodeUrl);
            request.Headers.Add("Accept", "application/json");

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", GitHubClientId),
                new KeyValuePair<string, string>("scope", "read:user")
            });
            request.Content = content;

            var response = await httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var deviceCodeResponse = JsonSerializer.Deserialize<DeviceCodeResponse>(json);
            
            if (deviceCodeResponse == null)
            {
                throw new InvalidOperationException("Failed to deserialize device code response");
            }

            return deviceCodeResponse;
        }

        /// <summary>
        /// Polls GitHub for access token after user authorizes.
        /// </summary>
        private async Task<string> PollForAccessTokenAsync(DeviceCodeResponse deviceCode, CancellationToken cancellationToken)
        {
            var expiresAt = DateTimeOffset.UtcNow.AddSeconds(deviceCode.ExpiresIn);
            var intervalMs = deviceCode.Interval * 1000;

            while (DateTimeOffset.UtcNow < expiresAt)
            {
                await Task.Delay(intervalMs, cancellationToken);

                var request = new HttpRequestMessage(HttpMethod.Post, GitHubAccessTokenUrl);
                request.Headers.Add("Accept", "application/json");

                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("client_id", GitHubClientId),
                    new KeyValuePair<string, string>("device_code", deviceCode.DeviceCode),
                    new KeyValuePair<string, string>("grant_type", "urn:ietf:params:oauth:grant-type:device_code")
                });
                request.Content = content;

                var response = await httpClient.SendAsync(request, cancellationToken);
                var json = await response.Content.ReadAsStringAsync();
                var tokenResponse = JsonSerializer.Deserialize<AccessTokenResponse>(json);

                if (tokenResponse == null)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(tokenResponse.AccessToken))
                {
                    return tokenResponse.AccessToken!;
                }

                if (!string.IsNullOrEmpty(tokenResponse.Error))
                {
                    if (tokenResponse.Error == "authorization_pending")
                    {
                        // User hasn't authorized yet, continue polling
                        continue;
                    }
                    else if (tokenResponse.Error == "slow_down")
                    {
                        // Increase interval by 5 seconds
                        intervalMs += 5000;
                        continue;
                    }
                    else if (tokenResponse.Error == "expired_token")
                    {
                        throw new InvalidOperationException("Device code expired. Please try again.");
                    }
                    else if (tokenResponse.Error == "access_denied")
                    {
                        throw new InvalidOperationException("User denied authorization.");
                    }
                    else
                    {
                        throw new InvalidOperationException($"GitHub returned error: {tokenResponse.Error} - {tokenResponse.ErrorDescription}");
                    }
                }
            }

            throw new TimeoutException("Device code expired before user authorized.");
        }

        public void Dispose()
        {
            httpClient?.Dispose();
        }
    }
}

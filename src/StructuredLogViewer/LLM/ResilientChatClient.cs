using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using Anthropic.Exceptions;
using Azure;

namespace StructuredLogViewer.LLM
{
    /// <summary>
    /// Wraps an IChatClient to provide automatic retry with exponential backoff for recoverable errors.
    /// Handles rate limit errors and transient failures.
    /// </summary>
    public class ResilientChatClient : IChatClient
    {
        private readonly IChatClient innerClient;
        private readonly int maxRetries;
        private readonly TimeSpan initialDelay;
        private readonly TimeSpan maxDelay;

        public ResilientChatClient(IChatClient innerClient, int maxRetries = 3, TimeSpan? initialDelay = null, TimeSpan? maxDelay = null)
        {
            this.innerClient = innerClient ?? throw new ArgumentNullException(nameof(innerClient));
            this.maxRetries = maxRetries;
            this.initialDelay = initialDelay ?? TimeSpan.FromSeconds(1);
            this.maxDelay = maxDelay ?? TimeSpan.FromMinutes(2);
        }

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions options = null, CancellationToken cancellationToken = default)
        {
            return ExecuteWithRetryAsync(
                () => innerClient.GetResponseAsync(messages, options, cancellationToken),
                cancellationToken);
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions options = null, CancellationToken cancellationToken = default)
        {
            // Note: Streaming doesn't benefit as much from retry logic since it's already started,
            // but we wrap it for consistency
            return innerClient.GetStreamingResponseAsync(messages, options, cancellationToken);
        }

        public void Dispose()
        {
            innerClient?.Dispose();
        }

        public object GetService(Type serviceType, object serviceKey = null)
        {
            return innerClient.GetService(serviceType, serviceKey);
        }

        private async Task<ChatResponse> ExecuteWithRetryAsync(Func<Task<ChatResponse>> operation, CancellationToken cancellationToken)
        {
            int attempt = 0;
            TimeSpan delay = initialDelay;

            while (true)
            {
                try
                {
                    return await operation();
                }
                catch (Exception ex) when (IsRetryable(ex) && attempt < maxRetries)
                {
                    attempt++;
                    
                    // Try to extract wait time from the error message
                    var waitTime = ExtractWaitTimeFromError(ex);
                    if (waitTime.HasValue)
                    {
                        delay = waitTime.Value;
                        System.Diagnostics.Debug.WriteLine($"Rate limit hit. Waiting {delay.TotalSeconds}s as specified in error (attempt {attempt}/{maxRetries})");
                    }
                    else
                    {
                        // Use exponential backoff
                        delay = TimeSpan.FromSeconds(Math.Min(
                            initialDelay.TotalSeconds * Math.Pow(2, attempt - 1),
                            maxDelay.TotalSeconds));
                        System.Diagnostics.Debug.WriteLine($"Retryable error encountered. Waiting {delay.TotalSeconds}s before retry (attempt {attempt}/{maxRetries}): {ex.Message}");
                    }

                    // Check if delay would exceed cancellation
                    try
                    {
                        await Task.Delay(delay, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        // If cancelled during delay, re-throw
                        throw;
                    }

                    // Continue to next attempt
                    continue;
                }
                // If not retryable or max retries exceeded, throw
            }
        }

        /// <summary>
        /// Determines if an exception is retryable.
        /// </summary>
        private bool IsRetryable(Exception ex)
        {
            // Anthropic rate limit exceptions
            if (ex is AnthropicRateLimitException)
            {
                return true;
            }

            // Azure RequestFailedException with rate limit status codes
            if (ex is RequestFailedException requestEx)
            {
                return requestEx.Status == 429 || // Too Many Requests
                       requestEx.Status == 503 || // Service Unavailable
                       requestEx.Status == 504 || // Gateway Timeout
                       requestEx.Status == 402 || // Payment Required
                       requestEx.Status == 429 || // Too Many Requests
                       requestEx.Status == 502;   // Bad Gateway
            }

            // Check for HTTP status code in message (fallback)
            if (ex.Message.Contains("429") || ex.Message.Contains("Rate limit", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Transient network errors
            if (ex is System.Net.Http.HttpRequestException ||
                ex is System.Net.Sockets.SocketException ||
                ex is TimeoutException)
            {
                return true;
            }

            // Check inner exception
            if (ex.InnerException != null)
            {
                return IsRetryable(ex.InnerException);
            }

            return false;
        }

        /// <summary>
        /// Attempts to extract the wait time from rate limit error messages.
        /// </summary>
        private TimeSpan? ExtractWaitTimeFromError(Exception ex)
        {
            var message = ex.Message;

            // Look for patterns like "wait 59 seconds" or "wait for 59s" or "retry after 60 seconds"
            var patterns = new[]
            {
                @"wait\s+(\d+)\s+seconds?",
                @"wait\s+for\s+(\d+)\s*s\b",
                @"retry\s+after\s+(\d+)\s+seconds?",
                @"try\s+again\s+in\s+(\d+)\s+seconds?",
                @"Please wait (\d+) seconds before retrying"
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(message, pattern, RegexOptions.IgnoreCase);
                if (match.Success && int.TryParse(match.Groups[1].Value, out int seconds))
                {
                    // Add a small buffer (1 second) to ensure we're past the limit
                    return TimeSpan.FromSeconds(seconds + 1);
                }
            }

            // Check for Retry-After header value in Azure exceptions
            if (ex is RequestFailedException requestEx && requestEx.Status == 429)
            {
                // Azure SDK may include retry-after in the exception details
                // This is a best-effort attempt
            }

            return null;
        }
    }
}

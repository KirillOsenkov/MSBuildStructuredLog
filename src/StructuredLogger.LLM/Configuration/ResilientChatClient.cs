using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Anthropic.Exceptions;
using Azure;
using Microsoft.Build.Logging.StructuredLogger;
using Microsoft.Extensions.AI;
using StructuredLogger.LLM.Logging;

namespace StructuredLogger.LLM
{
    /// <summary>
    /// Type of resilience action being taken
    /// </summary>
    public enum ResilienceType
    {
        Throttling,
        ContextTrimming
    }

    /// <summary>
    /// Event args for resilience events
    /// </summary>
    public class ResilienceEventArgs : EventArgs
    {
        public string Message { get; set; } = string.Empty;
        public int Attempt { get; set; }
        public int MaxAttempts { get; set; }
        public ResilienceType Type { get; set; }
    }

    /// <summary>
    /// Wraps an IChatClient to provide automatic retry with exponential backoff for recoverable errors.
    /// Handles rate limit errors, transient failures, and context overflow with intelligent truncation.
    /// </summary>
    public class ResilientChatClient : IChatClient
    {
        private readonly IChatClient innerClient;
        private readonly int maxRetries;
        private readonly TimeSpan initialDelay;
        private readonly TimeSpan maxDelay;
        private readonly ILLMLogger? logger;

        /// <summary>
        /// Raised when the client is retrying a request due to recoverable errors
        /// </summary>
        public event EventHandler<ResilienceEventArgs>? RequestRetrying;

        public ResilientChatClient(IChatClient innerClient, int maxRetries = 10, TimeSpan? initialDelay = null, TimeSpan? maxDelay = null, ILLMLogger? logger = null)
        {
            this.innerClient = innerClient ?? throw new ArgumentNullException(nameof(innerClient));
            this.maxRetries = maxRetries;
            this.initialDelay = initialDelay ?? TimeSpan.FromSeconds(1);
            this.maxDelay = maxDelay ?? TimeSpan.FromMinutes(2);
            this.logger = logger;
        }

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            return ExecuteWithRetryAsync(
                messages,
                options ?? new ChatOptions(),
                cancellationToken);
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            // Note: Streaming doesn't benefit as much from retry logic since it's already started,
            // but we wrap it for consistency
            return innerClient.GetStreamingResponseAsync(messages, options, cancellationToken);
        }

        public void Dispose()
        {
            innerClient?.Dispose();
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
        {
            return innerClient.GetService(serviceType, serviceKey);
        }

        private async Task<ChatResponse> ExecuteWithRetryAsync(
            IEnumerable<ChatMessage> messages, 
            ChatOptions options, 
            CancellationToken cancellationToken)
        {
            int attempt = 0;
            TimeSpan delay = initialDelay;
            var currentMessages = messages.ToList(); // Convert to list for potential truncation

            while (true)
            {
                try
                {
                    return await innerClient.GetResponseAsync(currentMessages, options, cancellationToken);
                }
                catch (Exception ex) when (attempt < maxRetries && ex is not OperationCanceledException && !cancellationToken.IsCancellationRequested)
                {
                    // Check if this is a context overflow error
                    var contextOverflow = ExtractContextOverflowInfo(ex, currentMessages);
                    if (contextOverflow.IsOverflow)
                    {
                        attempt++;
                        logger?.LogInfo(
                            $"Context overflow detected: {contextOverflow.CurrentTokens} > {contextOverflow.MaxTokens} " +
                            $"(attempt {attempt}/{maxRetries})");

                        // Raise context trimming event
                        RequestRetrying?.Invoke(this, new ResilienceEventArgs
                        {
                            Message = "Context trimmed",
                            Attempt = attempt,
                            MaxAttempts = maxRetries,
                            Type = ResilienceType.ContextTrimming
                        });

                        // Truncate messages to fit within limit
                        currentMessages = TruncateMessages(
                            currentMessages, 
                            contextOverflow.MaxTokens, 
                            contextOverflow.CurrentTokens);

                        if (currentMessages.Count == 0)
                        {
                            logger?.LogError("Cannot truncate messages further. Throwing exception.");
                            throw;
                        }

                        logger?.LogInfo(
                            $"Retrying with {currentMessages.Count} messages (estimated {EstimateTokens(currentMessages)} tokens)");
                        
                        // Small delay before retry
                        await System.Threading.Tasks.Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
                        continue;
                    }

                    // Handle other retryable errors (rate limits, transient failures)
                    if (IsRetryable(ex))
                    {
                        attempt++;
                        
                        // Try to extract wait time from the error message
                        var waitTime = ExtractWaitTimeFromError(ex);
                        if (waitTime.HasValue)
                        {
                            delay = waitTime.Value;
                            logger?.LogInfo($"Rate limit hit. Waiting {delay.TotalSeconds}s as specified in error (attempt {attempt}/{maxRetries})");
                        }
                        else
                        {
                            // Use exponential backoff
                            delay = TimeSpan.FromSeconds(Math.Min(
                                initialDelay.TotalSeconds * Math.Pow(2, attempt - 1),
                                maxDelay.TotalSeconds));
                            logger?.LogInfo($"Retryable error encountered. Waiting {delay.TotalSeconds}s before retry (attempt {attempt}/{maxRetries}): {ex.Message}");
                        }

                        // Raise throttling event
                        RequestRetrying?.Invoke(this, new ResilienceEventArgs
                        {
                            Message = "Throttling requests",
                            Attempt = attempt,
                            MaxAttempts = maxRetries,
                            Type = ResilienceType.Throttling
                        });

                        // Check if delay would exceed cancellation
                        try
                        {
                            await System.Threading.Tasks.Task.Delay(delay, cancellationToken);
                        }
                        catch (OperationCanceledException)
                        {
                            // If cancelled during delay, re-throw
                            throw;
                        }

                        // Continue to next attempt
                        continue;
                    }

                    // If not retryable, throw
                    throw;
                }
                // If max retries exceeded, the loop will throw the exception
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
            if (ex.Message.Contains("429") || ex.Message.ContainsIgnoreCase("Rate limit"))
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

        /// <summary>
        /// Context overflow information extracted from an exception.
        /// </summary>
        private struct ContextOverflowInfo
        {
            public bool IsOverflow { get; set; }
            public int CurrentTokens { get; set; }
            public int MaxTokens { get; set; }
        }

        /// <summary>
        /// Pattern definition for extracting token overflow information from error messages.
        /// </summary>
        private readonly struct OverflowPattern
        {
            public string Regex { get; }
            public int CurrentTokensGroup { get; }
            public int MaxTokensGroup { get; }

            public OverflowPattern(string regex, int currentTokensGroup, int maxTokensGroup)
            {
                Regex = regex;
                CurrentTokensGroup = currentTokensGroup;
                MaxTokensGroup = maxTokensGroup;
            }
        }

        /// <summary>
        /// Patterns for detecting context overflow errors from various LLM providers.
        /// </summary>
        private static readonly OverflowPattern[] OverflowPatterns =
        {
            // Anthropic: "prompt is too long: 216483 tokens > 200000 maximum"
            new(@"prompt is too long:\s*(\d+)\s*tokens?\s*>\s*(\d+)\s*maximum", 1, 2),
            // GitHub Copilot: "prompt token count of 795491 exceeds the limit of 128000"
            new(@"prompt token count of\s*(\d+)\s*exceeds the limit of\s*(\d+)", 1, 2),
            // OpenAI: "maximum context length is 128000 tokens"
            new(@"maximum context length is\s*(\d+)\s*tokens?", -1, 1),
            // OpenAI: "context length of 150000 exceeds"
            new(@"context length of\s*(\d+)\s*exceeds", 1, -1),
        };

        /// <summary>
        /// Extracts context overflow information from an exception.
        /// </summary>
        private ContextOverflowInfo ExtractContextOverflowInfo(Exception ex, List<ChatMessage> messages)
        {
            var message = ex.Message;

            // Try known patterns first
            foreach (var pattern in OverflowPatterns)
            {
                var result = TryMatchOverflowPattern(message, pattern, messages);
                if (result.IsOverflow)
                {
                    return result;
                }
            }

            // Check for model_max_prompt_tokens_exceeded code
            if (message.ContainsIgnoreCase("model_max_prompt_tokens_exceeded"))
            {
                return CreateOverflowInfo(messages, ExtractLimitFromMessage(message, messages));
            }

            // Check for generic context/prompt + token + overflow keywords
            if (IsGenericOverflowMessage(message))
            {
                return CreateOverflowInfo(messages);
            }

            // Check for Anthropic BadRequest with token mention
            if (ex is AnthropicBadRequestException && message.ContainsIgnoreCase("token"))
            {
                return CreateOverflowInfo(messages);
            }

            return new ContextOverflowInfo { IsOverflow = false };
        }

        private ContextOverflowInfo TryMatchOverflowPattern(
            string message,
            OverflowPattern pattern,
            List<ChatMessage> messages)
        {
            var match = Regex.Match(message, pattern.Regex, RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return new ContextOverflowInfo { IsOverflow = false };
            }

            int currentTokens = pattern.CurrentTokensGroup > 0 && 
                                int.TryParse(match.Groups[pattern.CurrentTokensGroup].Value, out int c)
                ? c
                : EstimateTokens(messages);

            int maxTokens = pattern.MaxTokensGroup > 0 && 
                            int.TryParse(match.Groups[pattern.MaxTokensGroup].Value, out int m)
                ? m
                : (int)(currentTokens * 0.9); // Estimate max as 90% of current

            return new ContextOverflowInfo
            {
                IsOverflow = true,
                CurrentTokens = currentTokens,
                MaxTokens = maxTokens
            };
        }

        private ContextOverflowInfo CreateOverflowInfo(List<ChatMessage> messages, int? maxTokens = null)
        {
            int currentTokens = EstimateTokens(messages);
            return new ContextOverflowInfo
            {
                IsOverflow = true,
                CurrentTokens = currentTokens,
                MaxTokens = maxTokens ?? (int)(currentTokens * 0.8)
            };
        }

        private int ExtractLimitFromMessage(string message, List<ChatMessage> messages)
        {
            var match = Regex.Match(message, @"limit of\s*(\d+)", RegexOptions.IgnoreCase);
            return match.Success && int.TryParse(match.Groups[1].Value, out int limit)
                ? limit
                : (int)(EstimateTokens(messages) * 0.8);
        }

        private static bool IsGenericOverflowMessage(string message)
        {
            bool hasContextOrPrompt = message.ContainsIgnoreCase("context") || 
                                      message.ContainsIgnoreCase("prompt");
            bool hasToken = message.ContainsIgnoreCase("token");
            bool hasOverflowKeyword = message.ContainsIgnoreCase("too long") ||
                                      message.ContainsIgnoreCase("exceeds") ||
                                      message.ContainsIgnoreCase("limit");

            return hasContextOrPrompt && hasToken && hasOverflowKeyword;
        }

        /// <summary>
        /// Estimates the number of tokens in messages (rough approximation).
        /// Uses 4 characters ≈ 1 token as a conservative estimate.
        /// </summary>
        private int EstimateTokens(List<ChatMessage> messages)
        {
            int totalChars = 0;
            foreach (var msg in messages)
            {
                // Get text content from the message
                var text = msg.Text;
                totalChars += text?.Length ?? 0;
                // Add overhead for role and structure
                totalChars += 50;
            }
            return totalChars / 4; // Conservative: 4 chars = 1 token
        }

        /// <summary>
        /// Intelligently truncates messages to fit within token limit.
        /// Strategy:
        /// 1. Keep system message intact (usually first)
        /// 2. Keep most recent user message intact (usually last)
        /// 3. Progressively remove older messages from the middle
        /// 4. If still too long, truncate message contents
        /// </summary>
        private List<ChatMessage> TruncateMessages(
            List<ChatMessage> messages, 
            int maxTokens, 
            int currentTokens)
        {
            if (messages.Count == 0)
            {
                return messages;
            }

            // Calculate target tokens (use 95% of max as safety margin, or 80% of current if max unknown)
            int targetTokens = maxTokens > 0 ? (int)(maxTokens * 0.95) : (int)(currentTokens * 0.8);

            logger?.LogVerbose(
                $"Truncating messages: current={currentTokens}, max={maxTokens}, target={targetTokens}");

            var result = new List<ChatMessage>();

            // Identify system message (usually first) and latest user message (usually last)
            ChatMessage? systemMessage = null;
            ChatMessage? latestUserMessage = null;
            var middleMessages = new List<ChatMessage>();

            for (int i = 0; i < messages.Count; i++)
            {
                var msg = messages[i];
                if (i == 0 && msg.Role == ChatRole.System)
                {
                    systemMessage = msg;
                }
                else if (i == messages.Count - 1 && msg.Role == ChatRole.User)
                {
                    latestUserMessage = msg;
                }
                else
                {
                    middleMessages.Add(msg);
                }
            }

            // Always include system message
            if (systemMessage != null)
            {
                result.Add(systemMessage);
            }

            // Try including all middle messages initially, then remove from oldest
            var includedMiddle = new List<ChatMessage>(middleMessages);
            int estimatedTokens = EstimateTokens(result);
            
            if (latestUserMessage != null)
            {
                estimatedTokens += EstimateTokens(new List<ChatMessage> { latestUserMessage });
            }

            // Remove middle messages from the beginning until we fit
            while (includedMiddle.Count > 0)
            {
                var middleTokens = EstimateTokens(includedMiddle);
                if (estimatedTokens + middleTokens <= targetTokens)
                {
                    break; // Everything fits
                }

                // Remove oldest message
                includedMiddle.RemoveAt(0);
                logger?.LogVerbose($"Removed 1 message, {includedMiddle.Count} middle messages remaining");
            }

            result.AddRange(includedMiddle);

            // Add latest user message
            if (latestUserMessage != null)
            {
                result.Add(latestUserMessage);
            }

            // Final check - if still too large, truncate content of messages
            estimatedTokens = EstimateTokens(result);
            if (estimatedTokens > targetTokens && result.Count > 0)
            {
                logger?.LogVerbose(
                    $"Still too large ({estimatedTokens} tokens), truncating message contents");

                // Truncate from end backwards, but preserve system and last user message structure
                double reductionFactor = (double)targetTokens / estimatedTokens;
                
                for (int i = 0; i < result.Count; i++)
                {
                    var msg = result[i];
                    var msgText = msg.Text;
                    if (string.IsNullOrEmpty(msgText))
                    {
                        continue;
                    }

                    // Don't truncate system message or final user message too aggressively
                    bool isImportant = (i == 0 && msg.Role == ChatRole.System) || 
                                      (i == result.Count - 1 && msg.Role == ChatRole.User);
                    
                    int targetLength = isImportant 
                        ? (int)(msgText.Length * Math.Max(0.7, reductionFactor)) // Keep at least 70%
                        : (int)(msgText.Length * reductionFactor);

                    if (msgText.Length > targetLength)
                    {
                        var truncated = msgText.Substring(0, targetLength);
                        // Try to cut at a nice boundary
                        var lastNewline = truncated.LastIndexOf('\n');
                        if (lastNewline > targetLength * 0.8)
                        {
                            truncated = truncated.Substring(0, lastNewline);
                        }
                        
                        result[i] = new ChatMessage(
                            msg.Role, 
                            truncated + "\n\n[... content truncated to fit token limit ...]");
                    }
                }
            }

            logger?.LogVerbose(
                $"Truncation complete: {messages.Count} -> {result.Count} messages, " +
                $"estimated {EstimateTokens(result)} tokens");

            return result;
        }
    }
}

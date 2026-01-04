using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace StructuredLogger.LLM.Clients.GitHub
{
    /// <summary>
    /// Account type for GitHub Copilot.
    /// </summary>
    public enum CopilotAccountType
    {
        Individual,
        Business,
        Enterprise
    }

    /// <summary>
    /// GitHub Copilot token with metadata.
    /// </summary>
    public class CopilotToken
    {
        [JsonPropertyName("token")]
        public string Token { get; set; } = string.Empty;

        [JsonPropertyName("expires_at")]
        public long ExpiresAtUnix { get; set; }

        [JsonPropertyName("refresh_in")]
        public int RefreshIn { get; set; }

        [JsonIgnore]
        public DateTimeOffset ExpiresAt
        {
            get => DateTimeOffset.FromUnixTimeSeconds(ExpiresAtUnix);
            set => ExpiresAtUnix = value.ToUnixTimeSeconds();
        }

        [JsonIgnore]
        public string? BaseUrl { get; set; }

        [JsonIgnore]
        public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt.AddMinutes(-5);
    }

    /// <summary>
    /// Response from GitHub device code flow initiation.
    /// </summary>
    public class DeviceCodeResponse
    {
        [JsonPropertyName("device_code")]
        public string DeviceCode { get; set; } = string.Empty;

        [JsonPropertyName("user_code")]
        public string UserCode { get; set; } = string.Empty;

        [JsonPropertyName("verification_uri")]
        public string VerificationUri { get; set; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("interval")]
        public int Interval { get; set; }
    }

    /// <summary>
    /// Response from GitHub access token polling.
    /// </summary>
    public class AccessTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("token_type")]
        public string? TokenType { get; set; }

        [JsonPropertyName("scope")]
        public string? Scope { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("error_description")]
        public string? ErrorDescription { get; set; }
    }

    /// <summary>
    /// Chat completion request for GitHub Copilot API.
    /// Compatible with OpenAI format.
    /// </summary>
    public class ChatCompletionRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("messages")]
        public List<ChatMessageDto> Messages { get; set; } = new List<ChatMessageDto>();

        [JsonPropertyName("temperature")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public float? Temperature { get; set; }

        [JsonPropertyName("top_p")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public float? TopP { get; set; }

        [JsonPropertyName("max_tokens")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? MaxTokens { get; set; }

        [JsonPropertyName("stream")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? Stream { get; set; }

        [JsonPropertyName("tools")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<ToolDto>? Tools { get; set; }

        [JsonPropertyName("tool_choice")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ToolChoice { get; set; }
    }

    /// <summary>
    /// Chat message DTO for API requests/responses.
    /// </summary>
    public class ChatMessageDto
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Content { get; set; }

        [JsonPropertyName("tool_calls")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<ToolCallDto>? ToolCalls { get; set; }

        [JsonPropertyName("tool_call_id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ToolCallId { get; set; }

        [JsonPropertyName("reasoning_opaque")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ReasoningOpaque { get; set; }

        [JsonExtensionData]
        public Dictionary<string, object>? AdditionalProperties { get; set; }
    }

    /// <summary>
    /// Tool definition for function calling.
    /// </summary>
    public class ToolDto
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "function";

        [JsonPropertyName("function")]
        public FunctionDto Function { get; set; } = new FunctionDto();
    }

    /// <summary>
    /// Function definition.
    /// </summary>
    public class FunctionDto
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Description { get; set; }

        [JsonPropertyName("parameters")]
        public object? Parameters { get; set; }
    }

    /// <summary>
    /// Tool call from assistant.
    /// </summary>
    public class ToolCallDto
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = "function";

        [JsonPropertyName("function")]
        public FunctionCallDto Function { get; set; } = new FunctionCallDto();
    }

    /// <summary>
    /// Function call details.
    /// </summary>
    public class FunctionCallDto
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("arguments")]
        public string Arguments { get; set; } = string.Empty;
    }

    /// <summary>
    /// Response from GitHub Copilot chat completions.
    /// </summary>
    public class ChatCompletionResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("choices")]
        public List<ChoiceDto> Choices { get; set; } = new List<ChoiceDto>();

        [JsonPropertyName("usage")]
        public UsageDto? Usage { get; set; }

        [JsonPropertyName("created")]
        public long? Created { get; set; }

        [JsonPropertyName("system_fingerprint")]
        public string? SystemFingerprint { get; set; }

        [JsonExtensionData]
        public Dictionary<string, object>? AdditionalProperties { get; set; }
    }

    /// <summary>
    /// Choice in the response.
    /// </summary>
    public class ChoiceDto
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("message")]
        public ChatMessageDto Message { get; set; } = new ChatMessageDto();

        [JsonPropertyName("finish_reason")]
        public string? FinishReason { get; set; }

        [JsonExtensionData]
        public Dictionary<string, object>? AdditionalProperties { get; set; }
    }

    /// <summary>
    /// Token usage information.
    /// </summary>
    public class UsageDto
    {
        [JsonPropertyName("prompt_tokens")]
        public int PromptTokens { get; set; }

        [JsonPropertyName("completion_tokens")]
        public int CompletionTokens { get; set; }

        [JsonPropertyName("total_tokens")]
        public int TotalTokens { get; set; }
    }

    /// <summary>
    /// Streaming chunk from chat completions.
    /// </summary>
    public class ChatCompletionChunk
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("choices")]
        public List<StreamChoiceDto> Choices { get; set; } = new List<StreamChoiceDto>();
    }

    /// <summary>
    /// Streaming choice.
    /// </summary>
    public class StreamChoiceDto
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("delta")]
        public DeltaDto Delta { get; set; } = new DeltaDto();

        [JsonPropertyName("finish_reason")]
        public string? FinishReason { get; set; }
    }

    /// <summary>
    /// Delta in streaming response.
    /// </summary>
    public class DeltaDto
    {
        [JsonPropertyName("role")]
        public string? Role { get; set; }

        [JsonPropertyName("content")]
        public string? Content { get; set; }

        [JsonPropertyName("tool_calls")]
        public List<ToolCallDto>? ToolCalls { get; set; }
    }
}

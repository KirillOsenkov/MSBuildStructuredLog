using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace StructuredLogger.LLM.Clients.GitHub
{
    /// <summary>
    /// Response from GitHub Copilot models API.
    /// </summary>
    public class ModelsResponse
    {
        [JsonPropertyName("data")]
        public List<ModelInfo> Data { get; set; } = new List<ModelInfo>();
    }

    /// <summary>
    /// Information about an available GitHub Copilot model.
    /// </summary>
    public class ModelInfo
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("model_picker_enabled")]
        public bool ModelPickerEnabled { get; set; }

        [JsonPropertyName("policy")]
        public ModelPolicy Policy { get; set; } = new ModelPolicy();
    }

    /// <summary>
    /// Model policy information.
    /// </summary>
    public class ModelPolicy
    {
        [JsonPropertyName("state")]
        public string State { get; set; } = string.Empty;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace StructuredLogViewer.LLM
{
    /// <summary>
    /// Contains information about a tool call execution including arguments, results, and timing.
    /// </summary>
    public class ToolCallInfo
    {
        public string ToolName { get; set; }
        public string ArgumentsJson { get; set; }
        public string ResultText { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public TimeSpan? Duration => EndTime.HasValue ? EndTime.Value - StartTime : null;
        public bool IsError { get; set; }
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Gets a dictionary of parsed arguments for user-friendly display.
        /// </summary>
        public Dictionary<string, string> GetParsedArguments()
        {
            var result = new Dictionary<string, string>();
            
            if (string.IsNullOrWhiteSpace(ArgumentsJson))
            {
                return result;
            }

            try
            {
                using (var doc = JsonDocument.Parse(ArgumentsJson))
                {
                    foreach (var property in doc.RootElement.EnumerateObject())
                    {
                        result[property.Name] = GetJsonValueAsString(property.Value);
                    }
                }
            }
            catch
            {
                // If parsing fails, return the raw JSON as a single entry
                result["arguments"] = ArgumentsJson;
            }

            return result;
        }

        /// <summary>
        /// Gets a user-friendly summary of arguments (truncated).
        /// </summary>
        public string GetArgumentsSummary(int maxLength = 100)
        {
            var args = GetParsedArguments();
            if (args.Count == 0)
            {
                return "(no arguments)";
            }

            var parts = args.Select(kvp => $"{kvp.Key}: {kvp.Value}");
            var summary = string.Join(", ", parts);
            
            if (summary.Length > maxLength)
            {
                return summary.Substring(0, maxLength - 3) + "...";
            }

            return summary;
        }

        private string GetJsonValueAsString(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    return element.GetString();
                case JsonValueKind.Number:
                    return element.GetRawText();
                case JsonValueKind.True:
                    return "true";
                case JsonValueKind.False:
                    return "false";
                case JsonValueKind.Null:
                    return "null";
                case JsonValueKind.Array:
                    var arrayItems = element.EnumerateArray()
                        .Select(e => GetJsonValueAsString(e))
                        .Take(3);
                    var arrayStr = string.Join(", ", arrayItems);
                    if (element.GetArrayLength() > 3)
                    {
                        arrayStr += "...";
                    }
                    return $"[{arrayStr}]";
                case JsonValueKind.Object:
                    return "{...}";
                default:
                    return element.GetRawText();
            }
        }
    }
}

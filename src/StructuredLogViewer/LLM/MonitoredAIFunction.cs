using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;

namespace StructuredLogViewer.LLM
{
    /// <summary>
    /// Wraps an AIFunction to monitor and capture tool call executions.
    /// Raises events when tool calls start and complete, including timing and result information.
    /// </summary>
    public class MonitoredAIFunction : AIFunction
    {
        private readonly AIFunction innerFunction;

        public event EventHandler<ToolCallInfo> ToolCallStarted;
        public event EventHandler<ToolCallInfo> ToolCallCompleted;

        public MonitoredAIFunction(AIFunction innerFunction)
        {
            this.innerFunction = innerFunction ?? throw new ArgumentNullException(nameof(innerFunction));
        }

        // Delegate properties to inner function
        public override string Name => innerFunction.Name;
        public override string Description => innerFunction.Description;
        public override JsonElement JsonSchema => innerFunction.JsonSchema;
        public override MethodInfo UnderlyingMethod => innerFunction.UnderlyingMethod;
        public override JsonElement? ReturnJsonSchema => innerFunction.ReturnJsonSchema;
        public override JsonSerializerOptions JsonSerializerOptions => innerFunction.JsonSerializerOptions;
        public override IReadOnlyDictionary<string, object> AdditionalProperties => innerFunction.AdditionalProperties;

        protected override async ValueTask<object?> InvokeCoreAsync(
            AIFunctionArguments arguments,
            CancellationToken cancellationToken)
        {
            var toolCallInfo = new ToolCallInfo
            {
                CallId = Guid.NewGuid(),
                ToolName = Name,
                StartTime = DateTime.Now
            };

            // Remap arguments if needed (handle LLM providing slightly wrong parameter names)
            var correctedArguments = RemapArgumentsIfNeeded(arguments);

            // Serialize arguments for display (using corrected arguments)
            try
            {
                var argsDict = new Dictionary<string, object>();
                foreach (var arg in correctedArguments)
                {
                    argsDict[arg.Key] = arg.Value;
                }
                toolCallInfo.ArgumentsJson = JsonSerializer.Serialize(argsDict);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to serialize arguments: {ex.Message}");
                toolCallInfo.ArgumentsJson = "{}";
            }

            // Raise start event
            ToolCallStarted?.Invoke(this, toolCallInfo);

            try
            {
                // Invoke the actual function with corrected arguments
                var result = await innerFunction.InvokeAsync(correctedArguments, cancellationToken);
                
                toolCallInfo.EndTime = DateTime.Now;
                toolCallInfo.ResultText = result?.ToString() ?? "(no result)";
                
                // Raise completion event
                ToolCallCompleted?.Invoke(this, toolCallInfo);
                
                return result;
            }
            catch (Exception ex)
            {
                toolCallInfo.EndTime = DateTime.Now;
                toolCallInfo.IsError = true;
                toolCallInfo.ErrorMessage = ex.Message;
                toolCallInfo.ResultText = $"Error: {ex.Message}";
                
                // Raise completion event even for errors
                ToolCallCompleted?.Invoke(this, toolCallInfo);
                
                throw;
            }
        }

        /// <summary>
        /// Remaps argument names if the LLM provided slightly incorrect names.
        /// Handles cases like "path" instead of "filePath" by checking substring matches.
        /// Prioritizes mandatory parameters over optional ones.
        /// </summary>
        private AIFunctionArguments RemapArgumentsIfNeeded(AIFunctionArguments arguments)
        {
            // Get expected parameter names from the function's JSON schema
            var (expectedParams, requiredParams) = GetExpectedParameterNames();
            if (expectedParams.Count == 0)
            {
                // No schema available, return arguments as-is
                return arguments;
            }

            var providedKeys = new HashSet<string>(arguments.Keys, StringComparer.OrdinalIgnoreCase);
            var expectedKeys = new HashSet<string>(expectedParams, StringComparer.OrdinalIgnoreCase);

            // Check if all provided keys are already correct
            if (providedKeys.IsSubsetOf(expectedKeys))
            {
                return arguments;
            }

            // Find keys that need remapping
            var remappings = new Dictionary<string, string>(); // provided -> expected
            var unmatchedProvided = new HashSet<string>(providedKeys);
            var unmatchedExpected = new HashSet<string>(expectedKeys);

            // Remove exact matches
            foreach (var key in providedKeys.ToList())
            {
                if (expectedKeys.Contains(key))
                {
                    unmatchedProvided.Remove(key);
                    unmatchedExpected.Remove(key);
                }
            }

            // Try to find fuzzy matches for remaining arguments
            foreach (var providedKey in unmatchedProvided.ToList())
            {
                // First, try to find matches in required parameters only
                var requiredMatches = FindPotentialMatches(providedKey, unmatchedExpected, requiredParams, requiredOnly: true);
                
                if (requiredMatches.Count == 1)
                {
                    // Single unambiguous match in required parameters - use it
                    var expectedKey = requiredMatches[0];
                    remappings[providedKey] = expectedKey;
                    unmatchedExpected.Remove(expectedKey);
                    unmatchedProvided.Remove(providedKey);
                    
                    System.Diagnostics.Debug.WriteLine(
                        $"[MonitoredAIFunction] Remapping argument '{providedKey}' -> '{expectedKey}' (required) for function '{Name}'");
                }
                else if (requiredMatches.Count == 0)
                {
                    // No match in required parameters, check all parameters
                    var allMatches = FindPotentialMatches(providedKey, unmatchedExpected, requiredParams, requiredOnly: false);
                    
                    if (allMatches.Count == 1)
                    {
                        // Unambiguous match found in optional parameters
                        var expectedKey = allMatches[0];
                        remappings[providedKey] = expectedKey;
                        unmatchedExpected.Remove(expectedKey);
                        unmatchedProvided.Remove(providedKey);
                        
                        System.Diagnostics.Debug.WriteLine(
                            $"[MonitoredAIFunction] Remapping argument '{providedKey}' -> '{expectedKey}' (optional) for function '{Name}'");
                    }
                    else if (allMatches.Count > 1)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[MonitoredAIFunction] Ambiguous matches for '{providedKey}': {string.Join(", ", allMatches)} - not remapping");
                    }
                }
                else
                {
                    // Multiple matches in required parameters - ambiguous
                    System.Diagnostics.Debug.WriteLine(
                        $"[MonitoredAIFunction] Ambiguous required matches for '{providedKey}': {string.Join(", ", requiredMatches)} - not remapping");
                }
            }

            // Final fallback: if exactly one unmatched provided arg and one unmatched expected arg remain, match them
            if (unmatchedProvided.Count == 1 && unmatchedExpected.Count == 1)
            {
                var providedKey = unmatchedProvided.First();
                var expectedKey = unmatchedExpected.First();
                remappings[providedKey] = expectedKey;
                
                System.Diagnostics.Debug.WriteLine(
                    $"[MonitoredAIFunction] Remapping last remaining argument '{providedKey}' -> '{expectedKey}' for function '{Name}'");
            }

            // If no remappings needed, return original
            if (remappings.Count == 0)
            {
                return arguments;
            }

            // Create new AIFunctionArguments with remapped keys
            var correctedArgs = new AIFunctionArguments
            {
                Services = arguments.Services,
                Context = arguments.Context
            };

            foreach (var kvp in arguments)
            {
                var keyToUse = remappings.ContainsKey(kvp.Key) ? remappings[kvp.Key] : kvp.Key;
                correctedArgs[keyToUse] = kvp.Value;
            }

            return correctedArgs;
        }

        /// <summary>
        /// Finds potential matches for a provided parameter name among expected parameters.
        /// Uses substring matching in both directions.
        /// Can optionally filter to only required parameters.
        /// </summary>
        private List<string> FindPotentialMatches(string providedKey, HashSet<string> expectedKeys, 
            HashSet<string> requiredKeys, bool requiredOnly)
        {
            var matches = new List<string>();

            foreach (var expectedKey in expectedKeys)
            {
                // If filtering to required only, skip optional parameters
                if (requiredOnly && !requiredKeys.Contains(expectedKey))
                {
                    continue;
                }

                // Check if one is a substring of the other (case-insensitive)
                if (providedKey.IndexOf(expectedKey, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    expectedKey.IndexOf(providedKey, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    matches.Add(expectedKey);
                }
            }

            return matches;
        }

        /// <summary>
        /// Extracts expected parameter names and required parameter names from the function's JSON schema.
        /// Returns (allParameters, requiredParameters).
        /// </summary>
        private (List<string> allParams, HashSet<string> requiredParams) GetExpectedParameterNames()
        {
            var paramNames = new List<string>();
            var requiredNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var schema = innerFunction.JsonSchema;
                
                // Check if schema has properties
                if (schema.ValueKind == JsonValueKind.Object)
                {
                    if (schema.TryGetProperty("properties", out var properties) &&
                        properties.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var property in properties.EnumerateObject())
                        {
                            paramNames.Add(property.Name);
                        }
                    }

                    // Check if schema has required array
                    if (schema.TryGetProperty("required", out var required) &&
                        required.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var element in required.EnumerateArray())
                        {
                            if (element.ValueKind == JsonValueKind.String)
                            {
                                requiredNames.Add(element.GetString());
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to parse function schema: {ex.Message}");
            }

            return (paramNames, requiredNames);
        }
    }
}

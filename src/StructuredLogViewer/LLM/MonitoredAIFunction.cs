using System;
using System.Collections.Generic;
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

        protected override async ValueTask<object?> InvokeCoreAsync(
            AIFunctionArguments arguments,
            CancellationToken cancellationToken)
        {
            var toolCallInfo = new ToolCallInfo
            {
                ToolName = Name,
                StartTime = DateTime.Now
            };

            // Serialize arguments for display
            try
            {
                var argsDict = new Dictionary<string, object>();
                foreach (var arg in arguments)
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
                // Invoke the actual function
                var result = await innerFunction.InvokeAsync(arguments, cancellationToken);
                
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
    }
}

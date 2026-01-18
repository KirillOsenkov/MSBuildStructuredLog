using System;
using StructuredLogger.LLM;

namespace BinlogTool
{
    /// <summary>
    /// Reports LLM progress events to the console with visual formatting.
    /// </summary>
    public class PromptProgressReporter
    {
        private readonly CliLogger logger;

        public PromptProgressReporter(CliLogger logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void OnMessage(object sender, ChatMessageViewModel message)
        {
            if (message.IsError)
            {
                logger.LogError(message.Content);
            }
            else if (message.Role == "Assistant" || message.Role == "Agent")
            {
                logger.LogResponse(message.Content);
            }
            else if (message.Role == "System")
            {
                logger.LogSystem(message.Content);
            }
            else
            {
                logger.LogInfo(message.Content);
            }
        }

        public void OnToolCallStarted(object sender, ToolCallInfo toolCall)
        {
            logger.LogTool($"🔧 Executing: {toolCall.ToolName}");
            
            if (logger.IsVerbose)
            {
                var argsSummary = toolCall.GetArgumentsSummary(200);
                logger.LogVerbose($"   Arguments: {argsSummary}");
            }
        }

        public void OnToolCallCompleted(object sender, ToolCallInfo toolCall)
        {
            var duration = toolCall.Duration?.TotalSeconds.ToString("F1") ?? "?";
            
            if (toolCall.IsError)
            {
                logger.LogError($"❌ {toolCall.ToolName} failed ({duration}s): {toolCall.ErrorMessage}");
            }
            else
            {
                logger.LogTool($"✓ {toolCall.ToolName} ({duration}s)");
                
                if (logger.IsVerbose && !string.IsNullOrWhiteSpace(toolCall.ResultText))
                {
                    var preview = toolCall.ResultText.Length > 200 
                        ? toolCall.ResultText.Substring(0, 200) + "..."
                        : toolCall.ResultText;
                    logger.LogVerbose($"   Result: {preview}");
                }
            }
        }

        public void OnRetrying(object sender, ResilienceEventArgs e)
        {
            logger.LogRetry($"⚠️ {e.Message}");
        }

        public void OnAgentProgress(object sender, AgentProgressEventArgs e)
        {
            var phaseEmoji = e.Phase switch
            {
                AgentExecutionPhase.Planning => "📋",
                AgentExecutionPhase.Research => "🔍",
                AgentExecutionPhase.Summarization => "📊",
                AgentExecutionPhase.Complete => "✅",
                AgentExecutionPhase.Failed => "❌",
                _ => "⏳"
            };

            logger.LogAgent($"{phaseEmoji} Phase: {e.Phase} - {e.Message}");

            // Show task progress in verbose mode
            if (logger.IsVerbose && e.CurrentTask != null)
            {
                logger.LogVerbose($"   Task: {e.CurrentTask.Description} [{e.CurrentTask.Status}]");
            }
        }
    }
}

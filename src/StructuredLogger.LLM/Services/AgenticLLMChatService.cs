using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Logging.StructuredLogger;
using Microsoft.Extensions.AI;
using StructuredLogger.LLM.Logging;

namespace StructuredLogger.LLM
{
    /// <summary>
    /// Orchestrates agentic multi-step reasoning for complex log analysis.
    /// Manages planning, research, and summarization phases.
    /// </summary>
    public class AgenticLLMChatService : IDisposable
    {
        private readonly BinlogContextProvider contextProvider;
        private readonly List<IToolsContainer> toolContainers;
        private MultiProviderLLMClient? llmClient;
        private readonly LLMConfiguration configuration;
        private readonly ILLMLogger? logger;

        public event EventHandler<AgentProgressEventArgs>? ProgressUpdated;
        public event EventHandler<ChatMessageViewModel>? MessageAdded;
        public event EventHandler<ToolCallInfo>? ToolCallExecuting;
        public event EventHandler<ToolCallInfo>? ToolCallExecuted;
        public event EventHandler<ResilienceEventArgs>? RequestRetrying;

        public bool IsConfigured => configuration?.IsConfigured ?? false;

        // Configuration
        public int MaxResearchTasks { get; set; } = 5;
        public int MaxTokensPerTask { get; set; } = 4000;

        private AgenticLLMChatService(Build build, LLMConfiguration config, ILLMLogger? logger)
        {
            this.contextProvider = new BinlogContextProvider(build);
            this.toolContainers = new List<IToolsContainer>();
            this.configuration = config ?? throw new ArgumentNullException(nameof(config));
            this.logger = logger;

            // Register default tool executors
            RegisterToolContainer(new BinlogToolExecutor(build));
            RegisterToolContainer(new EmbeddedFilesToolExecutor(build));
            RegisterToolContainer(new ListEventsToolExecutor(build));
            RegisterToolContainer(new ResultsToolExecutor());
        }

        /// <summary>
        /// Creates and initializes a new instance of AgenticLLMChatService.
        /// </summary>
        /// <param name="build">The build to analyze.</param>
        /// <param name="config">LLM configuration.</param>
        /// <param name="logger">Optional logger for diagnostics.</param>
        /// <param name="cancellationToken">Cancellation token for async initialization.</param>
        /// <returns>A fully initialized AgenticLLMChatService instance.</returns>
        public static async System.Threading.Tasks.Task<AgenticLLMChatService> CreateAsync(
            Build build,
            LLMConfiguration config,
            ILLMLogger? logger = null,
            CancellationToken cancellationToken = default)
        {
            var service = new AgenticLLMChatService(build, config, logger);
            
            await service.InitializeLLMClientAsync(cancellationToken);
            
            return service;
        }

        private async System.Threading.Tasks.Task InitializeLLMClientAsync(CancellationToken cancellationToken)
        {
            if (!configuration.IsConfigured)
            {
                return;
            }

            var client = new MultiProviderLLMClient(configuration, logger: logger);
            await client.InitializeAsync(cancellationToken);
            this.llmClient = client;
            
            SubscribeToResilienceEvents(client);
        }

        private void SubscribeToResilienceEvents(MultiProviderLLMClient client)
        {
            // Subscribe to resilience events
            if (client.ResilientClient != null)
            {
                client.ResilientClient.RequestRetrying += (sender, e) => RequestRetrying?.Invoke(this, e);
            }
        }

        /// <summary>
        /// Registers an additional tool executor with this service.
        /// Used to add UI-specific tools after service construction.
        /// </summary>
        public void RegisterToolContainer(IToolsContainer executor)
        {
            if (executor == null)
            {
                throw new ArgumentNullException(nameof(executor));
            }

            // Prevent duplicate registration
            if (!toolContainers.Contains(executor))
            {
                toolContainers.Add(executor);
            }
        }

        /// <summary>
        /// Executes an agentic workflow for a user query.
        /// </summary>
        public async Task<string> ExecuteAgenticWorkflowAsync(string userQuery, CancellationToken cancellationToken = default)
        {
            if (!IsConfigured)
            {
                return "LLM is not configured. Please configure the LLM settings.";
            }

            var plan = new AgentPlan(userQuery);

            try
            {
                // Phase 1: Planning
                await PlanningPhaseAsync(plan, cancellationToken);

                // Phase 2: Research
                await ResearchPhaseAsync(plan, cancellationToken);

                // Phase 3: Summarization
                await SummarizationPhaseAsync(plan, cancellationToken);

                plan.Phase = AgentExecutionPhase.Complete;
                plan.EndTime = DateTime.Now;
                RaiseProgress(plan, "Agent workflow completed successfully.");

                return plan.FinalSummary ?? "No summary generated.";
            }
            catch (OperationCanceledException)
            {
                plan.Phase = AgentExecutionPhase.Failed;
                plan.Error = "Operation cancelled by user.";
                RaiseProgress(plan, plan.Error, isError: true);
                return plan.Error;
            }
            catch (Exception ex)
            {
                plan.Phase = AgentExecutionPhase.Failed;
                plan.Error = $"Agent workflow failed: {ex.Message}";
                RaiseProgress(plan, plan.Error, isError: true);
                return plan.Error;
            }
        }

        /// <summary>
        /// Phase 1: Generate a research plan by breaking down the user query.
        /// </summary>
        private async System.Threading.Tasks.Task PlanningPhaseAsync(AgentPlan plan, CancellationToken cancellationToken)
        {
            plan.Phase = AgentExecutionPhase.Planning;
            RaiseProgress(plan, "Creating research plan...");

            // Get research tools to include their definitions in planning context
            var researchTools = GetToolsForPhase(AgentPhase.Research);
            var toolDescriptions = GetToolDescriptions(researchTools);

            var systemPrompt = GetPlanningSystemPrompt(toolDescriptions);
            
            var overview = contextProvider is BinlogContextProvider provider
                ? provider.GetBuildOverview()
                : "Build log loaded";

            var userPrompt = $@"User Question: {plan.UserQuery}

Build Overview:
{overview}

Analyze this question carefully. Think through:
- What information is needed to answer this question?
- What specific aspects of the build should be investigated?
- Which tools would be most appropriate for each investigation?
- Are there any ambiguities that you will not be able to resolve yourself and that absolutely need clarification from the user?

You can use the available tools (including AskUser if the question is ambiguous) to help you plan better.

After your analysis, create a research plan with 1-{MaxResearchTasks} specific tasks.
End your response with the plan in JSON format:
```json
{{
  ""tasks"": [
    {{""id"": ""task1"", ""description"": ""Brief description"", ""goal"": ""Specific investigation goal""}},
    {{""id"": ""task2"", ""description"": ""Brief description"", ""goal"": ""Specific investigation goal""}}
  ]
}}
```";

            var messages = new List<ChatMessage>
            {
                new ChatMessage(ChatRole.System, systemPrompt),
                new ChatMessage(ChatRole.User, userPrompt)
            };

            // Get tools for planning phase (including AskUser)
            var planningTools = GetToolsForPhase(AgentPhase.Planning);

            var options = new ChatOptions
            {
                Tools = planningTools,
                Temperature = 0.4f // Slightly higher to encourage thinking
            };

            var response = await llmClient!.CompleteChatAsync(messages, options, cancellationToken);
            var fullResponse = response.Text?.Trim() ?? "";

            // Separate thinking from plan JSON
            var (thinking, planJson) = ExtractThinkingAndPlan(fullResponse);
            
            // Store the thinking
            if (!string.IsNullOrWhiteSpace(thinking))
            {
                plan.PlanningThinking = thinking;
                RaiseMessage(new ChatMessageViewModel(
                    "Assistant",
                    $"**Planning Analysis:**\n\n{thinking}"
                ));
            }

            // Parse the plan
            try
            {
                var jsonOptions = new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                };
                var planData = JsonSerializer.Deserialize<PlanResponse>(planJson, jsonOptions);
                
                // Check if planning agent provided a direct answer
                if (!string.IsNullOrWhiteSpace(planData?.DirectAnswer))
                {
                    plan.DirectAnswer = planData!.DirectAnswer;
                    RaiseProgress(plan, "Planning agent provided direct answer (trivial query detected).");
                    RaiseMessage(new ChatMessageViewModel(
                        "Assistant",
                        $"**Direct Answer (no research needed):**\n\n{planData.DirectAnswer}"
                    ));
                }
                else if (planData?.Tasks != null && planData.Tasks.Count > 0)
                {
                    foreach (var taskData in planData.Tasks.Take(MaxResearchTasks))
                    {
                        plan.ResearchTasks.Add(new ResearchTask(
                            taskData.Id ?? $"task{plan.ResearchTasks.Count + 1}",
                            taskData.Description ?? "Research task",
                            taskData.Goal ?? "Investigate"
                        ));
                    }

                    RaiseProgress(plan, $"Plan created with {plan.ResearchTasks.Count} research tasks.");
                }
                else
                {
                    throw new Exception("No tasks or direct answer in plan response.");
                }
            }
            catch (Exception ex)
            {
                // Fallback: Create a single generic research task
                plan.ResearchTasks.Add(new ResearchTask(
                    "task1",
                    "Investigate user query",
                    $"Use available tools to answer: {plan.UserQuery}"
                ));
                RaiseProgress(plan, $"Using fallback plan (parsing failed: {ex.Message})");
            }
        }

        /// <summary>
        /// Phase 2: Execute each research task sequentially.
        /// </summary>
        private async System.Threading.Tasks.Task ResearchPhaseAsync(AgentPlan plan, CancellationToken cancellationToken)
        {
            plan.Phase = AgentExecutionPhase.Research;
            
            // Skip research if planning agent provided a direct answer
            if (!string.IsNullOrWhiteSpace(plan.DirectAnswer))
            {
                RaiseProgress(plan, "Skipping research phase (direct answer provided by planning agent).");
                // Store direct answer as a finding for summarization phase
                plan.Findings["direct"] = plan.DirectAnswer!;
                return;
            }
            
            plan.CurrentTaskIndex = 0;

            for (int i = 0; i < plan.ResearchTasks.Count; i++)
            {
                plan.CurrentTaskIndex = i;
                var task = plan.ResearchTasks[i];

                task.Status = TaskStatus.InProgress;
                task.StartTime = DateTime.Now;
                RaiseProgress(plan, $"Researching: {task.Description}");

                // Add message showing research task start
                MessageAdded?.Invoke(this, new ChatMessageViewModel(
                    "Agent",
                    $"🔍 **Research Task {i + 1}/{plan.ResearchTasks.Count}**: {task.Description}\n\n_{task.Goal}_"
                ));

                try
                {
                    var findings = await ExecuteResearchTaskAsync(plan, task, cancellationToken);
                    task.Findings = findings;
                    task.Status = TaskStatus.Complete;
                    task.EndTime = DateTime.Now;
                    plan.Findings[task.Id] = findings;

                    // Add message showing findings
                    MessageAdded?.Invoke(this, new ChatMessageViewModel(
                        "Agent",
                        $"✓ **Findings**: {findings}"
                    ));

                    RaiseProgress(plan, $"Completed: {task.Description}");
                }
                catch (Exception ex)
                {
                    task.Status = TaskStatus.Failed;
                    task.EndTime = DateTime.Now;
                    task.Error = ex.Message;
                    plan.Findings[task.Id] = $"Task failed: {ex.Message}";

                    MessageAdded?.Invoke(this, new ChatMessageViewModel(
                        "Agent",
                        $"❌ **Task Failed**: {ex.Message}",
                        isError: true
                    ));

                    RaiseProgress(plan, $"Task failed: {task.Description} - {ex.Message}");
                    // Continue with other tasks
                }
            }
        }

        /// <summary>
        /// Execute a single research task.
        /// </summary>
        private async Task<string> ExecuteResearchTaskAsync(AgentPlan plan, ResearchTask task, CancellationToken cancellationToken)
        {
            var systemPrompt = GetResearchSystemPrompt();

            // Build context from previous findings (with length limits)
            var previousFindings = new StringBuilder();
            const int maxFindingsLength = 100000; // Limit previous findings
            
            foreach (var kvp in plan.Findings)
            {
                if (kvp.Key != task.Id) // Don't include current task
                {
                    var finding = kvp.Value;
                    // Truncate individual findings if too long
                    if (finding.Length > 20000)
                    {
                        finding = finding.Substring(0, 20000) + "... [truncated]";
                    }
                    
                    previousFindings.AppendLine($"[{kvp.Key}]: {finding}");
                    previousFindings.AppendLine();
                    
                    // Stop if we've accumulated too much context
                    if (previousFindings.Length > maxFindingsLength)
                    {
                        previousFindings.AppendLine("[Additional findings omitted to save space]");
                        break;
                    }
                }
            }

            var overview = contextProvider is BinlogContextProvider provider
                ? provider.GetBuildOverview()
                : "Build log loaded";

            var userPrompt = $@"Task Goal: {task.Goal}
Task Description: {task.Description}

Build Overview:
{overview}

Previous Findings:
{previousFindings}

Use the available tools to investigate and produce clear, concise findings for this task.
Focus only on this specific task goal. Output your findings as a summary.";

            var messages = new List<ChatMessage>
            {
                new ChatMessage(ChatRole.System, systemPrompt),
                new ChatMessage(ChatRole.User, userPrompt)
            };

            var researchTools = GetToolsForPhase(AgentPhase.Research);

            var options = new ChatOptions
            {
                Tools = researchTools,
                Temperature = 0.5f,
                MaxOutputTokens = MaxTokensPerTask
            };

            var response = await llmClient!.CompleteChatAsync(messages, options, cancellationToken);
            return response.Text ?? "No findings generated.";
        }

        /// <summary>
        /// Phase 3: Synthesize all findings and present final answer.
        /// </summary>
        private async System.Threading.Tasks.Task SummarizationPhaseAsync(AgentPlan plan, CancellationToken cancellationToken)
        {
            plan.Phase = AgentExecutionPhase.Summarization;
            RaiseProgress(plan, "Synthesizing findings and preparing answer...");

            MessageAdded?.Invoke(this, new ChatMessageViewModel(
                "Agent",
                "📊 **Synthesizing all findings...**"
            ));

            var systemPrompt = GetSummarizationSystemPrompt();

            // Compile all findings
            var allFindings = new StringBuilder();
            
            // Check if we have a direct answer from planning phase
            if (!string.IsNullOrWhiteSpace(plan.DirectAnswer))
            {
                allFindings.AppendLine("## Direct Answer from Planning Phase");
                allFindings.AppendLine("The planning agent determined this question could be answered directly without examining the build log:");
                allFindings.AppendLine();
                allFindings.AppendLine(plan.DirectAnswer);
                allFindings.AppendLine();
            }
            else
            {
                // Compile findings from research tasks
                for (int i = 0; i < plan.ResearchTasks.Count; i++)
                {
                    var task = plan.ResearchTasks[i];
                    allFindings.AppendLine($"## Task {i + 1}: {task.Description}");
                    allFindings.AppendLine($"Status: {task.Status}");
                    if (task.Status == TaskStatus.Complete)
                    {
                        allFindings.AppendLine($"Findings: {task.Findings}");
                    }
                    else if (task.Status == TaskStatus.Failed)
                    {
                        allFindings.AppendLine($"Error: {task.Error}");
                    }
                    allFindings.AppendLine();
                }
            }

            var userPrompt = $@"User Question: {plan.UserQuery}

Research Findings:
{allFindings}

Your task:
1. Synthesize the findings into a coherent, well-structured answer
2. Provide clear, actionable insights
3. If a direct answer was provided, enhance it with formatting and ensure it fully addresses the user's question

Format your answer with markdown for readability.";

            var messages = new List<ChatMessage>
            {
                new ChatMessage(ChatRole.System, systemPrompt),
                new ChatMessage(ChatRole.User, userPrompt)
            };

            // Get ALL tools for summarization
            var allTools = GetToolsForPhase(AgentPhase.Summarization);

            var options = new ChatOptions
            {
                Tools = allTools,
                Temperature = 0.7f
            };

            var response = await llmClient!.CompleteChatAsync(messages, options, cancellationToken);
            plan.FinalSummary = response.Text ?? "No summary generated.";

            RaiseProgress(plan, "Summarization complete.");
        }

        /// <summary>
        /// Get tools appropriate for the current phase.
        /// </summary>
        private AIFunction[] GetToolsForPhase(AgentPhase phase)
        {
            try
            {
                var tools = new List<AIFunction>();

                // Enumerate all tool executors and get their tools
                foreach (var executor in toolContainers)
                {
                    foreach (var (function, applicablePhases) in executor.GetTools())
                    {
                        // Filter by phase
                        if ((applicablePhases & phase) == 0)
                        {
                            continue; // Skip tools not applicable to this phase
                        }

                        // Wrap with monitoring
                        var monitored = new MonitoredAIFunction(function, logger);
                        monitored.ToolCallStarted += OnToolCallStarted;
                        monitored.ToolCallCompleted += OnToolCallCompleted;
                        tools.Add(monitored);
                    }
                }

                return tools.ToArray();
            }
            catch (Exception ex)
            {
                logger?.LogError($"Error creating tools for phase {phase}: {ex.Message}");
                return Array.Empty<AIFunction>();
            }
        }

        private void OnToolCallStarted(object? sender, ToolCallInfo toolCallInfo)
        {
            // Raise event for UI consumption
            ToolCallExecuting?.Invoke(this, toolCallInfo);
        }

        private void OnToolCallCompleted(object? sender, ToolCallInfo toolCallInfo)
        {
            // Raise event for UI consumption
            ToolCallExecuted?.Invoke(this, toolCallInfo);
        }

        private void RaiseProgress(AgentPlan plan, string? message = null, bool isError = false)
        {
            ProgressUpdated?.Invoke(this, new AgentProgressEventArgs(plan, message, isError));
        }

        private void RaiseMessage(ChatMessageViewModel message)
        {
            MessageAdded?.Invoke(this, message);
        }

        #region System Prompts

        private string GetPlanningSystemPrompt(string researchToolDescriptions)
        {
            return $@"You are an expert MSBuild log analyzer creating a research plan.

Your task: Analyze the user's question and determine if it needs research or if you already know the answer.

You have access to tools during planning:
- **AskUser**: Use this if the user's question is ambiguous or unclear. Don't hesitate to ask for clarification.
- Other planning tools as available

First, think through the question:
1. What are the key aspects that need investigation?
2. What tools would be most effective for gathering information?
3. Is the question clear or ambiguous? If you are very unclear about requirements, consider using AskUser to clarify.
4. Is this a trivial question that you can answer directly based on general MSBuild knowledge WITHOUT needing to analyze the specific build log?
   - If you are VERY CONFIDENT you know the answer, you can provide it directly instead of creating research tasks.

The research agents will have access to these tools:

{researchToolDescriptions}

**Two Response Options:**

**Option 1 - Research Required** (default, use when answer requires examining this specific build log):
Create specific research tasks with 1-{MaxResearchTasks} tasks. Each task should:
- Have a clear, specific goal
- Be designed to use the tools that will be available to the research agents
- Produce findings that contribute to answering the user's question

End your response with the plan in JSON format:
```json
{{
  ""tasks"": [
    {{""id"": ""task1"", ""description"": ""Brief description"", ""goal"": ""Specific investigation goal""}},
    {{""id"": ""task2"", ""description"": ""Brief description"", ""goal"": ""Specific investigation goal""}}
  ]
}}
```

**Option 2 - Direct Answer** (use ONLY when you are confident about the answer based on general knowledge):
Provide the answer directly. End your response with JSON format:
```json
{{
  ""directAnswer"": ""Your complete answer to the user's question""
}}
```
";
        }

        private string GetResearchSystemPrompt()
        {
            return @"You are conducting a specific research task as part of analyzing an MSBuild log.

Use the available tools to investigate thoroughly.
Focus only on your specific task goal.
Be concise but complete in your findings.
Output your findings as a clear summary that will be used by another agent to synthesize the final answer.";
        }

        private string GetSummarizationSystemPrompt()
        {
            var basePrompt = @"You are synthesizing research findings to answer the user's question about their MSBuild log.

Your task:
1. Review all research findings
2. Synthesize them into a coherent, well-structured answer
3. Provide clear, actionable insights

Format your answer with markdown for readability.
Be helpful and specific.";

            // Check if we have GUI manipulation tools available
            if (HasGuiManipulationTools())
            {
                basePrompt += @"

You have access to GUI manipulation tools that can help users visualize and explore findings:
- Use tools like SelectNodeByTextAsync, SelectErrorAsync, SelectWarningAsync to navigate the UI to relevant nodes
- Use OpenTimelineAsync, OpenTracingAsync, PerformSearchAsync to switch views and help users explore data
- Use these tools proactively when they can help clarify or support your findings
- These tools make your answer interactive - leverage them to provide a better user experience

When presenting findings, consider:
- Which errors/warnings should be highlighted for the user to see?
- What nodes or files would help illustrate the issue?
- Would timeline or tracing views provide useful context?
- Should the user see specific search results?

Use GUI tools to make your insights actionable and immediately explorable by the user.";
            }

            return basePrompt;
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Generates a formatted string describing available tools based on AIFunction metadata.
        /// </summary>
        private string GetToolDescriptions(AIFunction[] tools)
        {
            var descriptions = new StringBuilder();
            
            foreach (var tool in tools)
            {
                descriptions.AppendLine($"- {tool.Name}: {tool.Description}");
                
                // Try to include parameter info from JSON schema
                try
                {
                    var schema = tool.JsonSchema;
                    if (schema.ValueKind == JsonValueKind.Object &&
                        schema.TryGetProperty("properties", out var properties) &&
                        properties.ValueKind == JsonValueKind.Object)
                    {
                        var paramNames = new List<string>();
                        foreach (var property in properties.EnumerateObject())
                        {
                            paramNames.Add(property.Name);
                        }
                        
                        if (paramNames.Count > 0)
                        {
                            descriptions.AppendLine($"  Parameters: {string.Join(", ", paramNames)}");
                        }
                    }
                }
                catch
                {
                    // If schema parsing fails, skip parameter details
                }
            }
            
            return descriptions.ToString();
        }

        /// <summary>
        /// Checks if any registered tool containers provide GUI manipulation tools.
        /// </summary>
        private bool HasGuiManipulationTools()
        {
            return toolContainers.Any(container => container.HasGuiTools);
        }

        /// <summary>
        /// Extracts thinking/reasoning and plan JSON from a response.
        /// Returns the thinking text and the JSON plan separately.
        /// </summary>
        private (string thinking, string planJson) ExtractThinkingAndPlan(string response)
        {
            if (string.IsNullOrWhiteSpace(response))
            {
                return (string.Empty, "{}");
            }

            // Strategy: Find the last JSON block in the response
            // Everything before it is thinking, the JSON block is the plan

            // First, try to find JSON in markdown code blocks
            int lastJsonBlockStart = response.LastIndexOf("```json", StringComparison.OrdinalIgnoreCase);
            if (lastJsonBlockStart < 0)
            {
                lastJsonBlockStart = response.LastIndexOf("```", StringComparison.Ordinal);
            }

            if (lastJsonBlockStart >= 0)
            {
                // Found a code block, extract JSON from it
                int jsonContentStart = response.IndexOf('\n', lastJsonBlockStart) + 1;
                int jsonBlockEnd = response.IndexOf("```", jsonContentStart, StringComparison.Ordinal);

                if (jsonBlockEnd > jsonContentStart)
                {
                    string thinking = response.Substring(0, lastJsonBlockStart).Trim();
                    string planJson = response.Substring(jsonContentStart, jsonBlockEnd - jsonContentStart).Trim();
                    return (thinking, planJson);
                }
            }

            // No markdown code block, try to find raw JSON
            // Look for the last occurrence of { followed by "tasks"
            int lastJsonStart = -1;
            int searchPos = 0;
            
            while (true)
            {
                int pos = response.IndexOf("{", searchPos, StringComparison.Ordinal);
                if (pos < 0)
                {
                    break;
                }

                // Check if this looks like our tasks JSON
                int tasksPos = response.IndexOf("\"tasks\"", pos, StringComparison.Ordinal);
                if (tasksPos > pos && tasksPos < pos + 50) // Within reasonable distance
                {
                    lastJsonStart = pos;
                }
                
                searchPos = pos + 1;
            }

            if (lastJsonStart >= 0)
            {
                // Try to find the matching closing brace
                int braceCount = 0;
                int jsonEnd = -1;
                
                for (int i = lastJsonStart; i < response.Length; i++)
                {
                    if (response[i] == '{')
                    {
                        braceCount++;
                    }
                    else if (response[i] == '}') 
                    {
                        braceCount--;
                        if (braceCount == 0)
                        {
                            jsonEnd = i + 1;
                            break;
                        }
                    }
                }

                if (jsonEnd > lastJsonStart)
                {
                    string thinking = response.Substring(0, lastJsonStart).Trim();
                    string planJson = response.Substring(lastJsonStart, jsonEnd - lastJsonStart).Trim();
                    return (thinking, planJson);
                }
            }

            // Fallback: treat entire response as JSON, no thinking
            return (string.Empty, response.Trim());
        }

        #endregion

        #region JSON Models

        private class PlanResponse
        {
            public List<TaskData> Tasks { get; set; } = new List<TaskData>();
            public string? DirectAnswer { get; set; }
        }

        private class TaskData
        {
            public string Id { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string Goal { get; set; } = string.Empty;
        }

        #endregion

        public void Dispose()
        {
            if (llmClient is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}

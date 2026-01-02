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

            toolContainers.Add(executor);
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

Create a research plan with 2-{MaxResearchTasks} specific tasks to answer this question.
Return ONLY a JSON object with this exact structure (no markdown, no extra text):
{{
  ""tasks"": [
    {{""id"": ""task1"", ""description"": ""Brief description"", ""goal"": ""Specific investigation goal""}},
    {{""id"": ""task2"", ""description"": ""Brief description"", ""goal"": ""Specific investigation goal""}}
  ]
}}";

            var messages = new List<ChatMessage>
            {
                new ChatMessage(ChatRole.System, systemPrompt),
                new ChatMessage(ChatRole.User, userPrompt)
            };

            // Get tools for planning phase (minimal tools)
            var planningTools = GetToolsForPhase(AgentPhase.Planning);

            var options = new ChatOptions
            {
                Tools = planningTools,
                Temperature = 0.3f
            };

            var response = await llmClient!.CompleteChatAsync(messages, options, cancellationToken);
            var planJson = response.Text?.Trim() ?? "";

            // Try to extract JSON if wrapped in markdown
            planJson = ExtractJsonFromResponse(planJson);

            // Parse the plan
            try
            {
                var jsonOptions = new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                };
                var planData = JsonSerializer.Deserialize<PlanResponse>(planJson, jsonOptions);
                if (planData?.Tasks != null && planData.Tasks.Count > 0)
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
                    throw new Exception("No tasks in plan response.");
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

            var userPrompt = $@"User Question: {plan.UserQuery}

Research Findings:
{allFindings}

Your task:
1. Synthesize the findings into a coherent, well-structured answer
2. Provide clear, actionable insights

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
                        var monitored = new MonitoredAIFunction(function);
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

        #region System Prompts

        private string GetPlanningSystemPrompt(string researchToolDescriptions)
        {
            return $@"You are an expert MSBuild log analyzer creating a research plan.

Your task: Break down the user's question into 2-{MaxResearchTasks} specific research tasks.
Each task should:
- Have a clear, specific goal
- Be designed to use the tools that will be available to the research agents
- Produce findings that contribute to answering the user's question

The research agents will have access to the following tools:

{researchToolDescriptions}

Consider these tools when designing your research plan. All tools are read-only - same as the actual overall investigation you are planning. Each task should leverage the appropriate tools to gather the necessary information.

Output ONLY valid JSON in this exact format:
{{
  ""tasks"": [
    {{""id"": ""task1"", ""description"": ""Brief description"", ""goal"": ""Specific investigation goal""}},
    {{""id"": ""task2"", ""description"": ""Brief description"", ""goal"": ""Specific investigation goal""}}
  ]
}}

Do not include markdown formatting or any text outside the JSON object.";
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
            return @"You are synthesizing research findings to answer the user's question about their MSBuild log.

Your task:
1. Review all research findings
2. Synthesize them into a coherent, well-structured answer
3. Provide clear, actionable insights

Format your answer with markdown for readability.
Be helpful and specific.";
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

        private string ExtractJsonFromResponse(string text)
        {
            // Try to extract JSON from markdown code blocks
            int jsonStart = text.IndexOf("```json", StringComparison.OrdinalIgnoreCase);
            if (jsonStart < 0)
            {
                // Try just ```
                jsonStart = text.IndexOf("```", StringComparison.Ordinal);
            }

            if (jsonStart >= 0)
            {
                jsonStart = text.IndexOf('\n', jsonStart) + 1;
                // Find the closing marker, searching from after the opening marker
                int jsonEnd = text.LastIndexOf("```", StringComparison.Ordinal);

                if (jsonEnd > jsonStart)
                {
                    return text.Substring(jsonStart, jsonEnd - jsonStart).Trim();
                }
            }

            return text.Trim();
        }

        #endregion

        #region JSON Models

        private class PlanResponse
        {
            public List<TaskData> Tasks { get; set; } = new List<TaskData>();
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

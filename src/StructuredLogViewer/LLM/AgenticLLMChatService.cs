using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using StructuredLogViewer.Controls;

namespace StructuredLogViewer.LLM
{
    /// <summary>
    /// Orchestrates agentic multi-step reasoning for complex log analysis.
    /// Manages planning, research, and summarization phases.
    /// </summary>
    public class AgenticLLMChatService : IDisposable
    {
        private readonly Microsoft.Build.Logging.StructuredLogger.Build build;
        private readonly BinlogContextProvider contextProvider;
        private readonly AsyncBinlogToolExecutor toolExecutor;
        private readonly AsyncBinlogUIInteractionExecutor uiInteractionExecutor;
        private readonly AsyncEmbeddedFilesToolExecutor embeddedFilesExecutor;
        private readonly AzureFoundryLLMClient llmClient;
        private readonly LLMConfiguration configuration;

        public event EventHandler<AgentProgressEventArgs> ProgressUpdated;
        public event EventHandler<ChatMessageViewModel> MessageAdded;
        public event EventHandler<ToolCallInfo> ToolCallExecuting;
        public event EventHandler<ToolCallInfo> ToolCallExecuted;

        public bool IsConfigured => configuration?.IsConfigured ?? false;

        // Configuration
        public int MaxResearchTasks { get; set; } = 5;
        public int MaxTokensPerTask { get; set; } = 4000;

        public AgenticLLMChatService(Microsoft.Build.Logging.StructuredLogger.Build build, BuildControl buildControl, LLMConfiguration config)
        {
            this.build = build ?? throw new ArgumentNullException(nameof(build));
            this.contextProvider = new BinlogContextProvider(build);
            this.toolExecutor = new AsyncBinlogToolExecutor(build);
            this.uiInteractionExecutor = buildControl != null ? new AsyncBinlogUIInteractionExecutor(build, buildControl) : null;
            this.embeddedFilesExecutor = new AsyncEmbeddedFilesToolExecutor(build);
            this.configuration = config ?? throw new ArgumentNullException(nameof(config));

            if (configuration.IsConfigured)
            {
                llmClient = new AzureFoundryLLMClient(configuration);
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

                plan.Phase = AgentPhase.Complete;
                plan.EndTime = DateTime.Now;
                RaiseProgress(plan, "Agent workflow completed successfully.");

                return plan.FinalSummary ?? "No summary generated.";
            }
            catch (OperationCanceledException)
            {
                plan.Phase = AgentPhase.Failed;
                plan.Error = "Operation cancelled by user.";
                RaiseProgress(plan, plan.Error, isError: true);
                return plan.Error;
            }
            catch (Exception ex)
            {
                plan.Phase = AgentPhase.Failed;
                plan.Error = $"Agent workflow failed: {ex.Message}";
                RaiseProgress(plan, plan.Error, isError: true);
                return plan.Error;
            }
        }

        /// <summary>
        /// Phase 1: Generate a research plan by breaking down the user query.
        /// </summary>
        private async Task PlanningPhaseAsync(AgentPlan plan, CancellationToken cancellationToken)
        {
            plan.Phase = AgentPhase.Planning;
            RaiseProgress(plan, "Creating research plan...");

            var systemPrompt = GetPlanningSystemPrompt();
            var userPrompt = $@"User Question: {plan.UserQuery}

Build Overview:
{contextProvider.GetBuildOverview()}

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

            var response = await llmClient.ChatClient.GetResponseAsync(messages, options, cancellationToken);
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
        private async Task ResearchPhaseAsync(AgentPlan plan, CancellationToken cancellationToken)
        {
            plan.Phase = AgentPhase.Research;
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

            // Build context from previous findings
            var previousFindings = new StringBuilder();
            foreach (var kvp in plan.Findings)
            {
                if (kvp.Key != task.Id) // Don't include current task
                {
                    previousFindings.AppendLine($"[{kvp.Key}]: {kvp.Value}");
                    previousFindings.AppendLine();
                }
            }

            var userPrompt = $@"Task Goal: {task.Goal}
Task Description: {task.Description}

Build Overview:
{contextProvider.GetBuildOverview()}

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

            var response = await llmClient.ChatClient.GetResponseAsync(messages, options, cancellationToken);
            return response.Text ?? "No findings generated.";
        }

        /// <summary>
        /// Phase 3: Synthesize all findings and present final answer with UI manipulation.
        /// </summary>
        private async Task SummarizationPhaseAsync(AgentPlan plan, CancellationToken cancellationToken)
        {
            plan.Phase = AgentPhase.Summarization;
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
2. Use UI tools to navigate to relevant parts of the log (errors, projects, files)
3. Provide clear, actionable insights

You have access to UI manipulation tools to help the user see relevant parts of their build.
Format your answer with markdown for readability.";

            var messages = new List<ChatMessage>
            {
                new ChatMessage(ChatRole.System, systemPrompt),
                new ChatMessage(ChatRole.User, userPrompt)
            };

            // Get ALL tools for summarization (including UI tools)
            var allTools = GetToolsForPhase(AgentPhase.Summarization);

            var options = new ChatOptions
            {
                Tools = allTools,
                Temperature = 0.7f
            };

            var response = await llmClient.ChatClient.GetResponseAsync(messages, options, cancellationToken);
            plan.FinalSummary = response.Text ?? "No summary generated.";

            RaiseProgress(plan, "Summarization complete.");
        }

        /// <summary>
        /// Get tools appropriate for the current phase.
        /// </summary>
        private AIFunction[] GetToolsForPhase(AgentPhase phase)
        {
            var baseFunctions = new List<AIFunction>();

            try
            {
                switch (phase)
                {
                    case AgentPhase.Planning:
                        // Minimal tools for planning
                        baseFunctions.Add(AIFunctionFactory.Create(toolExecutor.GetBuildSummary));
                        break;

                    case AgentPhase.Research:
                        // All investigation tools, no UI manipulation
                        baseFunctions.Add(AIFunctionFactory.Create(toolExecutor.GetBuildSummary));
                        baseFunctions.Add(AIFunctionFactory.Create(toolExecutor.SearchNodes));
                        baseFunctions.Add(AIFunctionFactory.Create(toolExecutor.GetErrorsAndWarnings));
                        baseFunctions.Add(AIFunctionFactory.Create(toolExecutor.GetProjects));
                        baseFunctions.Add(AIFunctionFactory.Create(toolExecutor.GetProjectTargets));
                        baseFunctions.Add(AIFunctionFactory.Create(embeddedFilesExecutor.ListEmbeddedFiles));
                        baseFunctions.Add(AIFunctionFactory.Create(embeddedFilesExecutor.SearchEmbeddedFiles));
                        baseFunctions.Add(AIFunctionFactory.Create(embeddedFilesExecutor.ReadEmbeddedFileLines));
                        break;

                    case AgentPhase.Summarization:
                        // All tools including UI manipulation
                        baseFunctions.Add(AIFunctionFactory.Create(toolExecutor.GetBuildSummary));
                        baseFunctions.Add(AIFunctionFactory.Create(toolExecutor.SearchNodes));
                        baseFunctions.Add(AIFunctionFactory.Create(toolExecutor.GetErrorsAndWarnings));
                        baseFunctions.Add(AIFunctionFactory.Create(toolExecutor.GetProjects));
                        baseFunctions.Add(AIFunctionFactory.Create(toolExecutor.GetProjectTargets));
                        baseFunctions.Add(AIFunctionFactory.Create(embeddedFilesExecutor.ListEmbeddedFiles));
                        baseFunctions.Add(AIFunctionFactory.Create(embeddedFilesExecutor.SearchEmbeddedFiles));
                        baseFunctions.Add(AIFunctionFactory.Create(embeddedFilesExecutor.ReadEmbeddedFileLines));

                        // UI tools
                        if (uiInteractionExecutor != null)
                        {
                            baseFunctions.Add(AIFunctionFactory.Create(uiInteractionExecutor.SelectNodeByText));
                            baseFunctions.Add(AIFunctionFactory.Create(uiInteractionExecutor.SelectError));
                            baseFunctions.Add(AIFunctionFactory.Create(uiInteractionExecutor.SelectWarning));
                            baseFunctions.Add(AIFunctionFactory.Create(uiInteractionExecutor.SelectProject));
                            baseFunctions.Add(AIFunctionFactory.Create(uiInteractionExecutor.OpenFile));
                            baseFunctions.Add(AIFunctionFactory.Create(uiInteractionExecutor.OpenTimeline));
                            baseFunctions.Add(AIFunctionFactory.Create(uiInteractionExecutor.OpenTracing));
                            baseFunctions.Add(AIFunctionFactory.Create(uiInteractionExecutor.PerformSearch));
                        }
                        break;
                }

                // Wrap all functions with monitoring
                var tools = new List<AIFunction>();
                foreach (var baseFunction in baseFunctions)
                {
                    var monitored = new MonitoredAIFunction(baseFunction);
                    monitored.ToolCallStarted += OnToolCallStarted;
                    monitored.ToolCallCompleted += OnToolCallCompleted;
                    tools.Add(monitored);
                }

                return tools.ToArray();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating tools for phase {phase}: {ex.Message}");
                return Array.Empty<AIFunction>();
            }
        }

        private void OnToolCallStarted(object sender, ToolCallInfo toolCallInfo)
        {
            // Raise event for UI consumption
            ToolCallExecuting?.Invoke(this, toolCallInfo);
        }

        private void OnToolCallCompleted(object sender, ToolCallInfo toolCallInfo)
        {
            // Raise event for UI consumption
            ToolCallExecuted?.Invoke(this, toolCallInfo);
        }

        private void RaiseProgress(AgentPlan plan, string message = null, bool isError = false)
        {
            ProgressUpdated?.Invoke(this, new AgentProgressEventArgs(plan, message, isError));
        }

        #region System Prompts

        private string GetPlanningSystemPrompt()
        {
            return @"You are an expert MSBuild log analyzer creating a research plan.

Your task: Break down the user's question into 2-5 specific research tasks.
Each task should:
- Have a clear, specific goal
- Use only read-only investigation tools
- Produce findings that contribute to answering the user's question

Available tools for research:
- GetBuildSummary: Get build status, duration, error/warning counts
- SearchNodes: Search for nodes in build tree
- GetErrorsAndWarnings: List errors and/or warnings
- GetProjects: List all projects with status
- GetProjectTargets: Get targets for a project
- ListEmbeddedFiles: List embedded source files
- SearchEmbeddedFiles: Search within embedded files
- ReadEmbeddedFileLines: Read specific lines from embedded files

Output ONLY valid JSON in this exact format:
{
  ""tasks"": [
    {""id"": ""task1"", ""description"": ""Brief description"", ""goal"": ""Specific investigation goal""},
    {""id"": ""task2"", ""description"": ""Brief description"", ""goal"": ""Specific investigation goal""}
  ]
}

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
3. Use UI manipulation tools to navigate the viewer to relevant parts (errors, projects, files)
4. Provide clear, actionable insights

Format your answer with markdown for readability.
Be helpful and specific.
Use UI tools to enhance the user experience by showing them relevant parts of their build.";
        }

        #endregion

        #region Helper Methods

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
            public List<TaskData> Tasks { get; set; }
        }

        private class TaskData
        {
            public string Id { get; set; }
            public string Description { get; set; }
            public string Goal { get; set; }
        }

        #endregion

        public void Dispose()
        {
            llmClient?.Dispose();
        }
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Logging.StructuredLogger;
using Microsoft.Extensions.AI;

namespace StructuredLogger.LLM
{
    /// <summary>
    /// Tool executor for searching and analyzing binlog build data.
    /// Provides core binlog analysis capabilities to the LLM.
    /// </summary>
    public class BinlogToolExecutor : IToolsContainer
    {
        private readonly Build build;
        private readonly BinlogToolExecutorCore core;

        public BinlogToolExecutor(Build build)
        {
            this.build = build ?? throw new ArgumentNullException(nameof(build));
            this.core = new BinlogToolExecutorCore(build);
        }

        public IEnumerable<(AIFunction Function, AgentPhase ApplicablePhases)> GetTools()
        {
            // Return all tools with their applicable phases
            yield return (AIFunctionFactory.Create(GetBuildSummaryAsync), AgentPhase.All);
            yield return (AIFunctionFactory.Create(SearchNodesAsync), AgentPhase.Research | AgentPhase.Summarization);
            yield return (AIFunctionFactory.Create(GetErrorsAndWarningsAsync), AgentPhase.All);
            yield return (AIFunctionFactory.Create(GetProjectsAsync), AgentPhase.Research | AgentPhase.Summarization);
            yield return (AIFunctionFactory.Create(GetProjectTargetsAsync), AgentPhase.Research | AgentPhase.Summarization);
        }

        [Description("Searches for nodes in the build tree by text or pattern. Returns matching nodes.")]
        public async System.Threading.Tasks.Task<string> SearchNodesAsync(
            [Description("The search query text or pattern")] string query,
            [Description("Maximum number of results to return (default 10)")] int maxResults = 10)
        {
            return await System.Threading.Tasks.Task.Run(() => core.SearchNodes(query, maxResults));
        }

        [Description("Gets a summary of the build including status, duration, errors and warnings count")]
        public async System.Threading.Tasks.Task<string> GetBuildSummaryAsync()
        {
            return await System.Threading.Tasks.Task.Run(() => core.GetBuildSummary());
        }

        [Description("Gets all errors and warnings from the build with their details")]
        public async System.Threading.Tasks.Task<string> GetErrorsAndWarningsAsync(
            [Description("Type of messages to retrieve: 'errors', 'warnings', or 'all'")] string type = "all")
        {
            return await System.Threading.Tasks.Task.Run(() => core.GetErrorsAndWarnings(type));
        }

        [Description("Gets list of all projects built with their status and duration")]
        public async System.Threading.Tasks.Task<string> GetProjectsAsync(
            [Description("Maximum number of projects to return (default 50)")] int maxResults = 50)
        {
            return await System.Threading.Tasks.Task.Run(() => core.GetProjects(maxResults));
        }

        [Description("Gets targets executed in a specific project")]
        public async System.Threading.Tasks.Task<string> GetProjectTargetsAsync(
            [Description("Name of the project to get targets for")] string projectName)
        {
            return await System.Threading.Tasks.Task.Run(() => core.GetProjectTargets(projectName));
        }
    }
}

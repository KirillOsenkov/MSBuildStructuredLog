using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.Build.Logging.StructuredLogger;

namespace StructuredLogViewer.LLM
{
    /// <summary>
    /// Async wrapper for BinlogToolExecutor that ensures all tool methods execute on background threads.
    /// This prevents UI freezing when the LLM calls these tools.
    /// </summary>
    public class AsyncBinlogToolExecutor
    {
        private readonly BinlogToolExecutor innerExecutor;

        public AsyncBinlogToolExecutor(Build build)
        {
            this.innerExecutor = new BinlogToolExecutor(build);
        }

        [Description("Gets a summary of the build including status, duration, errors and warnings count")]
        public async System.Threading.Tasks.Task<string> GetBuildSummary()
        {
            return await System.Threading.Tasks.Task.Run(() => innerExecutor.GetBuildSummary()).ConfigureAwait(false);
        }

        [Description("Searches for nodes in the build tree by text or pattern given via 'query' argument. Returns matching nodes.")]
        public async System.Threading.Tasks.Task<string> SearchNodes(
            [Description("The search query text or pattern")] string query,
            [Description("Maximum number of results to return (default 10)")] int maxResults = 10)
        {
            return await System.Threading.Tasks.Task.Run(() => innerExecutor.SearchNodes(query, maxResults)).ConfigureAwait(false);
        }

        [Description("Gets all errors from the build with their details")]
        public async System.Threading.Tasks.Task<string> GetErrorsAndWarnings(
            [Description("Type of messages to retrieve: 'errors', 'warnings', or 'all'")] string type = "all")
        {
            return await System.Threading.Tasks.Task.Run(() => innerExecutor.GetErrorsAndWarnings(type)).ConfigureAwait(false);
        }

        [Description("Gets list of all projects built with their status and duration")]
        public async System.Threading.Tasks.Task<string> GetProjects()
        {
            return await System.Threading.Tasks.Task.Run(() => innerExecutor.GetProjects()).ConfigureAwait(false);
        }

        [Description("Gets targets executed in a specific project")]
        public async System.Threading.Tasks.Task<string> GetProjectTargets(
            [Description("Name of the project to get targets for")] string projectName)
        {
            return await System.Threading.Tasks.Task.Run(() => innerExecutor.GetProjectTargets(projectName)).ConfigureAwait(false);
        }
    }
}

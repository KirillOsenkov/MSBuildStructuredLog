using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Logging.StructuredLogger;
using Microsoft.Extensions.AI;

namespace StructuredLogger.LLM
{
    /// <summary>
    /// Tool executor for accessing and searching embedded source files in the binlog.
    /// Applicable primarily during Research phase when investigating code details.
    /// </summary>
    public class EmbeddedFilesToolExecutor : IToolsContainer
    {
        private readonly Build build;
        private readonly EmbeddedFilesToolExecutorCore core;

        public EmbeddedFilesToolExecutor(Build build)
        {
            this.build = build ?? throw new ArgumentNullException(nameof(build));
            this.core = new EmbeddedFilesToolExecutorCore(build);
        }

        public bool HasGuiTools => false;

        public IEnumerable<(AIFunction Function, AgentPhase ApplicablePhases)> GetTools()
        {
            // Return all tools with their applicable phases
            yield return (AIFunctionFactory.Create(ListEmbeddedFilesAsync), AgentPhase.Research | AgentPhase.Summarization);
            yield return (AIFunctionFactory.Create(SearchEmbeddedFilesAsync), AgentPhase.Research | AgentPhase.Summarization);
            yield return (AIFunctionFactory.Create(ReadEmbeddedFileLinesAsync), AgentPhase.Research | AgentPhase.Summarization);
        }

        [Description("Lists all embedded files in the binlog with their paths. Optionally filters by regex pattern.")]
        public async System.Threading.Tasks.Task<string> ListEmbeddedFilesAsync(
            [Description("Optional regex pattern to filter file paths")] string? pathPattern = null,
            [Description("Maximum number of files to return (default 100)")] int maxResults = 100)
        {
            return await System.Threading.Tasks.Task.Run(() => core.ListEmbeddedFiles(pathPattern, maxResults));
        }

        [Description("Searches for text patterns within embedded files using regex.")]
        public async System.Threading.Tasks.Task<string> SearchEmbeddedFilesAsync(
            [Description("Regex pattern to search for in file contents")] string searchPattern,
            [Description("Optional regex pattern to filter which files to search")] string? filePathPattern = null,
            [Description("Maximum number of matches to return")] int maxMatches = 20)
        {
            return await System.Threading.Tasks.Task.Run(() => core.SearchEmbeddedFiles(searchPattern, filePathPattern, maxMatches));
        }

        [Description("Reads a specific range of lines from an embedded file.")]
        public async System.Threading.Tasks.Task<string> ReadEmbeddedFileLinesAsync(
            [Description("Full path of the embedded file to read")] string filePath,
            [Description("Starting line number (1-based)")] int startLine = 1,
            [Description("Ending line number (1-based, -1 for end of file)")] int endLine = -1,
            [Description("Maximum number of lines to return")] int maxLines = 100)
        {
            return await System.Threading.Tasks.Task.Run(() => core.ReadEmbeddedFileLines(filePath, startLine, endLine, maxLines));
        }
    }
}

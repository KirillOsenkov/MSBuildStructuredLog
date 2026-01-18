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
    /// Supports multiple binlog files with optional buildId parameter.
    /// </summary>
    public class EmbeddedFilesToolExecutor : IToolsContainer
    {
        private readonly MultiBuildContext context;

        /// <summary>
        /// Creates an EmbeddedFilesToolExecutor with multi-build support.
        /// </summary>
        /// <param name="context">The multi-build context containing loaded builds.</param>
        public EmbeddedFilesToolExecutor(MultiBuildContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// Creates an EmbeddedFilesToolExecutor for a single build (backward compatibility).
        /// </summary>
        /// <param name="build">The build to analyze.</param>
        public EmbeddedFilesToolExecutor(Build build)
            : this(CreateSingleBuildContext(build))
        {
        }

        private static MultiBuildContext CreateSingleBuildContext(Build build)
        {
            if (build == null)
            {
                throw new ArgumentNullException(nameof(build));
            }
            var ctx = new MultiBuildContext();
            ctx.AddBuild(build);
            return ctx;
        }

        /// <summary>
        /// Resolves a build and returns both the Build and friendly name.
        /// </summary>
        private (Build build, string friendlyName) ResolveBuildWithName(string buildId)
        {
            if (string.IsNullOrEmpty(buildId))
            {
                var primary = context.GetPrimaryBuild();
                return (primary.Build, primary.FriendlyName);
            }

            if (!context.TryGetBuild(buildId, out var buildInfo))
            {
                throw new ArgumentException($"Build '{buildId}' not found. Use ListBuilds to see available builds.");
            }

            return (buildInfo.Build, buildInfo.FriendlyName);
        }

        public bool HasGuiTools => false;

        public IEnumerable<(AIFunction Function, AgentPhase ApplicablePhases)> GetTools()
        {
            // Return all tools with their applicable phases
            yield return (AIFunctionFactory.Create(ListEmbeddedFilesAsync), AgentPhase.Research | AgentPhase.Summarization);
            yield return (AIFunctionFactory.Create(SearchEmbeddedFilesAsync), AgentPhase.Research | AgentPhase.Summarization);
            yield return (AIFunctionFactory.Create(ReadEmbeddedFileLinesAsync), AgentPhase.Research | AgentPhase.Summarization);
        }

        [Description("Lists all embedded files in the binlog with their paths. Optionally filters by regex pattern. Use buildId to target a specific build.")]
        public async System.Threading.Tasks.Task<string> ListEmbeddedFilesAsync(
            [Description("Optional regex pattern to filter file paths")] string pathPattern = null,
            [Description("Maximum number of files to return (default 100)")] int maxResults = 100,
            [Description("Build ID to query. Omit for primary build. Use ListBuilds to see available builds.")] string buildId = null)
        {
            return await System.Threading.Tasks.Task.Run(() =>
            {
                var (build, friendlyName) = ResolveBuildWithName(buildId);
                var core = new EmbeddedFilesToolExecutorCore(build);
                var result = core.ListEmbeddedFiles(pathPattern, maxResults);

                if (context.BuildCount > 1)
                {
                    return $"[Embedded files from {friendlyName}]\n{result}";
                }
                return result;
            });
        }

        [Description("Searches for text patterns within embedded files using regex. Use buildId to target a specific build.")]
        public async System.Threading.Tasks.Task<string> SearchEmbeddedFilesAsync(
            [Description("Regex pattern to search for in file contents")] string searchPattern,
            [Description("Optional regex pattern to filter which files to search")] string filePathPattern = null,
            [Description("Maximum number of matches to return")] int maxMatches = 20,
            [Description("Build ID to query. Omit for primary build. Use ListBuilds to see available builds.")] string buildId = null)
        {
            return await System.Threading.Tasks.Task.Run(() =>
            {
                var (build, friendlyName) = ResolveBuildWithName(buildId);
                var core = new EmbeddedFilesToolExecutorCore(build);
                var result = core.SearchEmbeddedFiles(searchPattern, filePathPattern, maxMatches);

                if (context.BuildCount > 1)
                {
                    return $"[Search results from {friendlyName}]\n{result}";
                }
                return result;
            });
        }

        [Description("Reads a specific range of lines from an embedded file. Use buildId to target a specific build.")]
        public async System.Threading.Tasks.Task<string> ReadEmbeddedFileLinesAsync(
            [Description("Full path of the embedded file to read")] string filePath,
            [Description("Starting line number (1-based)")] int startLine = 1,
            [Description("Ending line number (1-based, -1 for end of file)")] int endLine = -1,
            [Description("Maximum number of lines to return")] int maxLines = 100,
            [Description("Build ID to query. Omit for primary build. Use ListBuilds to see available builds.")] string buildId = null)
        {
            return await System.Threading.Tasks.Task.Run(() =>
            {
                var (build, friendlyName) = ResolveBuildWithName(buildId);
                var core = new EmbeddedFilesToolExecutorCore(build);
                var result = core.ReadEmbeddedFileLines(filePath, startLine, endLine, maxLines);

                if (context.BuildCount > 1)
                {
                    return $"[File from {friendlyName}]\n{result}";
                }
                return result;
            });
        }
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Logging.StructuredLogger;
using Microsoft.Extensions.AI;

namespace StructuredLogger.LLM
{
    /// <summary>
    /// Tool executor for searching and analyzing binlog build data.
    /// Provides core binlog analysis capabilities to the LLM.
    /// Supports multiple binlog files with optional buildId parameter.
    /// </summary>
    public class BinlogToolExecutor : IToolsContainer
    {
        private readonly MultiBuildContext context;

        /// <summary>
        /// Creates a BinlogToolExecutor with multi-build support.
        /// </summary>
        /// <param name="context">The multi-build context containing loaded builds.</param>
        public BinlogToolExecutor(MultiBuildContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// Creates a BinlogToolExecutor for a single build (backward compatibility).
        /// </summary>
        /// <param name="build">The build to analyze.</param>
        public BinlogToolExecutor(Build build)
            : this(CreateSingleBuildContext(build))
        {
        }

        private static MultiBuildContext CreateSingleBuildContext(Build build)
        {
            if (build == null)
            {
                throw new ArgumentNullException(nameof(build));
            }
            var context = new MultiBuildContext();
            context.AddBuild(build);
            return context;
        }

        public bool HasGuiTools => false;

        public IEnumerable<(AIFunction Function, AgentPhase ApplicablePhases)> GetTools()
        {
            // Return all tools with their applicable phases
            yield return (AIFunctionFactory.Create(ListBuildsAsync), AgentPhase.All);
            yield return (AIFunctionFactory.Create(GetBuildSummaryAsync), AgentPhase.All);
            yield return (AIFunctionFactory.Create(SearchNodesAsync), AgentPhase.Research | AgentPhase.Summarization);
            yield return (AIFunctionFactory.Create(GetErrorsAndWarningsAsync), AgentPhase.All);
            yield return (AIFunctionFactory.Create(GetProjectsAsync), AgentPhase.Research | AgentPhase.Summarization);
            yield return (AIFunctionFactory.Create(GetProjectTargetsAsync), AgentPhase.Research | AgentPhase.Summarization);
        }

        /// <summary>
        /// Resolves a build from an optional buildId parameter.
        /// </summary>
        private Build ResolveBuild(string buildId)
        {
            if (string.IsNullOrEmpty(buildId))
            {
                return context.GetPrimaryBuild().Build;
            }

            if (!context.TryGetBuild(buildId, out var buildInfo))
            {
                throw new ArgumentException($"Build '{buildId}' not found. Use ListBuilds to see available builds.");
            }

            return buildInfo.Build;
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

        [Description(@"Lists all loaded binlog files with their IDs, friendly names, full paths, and summary.
Use this to discover which builds are available and get their buildId for other tools.
The primary build (used when buildId is omitted) is marked with [PRIMARY].")]
        public async System.Threading.Tasks.Task<string> ListBuildsAsync()
        {
            return await System.Threading.Tasks.Task.Run(() =>
            {
                var sb = new StringBuilder();
                sb.AppendLine("Loaded Builds:");
                sb.AppendLine();

                foreach (var buildInfo in context.GetAllBuilds())
                {
                    var primary = buildInfo.IsPrimary ? " [PRIMARY]" : "";
                    var status = buildInfo.Succeeded ? "Succeeded" : "FAILED";

                    sb.AppendLine($"Build ID: {buildInfo.BuildId}{primary}");
                    sb.AppendLine($"  Name: {buildInfo.FriendlyName}");
                    sb.AppendLine($"  Path: {buildInfo.FullPath}");
                    sb.AppendLine($"  Status: {status}");
                    sb.AppendLine($"  Duration: {buildInfo.DurationText}");
                    sb.AppendLine($"  Errors: {buildInfo.ErrorCount}, Warnings: {buildInfo.WarningCount}");
                    sb.AppendLine();
                }

                return sb.ToString();
            });
        }

        [Description(@"Searches the build tree using advanced query syntax. Supports multiple search operators:

BASIC SEARCH:
  - Multiple words: space = AND operator (e.g., 'csc error' finds nodes with both terms)
  - Exact phrase: Use double quotes (e.g., '""Copying file""' for exact match)
  - Single word in quotes: Exact match, no substring (e.g., '""Build""' matches 'Build' but not 'PreBuild')

NODE TYPE FILTERS:
  - $project: Search for projects (e.g., '$project MyApp')
  - $target: Search for targets (e.g., '$target Build')
  - $task: Search for tasks (e.g., '$task Csc' or '$csc' shorthand)
  - $error: Search for errors
  - $warning: Search for warnings
  - $message: Search for messages
  - $property: Search for properties
  - $item: Search for items
  - $metadata: Search for metadata
  - $copy: Search for file copy operations (e.g., '$copy MyFile.dll')
  - $nuget: Search NuGet packages (e.g., '$nuget Newtonsoft')

HIERARCHY FILTERS:
  - under(FILTER): Include only results under nodes matching FILTER (e.g., 'error under($project MyApp)')
  - notunder(FILTER): Exclude results under nodes matching FILTER
  - project(NAME): Filter by parent project (e.g., 'Csc project(MyApp.csproj)')
  - not(FILTER): Exclude results matching FILTER

PROPERTY/ITEM FILTERS:
  - name=VALUE: Match by name (e.g., '$property name=Configuration')
  - value=VALUE: Match by value (e.g., '$property value=Debug')
  - skipped=true/false: Filter targets by skipped status (e.g., '$target skipped=false')

TIME-BASED FILTERS:
  - start<""TIMESTAMP"": Events starting before timestamp
  - start>""TIMESTAMP"": Events starting after timestamp
  - end<""TIMESTAMP"": Events ending before timestamp
  - end>""TIMESTAMP"": Events ending after timestamp
  - Example: '$task start>""2023-11-23 14:30:00"" end<""2023-11-23 14:35:00""'

DURATION/TIME DISPLAY:
  - $time or $duration: Include duration in results, sort by duration descending
  - $start: Include start time in results
  - $end: Include end time in results

EXAMPLES:
  - 'error': Find all errors
  - '$project MyApp': Find project named MyApp
  - 'Csc $time': Find Csc task invocations with durations
  - '$error under($project Core)': Find errors in Core project
  - '$target skipped=false $duration': Find executed targets sorted by duration
  - 'Copying file project(MyApp.csproj)': Find file copies in MyApp project
  - '$copy bin\\Debug': Find all files copied to bin\Debug
  - '$task start>""2023-11-23 14:30:00""': Find tasks starting after specific time

Returns detailed information about matching nodes including type, text, parent context, duration, and error/warning details.")]
        public async System.Threading.Tasks.Task<string> SearchNodesAsync(
            [Description("Search query using the syntax described above. Can be simple text or use advanced operators.")] string query,
            [Description("Maximum number of results to return (default 10, increase for broader searches)")] int maxResults = 10,
            [Description("Build ID to search. Omit for primary build. Use ListBuilds to see available builds.")] string buildId = null)
        {
            return await System.Threading.Tasks.Task.Run(() =>
            {
                var (build, friendlyName) = ResolveBuildWithName(buildId);
                var core = new BinlogToolExecutorCore(build);
                var result = core.SearchNodes(query, maxResults);

                // Prefix result with build context when multiple builds exist
                if (context.BuildCount > 1)
                {
                    return $"[Results from {friendlyName}]\n{result}";
                }
                return result;
            });
        }

        [Description("Gets a summary of the build including status, duration, errors and warnings count. Use buildId to target a specific build.")]
        public async System.Threading.Tasks.Task<string> GetBuildSummaryAsync(
            [Description("Build ID to get summary for. Omit for primary build. Use ListBuilds to see available builds.")] string buildId = null)
        {
            return await System.Threading.Tasks.Task.Run(() =>
            {
                var (build, friendlyName) = ResolveBuildWithName(buildId);
                var core = new BinlogToolExecutorCore(build);
                var result = core.GetBuildSummary();

                if (context.BuildCount > 1)
                {
                    return $"[Summary for {friendlyName}]\n{result}";
                }
                return result;
            });
        }

        [Description("Gets all errors and warnings from the build with their details. Use buildId to target a specific build.")]
        public async System.Threading.Tasks.Task<string> GetErrorsAndWarningsAsync(
            [Description("Type of messages to retrieve: 'errors', 'warnings', or 'all'")] string type = "all",
            [Description("Build ID to query. Omit for primary build. Use ListBuilds to see available builds.")] string buildId = null)
        {
            return await System.Threading.Tasks.Task.Run(() =>
            {
                var (build, friendlyName) = ResolveBuildWithName(buildId);
                var core = new BinlogToolExecutorCore(build);
                var result = core.GetErrorsAndWarnings(type);

                if (context.BuildCount > 1)
                {
                    return $"[Errors/Warnings from {friendlyName}]\n{result}";
                }
                return result;
            });
        }

        [Description("Gets list of all projects built with their status and duration. Use buildId to target a specific build.")]
        public async System.Threading.Tasks.Task<string> GetProjectsAsync(
            [Description("Maximum number of projects to return (default 50)")] int maxResults = 50,
            [Description("Build ID to query. Omit for primary build. Use ListBuilds to see available builds.")] string buildId = null)
        {
            return await System.Threading.Tasks.Task.Run(() =>
            {
                var (build, friendlyName) = ResolveBuildWithName(buildId);
                var core = new BinlogToolExecutorCore(build);
                var result = core.GetProjects(maxResults);

                if (context.BuildCount > 1)
                {
                    return $"[Projects from {friendlyName}]\n{result}";
                }
                return result;
            });
        }

        [Description("Gets targets executed in a specific project. Use buildId to target a specific build.")]
        public async System.Threading.Tasks.Task<string> GetProjectTargetsAsync(
            [Description("Name of the project to get targets for")] string projectName,
            [Description("Build ID to query. Omit for primary build. Use ListBuilds to see available builds.")] string buildId = null)
        {
            return await System.Threading.Tasks.Task.Run(() =>
            {
                var (build, friendlyName) = ResolveBuildWithName(buildId);
                var core = new BinlogToolExecutorCore(build);
                var result = core.GetProjectTargets(projectName);

                if (context.BuildCount > 1)
                {
                    return $"[Targets from {friendlyName}]\n{result}";
                }
                return result;
            });
        }
    }
}

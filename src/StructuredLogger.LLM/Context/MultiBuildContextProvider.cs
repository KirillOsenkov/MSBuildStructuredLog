using System;
using System.Linq;
using System.Text;
using Microsoft.Build.Logging.StructuredLogger;

namespace StructuredLogger.LLM
{
    /// <summary>
    /// Provides context information from multiple binlogs to the LLM chat.
    /// </summary>
    public class MultiBuildContextProvider
    {
        private readonly MultiBuildContext context;

        public MultiBuildContextProvider(MultiBuildContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// Gets overview of ALL loaded builds for system prompt.
        /// </summary>
        public string GetAllBuildsOverview()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Loaded Builds ===");
            sb.AppendLine($"Total builds: {context.BuildCount}");
            sb.AppendLine($"Primary build: {context.PrimaryBuildId}");
            sb.AppendLine();

            foreach (var buildInfo in context.GetAllBuilds())
            {
                var marker = buildInfo.IsPrimary ? " [PRIMARY]" : "";
                var status = buildInfo.Succeeded ? "Succeeded" : "FAILED";

                sb.AppendLine($"[{buildInfo.BuildId}] {buildInfo.FriendlyName}{marker}");
                sb.AppendLine($"  Path: {buildInfo.FullPath}");
                sb.AppendLine($"  Status: {status}");
                sb.AppendLine($"  Duration: {buildInfo.DurationText}");
                sb.AppendLine($"  Errors: {buildInfo.ErrorCount}, Warnings: {buildInfo.WarningCount}");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>
        /// Gets detailed overview of a specific build.
        /// </summary>
        public string GetBuildOverview(string buildId)
        {
            if (!context.TryGetBuild(buildId, out var buildInfo))
            {
                return $"Build '{buildId}' not found.";
            }

            return GetBuildOverview(buildInfo);
        }

        /// <summary>
        /// Gets detailed overview of a specific build.
        /// </summary>
        public string GetBuildOverview(BuildInfo buildInfo)
        {
            if (buildInfo == null)
            {
                return "No build provided.";
            }

            var build = buildInfo.Build;
            var sb = new StringBuilder();

            sb.AppendLine("=== Build Overview ===");
            sb.AppendLine($"Build ID: {buildInfo.BuildId}");
            sb.AppendLine($"Name: {buildInfo.FriendlyName}");
            sb.AppendLine($"Status: {(build.Succeeded ? "Succeeded" : "Failed")}");
            sb.AppendLine($"Duration: {build.DurationText}");

            if (!string.IsNullOrEmpty(buildInfo.FullPath))
            {
                sb.AppendLine($"Log File: {buildInfo.FullPath}");
            }

            sb.AppendLine($"Errors: {buildInfo.ErrorCount}");
            sb.AppendLine($"Warnings: {buildInfo.WarningCount}");

            return sb.ToString();
        }

        /// <summary>
        /// Gets context for selected node in specific build.
        /// </summary>
        public string GetSelectedNodeContext(BaseNode selectedNode, string buildId)
        {
            if (selectedNode == null)
            {
                return "No node selected.";
            }

            if (!context.TryGetBuild(buildId, out var buildInfo))
            {
                return $"Build '{buildId}' not found.";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"=== Selected Node (from {buildInfo.FriendlyName}) ===");
            sb.AppendLine($"Type: {selectedNode.GetType().Name}");
            sb.AppendLine($"Text: {selectedNode.ToString()}");

            if (selectedNode is TimedNode timedNode)
            {
                sb.AppendLine($"Duration: {timedNode.DurationText}");
                sb.AppendLine($"Start: {timedNode.StartTime}");
                sb.AppendLine($"End: {timedNode.EndTime}");
            }

            if (selectedNode is NamedNode namedNode)
            {
                sb.AppendLine($"Name: {namedNode.Name}");
            }

            if (selectedNode is Project project)
            {
                sb.AppendLine($"Project File: {project.ProjectFile}");
                if (project.HasChildren)
                {
                    sb.AppendLine($"Child Count: {project.Children.Count}");
                }
            }

            if (selectedNode is Target target)
            {
                sb.AppendLine($"Target Name: {target.Name}");
                sb.AppendLine($"Project: {target.GetNearestParent<Project>()?.Name ?? "Unknown"}");
            }

            if (selectedNode is Error error)
            {
                sb.AppendLine($"Error Code: {error.Code}");
                sb.AppendLine($"File: {error.File}");
                sb.AppendLine($"Line: {error.LineNumber}");
            }

            if (selectedNode is Warning warning)
            {
                sb.AppendLine($"Warning Code: {warning.Code}");
                sb.AppendLine($"File: {warning.File}");
                sb.AppendLine($"Line: {warning.LineNumber}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Gets a brief summary of all builds (for compact display).
        /// </summary>
        public string GetBuildsSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Builds loaded: {context.BuildCount}");

            foreach (var buildInfo in context.GetAllBuilds())
            {
                var status = buildInfo.Succeeded ? "✓" : "✗";
                var primary = buildInfo.IsPrimary ? "*" : " ";
                sb.AppendLine($"  {primary}[{buildInfo.BuildId}] {buildInfo.FriendlyName}: {status} {buildInfo.DurationText}");
            }

            return sb.ToString();
        }
    }
}

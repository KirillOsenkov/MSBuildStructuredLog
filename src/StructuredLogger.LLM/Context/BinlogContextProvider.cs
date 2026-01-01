using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Build.Logging.StructuredLogger;

namespace StructuredLogger.LLM
{
    /// <summary>
    /// Provides context information from the binlog to the LLM chat.
    /// </summary>
    public class BinlogContextProvider
    {
        private readonly Build build;

        public BinlogContextProvider(Build build)
        {
            this.build = build ?? throw new ArgumentNullException(nameof(build));
        }

        public string GetBuildOverview()
        {
            if (build == null)
            {
                return "No build loaded.";
            }

            var sb = new StringBuilder();
            sb.AppendLine("=== Build Overview ===");
            sb.AppendLine($"Status: {(build.Succeeded ? "Succeeded" : "Failed")}");
            sb.AppendLine($"Duration: {build.DurationText}");
            
            if (!string.IsNullOrEmpty(build.LogFilePath))
            {
                sb.AppendLine($"Log File: {build.LogFilePath}");
            }

            // Count errors and warnings
            int errorCount = 0;
            int warningCount = 0;
            build.VisitAllChildren<BaseNode>(node =>
            {
                if (node is Error) errorCount++;
                else if (node is Warning) warningCount++;
            });

            sb.AppendLine($"Errors: {errorCount}");
            sb.AppendLine($"Warnings: {warningCount}");

            return sb.ToString();
        }

        public string GetSelectedNodeContext(BaseNode selectedNode)
        {
            if (selectedNode == null)
            {
                return "No node selected.";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"=== Selected Node ===");
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

            // Include parent hierarchy
            var parents = new List<string>();
            var parent = selectedNode.Parent;
            while (parent != null && parents.Count < 5)
            {
                parents.Add($"{parent.GetType().Name}: {parent.ToString()}");
                parent = parent.Parent;
            }

            if (parents.Any())
            {
                sb.AppendLine("\n=== Parent Hierarchy ===");
                foreach (var p in parents)
                {
                    sb.AppendLine(p);
                }
            }

            return sb.ToString();
        }

        public string GetFullContext(BaseNode? selectedNode = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine(GetBuildOverview());
            
            if (selectedNode != null)
            {
                sb.AppendLine();
                sb.AppendLine(GetSelectedNodeContext(selectedNode));
            }

            return sb.ToString();
        }
    }
}

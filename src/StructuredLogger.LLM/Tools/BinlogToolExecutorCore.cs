using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Build.Logging.StructuredLogger;

namespace StructuredLogger.LLM
{
    /// <summary>
    /// Core implementation of binlog tool execution logic.
    /// Separated from the IToolExecutor implementation for reuse.
    /// </summary>
    internal class BinlogToolExecutorCore
    {
        private readonly Build build;
        private const int MaxOutputTokensPerTool = 3000; // Roughly 12,000 characters

        public BinlogToolExecutorCore(Build build)
        {
            this.build = build ?? throw new ArgumentNullException(nameof(build));
        }

        private string TruncateIfNeeded(string result)
        {
            const int maxChars = MaxOutputTokensPerTool * 4; // Conservative estimate
            if (result.Length > maxChars)
            {
                return result.Substring(0, maxChars) + "\n\n[Output truncated due to length. Use more specific queries or filters.]";
            }
            return result;
        }

        public string GetBuildSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Build Status: {(build.Succeeded ? "Succeeded" : "Failed")}");
            sb.AppendLine($"Duration: {build.DurationText}");
            sb.AppendLine($"Start Time: {build.StartTime}");
            sb.AppendLine($"End Time: {build.EndTime}");

            if (!string.IsNullOrEmpty(build.LogFilePath))
            {
                sb.AppendLine($"Log File: {build.LogFilePath}");
            }

            if (!string.IsNullOrEmpty(build.MSBuildVersion))
            {
                sb.AppendLine($"MSBuild Version: {build.MSBuildVersion}");
            }

            // Count errors and warnings
            int errorCount = 0;
            int warningCount = 0;
            int projectCount = 0;
            
            build.VisitAllChildren<BaseNode>(node =>
            {
                if (node is Error) errorCount++;
                else if (node is Warning) warningCount++;
                else if (node is Project) projectCount++;
            });

            sb.AppendLine($"Projects Built: {projectCount}");
            sb.AppendLine($"Total Errors: {errorCount}");
            sb.AppendLine($"Total Warnings: {warningCount}");

            return sb.ToString();
        }

        public string SearchNodes(string query, int maxResults = 10)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return "Error: Search query cannot be empty.";
            }

            var results = new List<string>();
            int count = 0;

            build.VisitAllChildren<BaseNode>(node =>
            {
                if (count >= maxResults) return;

                var text = node.ToString();
                if (text != null && text.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    results.Add($"{node.GetType().Name}: {text}");
                    count++;
                }
            });

            if (results.Count == 0)
            {
                return $"No nodes found matching '{query}'.";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Found {results.Count} matching nodes:");
            foreach (var result in results)
            {
                sb.AppendLine($"  - {result}");
            }

            return TruncateIfNeeded(sb.ToString());
        }

        public string GetErrorsAndWarnings(string type = "all")
        {
            var sb = new StringBuilder();
            var errors = new List<Error>();
            var warnings = new List<Warning>();

            build.VisitAllChildren<BaseNode>(node =>
            {
                if (node is Error error) errors.Add(error);
                else if (node is Warning warning) warnings.Add(warning);
            });

            bool showErrors = type.Equals("all", StringComparison.OrdinalIgnoreCase) || 
                            type.Equals("errors", StringComparison.OrdinalIgnoreCase);
            bool showWarnings = type.Equals("all", StringComparison.OrdinalIgnoreCase) || 
                              type.Equals("warnings", StringComparison.OrdinalIgnoreCase);

            if (showErrors && errors.Any())
            {
                sb.AppendLine($"=== Errors ({errors.Count}) ===");
                foreach (var error in errors.Take(20))
                {
                    sb.AppendLine($"[{error.Code}] {error.ToString()}");
                    if (!string.IsNullOrEmpty(error.File))
                    {
                        sb.AppendLine($"  File: {error.File}:{error.LineNumber}");
                    }
                    var project = error.GetNearestParent<Project>();
                    if (project != null)
                    {
                        sb.AppendLine($"  Project: {project.Name}");
                    }
                    sb.AppendLine();
                }
                if (errors.Count > 20)
                {
                    sb.AppendLine($"... and {errors.Count - 20} more errors");
                }
            }
            else if (showErrors)
            {
                sb.AppendLine("No errors found.");
            }

            if (showWarnings && warnings.Any())
            {
                sb.AppendLine($"\n=== Warnings ({warnings.Count}) ===");
                foreach (var warning in warnings.Take(20))
                {
                    sb.AppendLine($"[{warning.Code}] {warning.ToString()}");
                    if (!string.IsNullOrEmpty(warning.File))
                    {
                        sb.AppendLine($"  File: {warning.File}:{warning.LineNumber}");
                    }
                    var project = warning.GetNearestParent<Project>();
                    if (project != null)
                    {
                        sb.AppendLine($"  Project: {project.Name}");
                    }
                    sb.AppendLine();
                }
                if (warnings.Count > 20)
                {
                    sb.AppendLine($"... and {warnings.Count - 20} more warnings");
                }
            }
            else if (showWarnings)
            {
                sb.AppendLine("No warnings found.");
            }

            return TruncateIfNeeded(sb.ToString());
        }

        public string GetProjects(int maxResults = 50)
        {
            var sb = new StringBuilder();
            var projects = new List<Project>();

            build.VisitAllChildren<Project>(p => projects.Add(p));

            if (!projects.Any())
            {
                return "No projects found in the build.";
            }

            sb.AppendLine($"=== Projects ({projects.Count} total, showing first {Math.Min(maxResults, projects.Count)}) ===");
            foreach (var project in projects.Take(maxResults))
            {
                sb.AppendLine($"{project.Name}");
                sb.AppendLine($"  Duration: {project.DurationText}");
                if (!string.IsNullOrEmpty(project.ProjectFile))
                {
                    sb.AppendLine($"  File: {project.ProjectFile}");
                }

                // Count errors/warnings in this project
                int projErrors = 0;
                int projWarnings = 0;
                project.VisitAllChildren<BaseNode>(node =>
                {
                    if (node is Error) projErrors++;
                    else if (node is Warning) projWarnings++;
                });

                if (projErrors > 0 || projWarnings > 0)
                {
                    sb.AppendLine($"  Errors: {projErrors}, Warnings: {projWarnings}");
                }
                sb.AppendLine();
            }

            if (projects.Count > maxResults)
            {
                sb.AppendLine($"... and {projects.Count - maxResults} more projects");
            }

            return TruncateIfNeeded(sb.ToString());
        }

        public string GetProjectTargets(string projectName)
        {
            if (string.IsNullOrWhiteSpace(projectName))
            {
                return "Error: Project name cannot be empty.";
            }

            Project project = null;
            build.VisitAllChildren<Project>(p =>
            {
                if (p.Name != null && p.Name.IndexOf(projectName, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    project = p;
                }
            });

            if (project == null)
            {
                return $"Project '{projectName}' not found.";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"=== Targets in {project.Name} ===");

            var targets = new List<Target>();
            project.VisitAllChildren<Target>(t => targets.Add(t));

            if (!targets.Any())
            {
                return $"No targets found in project {project.Name}";
            }

            foreach (var target in targets.OrderByDescending(t => t.Duration))
            {
                sb.AppendLine($"{target.Name}");
                sb.AppendLine($"  Duration: {target.DurationText}");
                
                if (target is TreeNode treeNode && treeNode.HasChildren)
                {
                    var tasks = new List<Microsoft.Build.Logging.StructuredLogger.Task>();
                    target.VisitAllChildren<Microsoft.Build.Logging.StructuredLogger.Task>(t => tasks.Add(t));
                    if (tasks.Any())
                    {
                        sb.AppendLine($"  Tasks: {tasks.Count}");
                    }
                }
                sb.AppendLine();
            }

            return TruncateIfNeeded(sb.ToString());
        }
    }
}

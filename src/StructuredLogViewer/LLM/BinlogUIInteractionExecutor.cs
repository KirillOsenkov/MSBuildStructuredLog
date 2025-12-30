using System;
using System.ComponentModel;
using System.Linq;
using System.Text;
using Microsoft.Build.Logging.StructuredLogger;
using StructuredLogViewer.Controls;

namespace StructuredLogViewer.LLM
{
    /// <summary>
    /// Executes UI interaction tools that allow the LLM to select, highlight, and navigate to nodes in the viewer.
    /// </summary>
    public class BinlogUIInteractionExecutor
    {
        private readonly Build build;
        private readonly BuildControl buildControl;

        public BinlogUIInteractionExecutor(Build build, BuildControl buildControl)
        {
            this.build = build ?? throw new ArgumentNullException(nameof(build));
            this.buildControl = buildControl ?? throw new ArgumentNullException(nameof(buildControl));
        }

        [Description("Selects and navigates to a specific node in the tree view by searching for it. This highlights the node and shows it in the tree.")]
        public string SelectNodeByText(
            [Description("Text to search for in node names or content")] string searchText,
            [Description("Optional: Type of node to search for (e.g., 'Project', 'Target', 'Task', 'Error', 'Warning')")] string nodeType = null)
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                return "Error: Search text cannot be empty.";
            }

            BaseNode foundNode = null;

            build.VisitAllChildren<BaseNode>(node =>
            {
                if (foundNode != null) return; // Already found one

                // Check node type if specified
                if (!string.IsNullOrEmpty(nodeType))
                {
                    var actualType = node.GetType().Name;
                    if (!actualType.Equals(nodeType, StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }
                }

                // Check if text matches
                var nodeText = node.ToString();
                if (nodeText != null && nodeText.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    foundNode = node;
                }
            });

            if (foundNode == null)
            {
                return $"No node found matching '{searchText}'" + 
                       (string.IsNullOrEmpty(nodeType) ? "" : $" of type '{nodeType}'");
            }

            // Navigate to the node on the UI thread
            try
            {
                buildControl.Dispatcher.Invoke(() =>
                {
                    buildControl.SelectItem(foundNode);
                });

                return $"Selected {foundNode.GetType().Name}: {foundNode.ToString()}";
            }
            catch (Exception ex)
            {
                return $"Error selecting node: {ex.Message}";
            }
        }

        [Description("Selects and displays a specific error in the tree view by its index or error code.")]
        public string SelectError(
            [Description("Index of the error (1-based) or error code (e.g., 'CS1234')")] string errorIdentifier)
        {
            if (string.IsNullOrWhiteSpace(errorIdentifier))
            {
                return "Error: Error identifier cannot be empty.";
            }

            var errors = new System.Collections.Generic.List<Error>();
            build.VisitAllChildren<Error>(e => errors.Add(e));

            if (errors.Count == 0)
            {
                return "No errors found in the build.";
            }

            Error targetError = null;

            // Try parsing as index (1-based)
            if (int.TryParse(errorIdentifier, out int index))
            {
                if (index < 1 || index > errors.Count)
                {
                    return $"Error index {index} is out of range. Build has {errors.Count} error(s).";
                }
                targetError = errors[index - 1];
            }
            else
            {
                // Search by error code
                targetError = errors.FirstOrDefault(e =>
                    e.Code != null && e.Code.Equals(errorIdentifier, StringComparison.OrdinalIgnoreCase));

                if (targetError == null)
                {
                    return $"No error found with code '{errorIdentifier}'.";
                }
            }

            try
            {
                buildControl.Dispatcher.Invoke(() =>
                {
                    buildControl.SelectItem(targetError);
                });

                return $"Selected error: [{targetError.Code}] {targetError.ToString()}" +
                       (string.IsNullOrEmpty(targetError.File) ? "" : $"\nFile: {targetError.File}:{targetError.LineNumber}");
            }
            catch (Exception ex)
            {
                return $"Error selecting error node: {ex.Message}";
            }
        }

        [Description("Selects and displays a specific warning in the tree view by its index or warning code.")]
        public string SelectWarning(
            [Description("Index of the warning (1-based) or warning code (e.g., 'CS0168')")] string warningIdentifier)
        {
            if (string.IsNullOrWhiteSpace(warningIdentifier))
            {
                return "Error: Warning identifier cannot be empty.";
            }

            var warnings = new System.Collections.Generic.List<Warning>();
            build.VisitAllChildren<Warning>(w => warnings.Add(w));

            if (warnings.Count == 0)
            {
                return "No warnings found in the build.";
            }

            Warning targetWarning = null;

            // Try parsing as index (1-based)
            if (int.TryParse(warningIdentifier, out int index))
            {
                if (index < 1 || index > warnings.Count)
                {
                    return $"Warning index {index} is out of range. Build has {warnings.Count} warning(s).";
                }
                targetWarning = warnings[index - 1];
            }
            else
            {
                // Search by warning code
                targetWarning = warnings.FirstOrDefault(w =>
                    w.Code != null && w.Code.Equals(warningIdentifier, StringComparison.OrdinalIgnoreCase));

                if (targetWarning == null)
                {
                    return $"No warning found with code '{warningIdentifier}'.";
                }
            }

            try
            {
                buildControl.Dispatcher.Invoke(() =>
                {
                    buildControl.SelectItem(targetWarning);
                });

                return $"Selected warning: [{targetWarning.Code}] {targetWarning.ToString()}" +
                       (string.IsNullOrEmpty(targetWarning.File) ? "" : $"\nFile: {targetWarning.File}:{targetWarning.LineNumber}");
            }
            catch (Exception ex)
            {
                return $"Error selecting warning node: {ex.Message}";
            }
        }

        [Description("Selects and displays a specific project in the tree view by name or partial name match.")]
        public string SelectProject(
            [Description("Name or partial name of the project")] string projectName)
        {
            if (string.IsNullOrWhiteSpace(projectName))
            {
                return "Error: Project name cannot be empty.";
            }

            Project foundProject = null;

            build.VisitAllChildren<Project>(p =>
            {
                if (foundProject != null) return;

                if (p.Name != null && p.Name.IndexOf(projectName, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    foundProject = p;
                }
            });

            if (foundProject == null)
            {
                return $"No project found matching '{projectName}'.";
            }

            try
            {
                buildControl.Dispatcher.Invoke(() =>
                {
                    buildControl.SelectItem(foundProject);
                });

                return $"Selected project: {foundProject.Name}";
            }
            catch (Exception ex)
            {
                return $"Error selecting project: {ex.Message}";
            }
        }

        [Description("Opens a source file in the document viewer. Useful for viewing files referenced in errors, warnings, or tasks.")]
        public string OpenFile(
            [Description("Full path or partial path/filename to open")] string filePath,
            [Description("Optional: Line number to navigate to (1-based)")] int lineNumber = 0)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return "Error: File path cannot be empty.";
            }

            try
            {
                bool success = false;
                buildControl.Dispatcher.Invoke(() =>
                {
                    success = buildControl.DisplayFile(filePath, lineNumber);
                });

                if (success)
                {
                    return $"Opened file: {filePath}" + (lineNumber > 0 ? $" at line {lineNumber}" : "");
                }
                else
                {
                    return $"Could not open file: {filePath}. File may not be embedded in the binlog or path may be incorrect.";
                }
            }
            catch (Exception ex)
            {
                return $"Error opening file: {ex.Message}";
            }
        }

        [Description("Opens the Timeline view and navigates to a specific timed node (project, target, or task).")]
        public string OpenTimeline(
            [Description("Optional: Name of project, target, or task to highlight in timeline")] string nodeName = null)
        {
            try
            {
                buildControl.Dispatcher.Invoke(() =>
                {
                    if (!string.IsNullOrWhiteSpace(nodeName))
                    {
                        // Find the node first
                        TimedNode foundNode = null;
                        build.VisitAllChildren<TimedNode>(node =>
                        {
                            if (foundNode != null) return;

                            var nodeText = node.ToString();
                            if (nodeText != null && nodeText.IndexOf(nodeName, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                foundNode = node;
                            }
                        });

                        if (foundNode != null)
                        {
                            buildControl.SelectItem(foundNode);
                        }
                    }

                    buildControl.GoToTimeLine();
                });

                return string.IsNullOrWhiteSpace(nodeName) 
                    ? "Opened Timeline view" 
                    : $"Opened Timeline view for: {nodeName}";
            }
            catch (Exception ex)
            {
                return $"Error opening timeline: {ex.Message}";
            }
        }

        [Description("Opens the Tracing view and navigates to a specific timed node for detailed performance analysis.")]
        public string OpenTracing(
            [Description("Optional: Name of project, target, or task to analyze in tracing view")] string nodeName = null)
        {
            try
            {
                buildControl.Dispatcher.Invoke(() =>
                {
                    if (!string.IsNullOrWhiteSpace(nodeName))
                    {
                        // Find the node first
                        TimedNode foundNode = null;
                        build.VisitAllChildren<TimedNode>(node =>
                        {
                            if (foundNode != null) return;

                            var nodeText = node.ToString();
                            if (nodeText != null && nodeText.IndexOf(nodeName, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                foundNode = node;
                            }
                        });

                        if (foundNode != null)
                        {
                            buildControl.SelectItem(foundNode);
                        }
                    }

                    buildControl.GoToTracing();
                });

                return string.IsNullOrWhiteSpace(nodeName)
                    ? "Opened Tracing view"
                    : $"Opened Tracing view for: {nodeName}";
            }
            catch (Exception ex)
            {
                return $"Error opening tracing: {ex.Message}";
            }
        }

        [Description("Performs a search in the build log and optionally selects the first result.")]
        public string PerformSearch(
            [Description("Search query text")] string searchText,
            [Description("Whether to automatically select the first search result")] bool selectFirst = true)
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                return "Error: Search text cannot be empty.";
            }

            try
            {
                buildControl.Dispatcher.Invoke(() =>
                {
                    buildControl.SelectSearchTab(searchText);
                });

                return $"Performed search for: '{searchText}'" + 
                       (selectFirst ? " and selected first result" : "");
            }
            catch (Exception ex)
            {
                return $"Error performing search: {ex.Message}";
            }
        }

        [Description("Opens the Properties and Items tab to view MSBuild properties and items for the selected or specified project.")]
        public string OpenPropertiesAndItems(
            [Description("Optional: Project name to set context for")] string projectName = null)
        {
            try
            {
                buildControl.Dispatcher.Invoke(() =>
                {
                    if (!string.IsNullOrWhiteSpace(projectName))
                    {
                        // Find and select the project first
                        Project foundProject = null;
                        build.VisitAllChildren<Project>(p =>
                        {
                            if (foundProject != null) return;
                            if (p.Name != null && p.Name.IndexOf(projectName, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                foundProject = p;
                            }
                        });

                        if (foundProject != null)
                        {
                            buildControl.SelectItem(foundProject);
                        }
                    }

                    buildControl.SelectPropertiesAndItemsTab();
                });

                return string.IsNullOrWhiteSpace(projectName)
                    ? "Opened Properties and Items view"
                    : $"Opened Properties and Items view for project: {projectName}";
            }
            catch (Exception ex)
            {
                return $"Error opening Properties and Items: {ex.Message}";
            }
        }

        [Description("Opens the Find in Files tab for full-text search across embedded files.")]
        public string OpenFindInFiles(
            [Description("Optional: Initial search text to populate")] string searchText = null)
        {
            try
            {
                buildControl.Dispatcher.Invoke(() =>
                {
                    buildControl.SelectFindInFilesTab(searchText);
                });

                return string.IsNullOrWhiteSpace(searchText)
                    ? "Opened Find in Files view"
                    : $"Opened Find in Files view with search: '{searchText}'";
            }
            catch (Exception ex)
            {
                return $"Error opening Find in Files: {ex.Message}";
            }
        }

        [Description("Focuses the main search box at the top of the window, ready for user input.")]
        public string FocusSearch()
        {
            try
            {
                buildControl.Dispatcher.Invoke(() =>
                {
                    buildControl.FocusSearch();
                });

                return "Focused the search box";
            }
            catch (Exception ex)
            {
                return $"Error focusing search: {ex.Message}";
            }
        }
    }
}

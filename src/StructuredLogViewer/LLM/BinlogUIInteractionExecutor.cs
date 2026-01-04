using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Logging.StructuredLogger;
using Microsoft.Extensions.AI;
using StructuredLogger.LLM;
using StructuredLogViewer.Controls;

namespace StructuredLogViewer.LLM
{
    /// <summary>
    /// UI interaction tool executor that enables LLM to manipulate the WPF viewer.
    /// Provides tools for navigation, selection, and view switching.
    /// This is UI-specific and should only be used in StructuredLogViewer, not in CLI.
    /// </summary>
    public class BinlogUIInteractionExecutor : IToolsContainer
    {
        private readonly Build build;
        private readonly BuildControl buildControl;

        public BinlogUIInteractionExecutor(Build build, BuildControl buildControl)
        {
            this.build = build ?? throw new ArgumentNullException(nameof(build));
            this.buildControl = buildControl ?? throw new ArgumentNullException(nameof(buildControl));
        }

        public IEnumerable<(AIFunction Function, StructuredLogger.LLM.AgentPhase ApplicablePhases)> GetTools()
        {
            // Return all UI interaction tools - these are only applicable during summarization phase
            var phase = StructuredLogger.LLM.AgentPhase.Summarization;
            
            yield return (AIFunctionFactory.Create(SelectNodeByTextAsync), phase);
            yield return (AIFunctionFactory.Create(SelectErrorAsync), phase);
            yield return (AIFunctionFactory.Create(SelectWarningAsync), phase);
            yield return (AIFunctionFactory.Create(SelectProjectAsync), phase);
            yield return (AIFunctionFactory.Create(OpenFileAsync), phase);
            yield return (AIFunctionFactory.Create(OpenTimelineAsync), phase);
            yield return (AIFunctionFactory.Create(OpenTracingAsync), phase);
            yield return (AIFunctionFactory.Create(PerformSearchAsync), phase);
            yield return (AIFunctionFactory.Create(OpenPropertiesAndItemsAsync), phase);
            yield return (AIFunctionFactory.Create(OpenFindInFilesAsync), phase);
            yield return (AIFunctionFactory.Create(FocusSearchAsync), phase);
        }

        [Description("Selects and navigates to a specific node in the tree view by searching for it. This highlights the node and shows it in the tree.")]
        public async System.Threading.Tasks.Task<string> SelectNodeByTextAsync(
            [Description("Text to search for in node names or content")] string searchText,
            [Description("Optional: Type of node to search for (e.g., 'Project', 'Target', 'Task', 'Error', 'Warning')")] string nodeType = null)
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                return "Error: Search text cannot be empty.";
            }

            // Perform expensive search on background thread
            var foundNode = await System.Threading.Tasks.Task.Run(() =>
            {
                BaseNode result = null;
                build.VisitAllChildren<BaseNode>(node =>
                {
                    if (result != null) return; // Already found one

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
                        result = node;
                    }
                });
                return result;
            }).ConfigureAwait(false);

            if (foundNode == null)
            {
                return $"No node found matching '{searchText}'" +
                       (string.IsNullOrEmpty(nodeType) ? "" : $" of type '{nodeType}'");
            }

            // Navigate to the node on the UI thread
            try
            {
                await buildControl.Dispatcher.InvokeAsync(() =>
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
        public async System.Threading.Tasks.Task<string> SelectErrorAsync(
            [Description("Index of the error (1-based) or error code (e.g., 'CS1234')")] string errorIdentifier)
        {
            if (string.IsNullOrWhiteSpace(errorIdentifier))
            {
                return "Error: Error identifier cannot be empty.";
            }

            // Collect errors on background thread
            var result = await System.Threading.Tasks.Task.Run(() =>
            {
                var errorList = new List<Error>();
                build.VisitAllChildren<Error>(e => errorList.Add(e));

                if (errorList.Count == 0)
                {
                    return (errorList, (Error)null, "No errors found in the build.");
                }

                Error target = null;

                // Try parsing as index (1-based)
                if (int.TryParse(errorIdentifier, out int index))
                {
                    if (index < 1 || index > errorList.Count)
                    {
                        return (errorList, (Error)null, $"Error index {index} is out of range. Build has {errorList.Count} error(s).");
                    }
                    target = errorList[index - 1];
                }
                else
                {
                    // Search by error code
                    target = errorList.FirstOrDefault(e =>
                        e.Code != null && e.Code.Equals(errorIdentifier, StringComparison.OrdinalIgnoreCase));

                    if (target == null)
                    {
                        return (errorList, (Error)null, $"No error found with code '{errorIdentifier}'.");
                    }
                }

                return (errorList, target, (string)null);
            }).ConfigureAwait(false);

            var errors = result.Item1;
            var targetError = result.Item2;
            var errorMessage = result.Item3;

            if (errorMessage != null)
            {
                return errorMessage;
            }

            try
            {
                await buildControl.Dispatcher.InvokeAsync(() =>
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
        public async System.Threading.Tasks.Task<string> SelectWarningAsync(
            [Description("Index of the warning (1-based) or warning code (e.g., 'CS0168')")] string warningIdentifier)
        {
            if (string.IsNullOrWhiteSpace(warningIdentifier))
            {
                return "Error: Warning identifier cannot be empty.";
            }

            // Collect warnings on background thread
            var result = await System.Threading.Tasks.Task.Run(() =>
            {
                var warningList = new List<Warning>();
                build.VisitAllChildren<Warning>(w => warningList.Add(w));

                if (warningList.Count == 0)
                {
                    return (warningList, (Warning)null, "No warnings found in the build.");
                }

                Warning target = null;

                // Try parsing as index (1-based)
                if (int.TryParse(warningIdentifier, out int index))
                {
                    if (index < 1 || index > warningList.Count)
                    {
                        return (warningList, (Warning)null, $"Warning index {index} is out of range. Build has {warningList.Count} warning(s).");
                    }
                    target = warningList[index - 1];
                }
                else
                {
                    // Search by warning code
                    target = warningList.FirstOrDefault(w =>
                        w.Code != null && w.Code.Equals(warningIdentifier, StringComparison.OrdinalIgnoreCase));

                    if (target == null)
                    {
                        return (warningList, (Warning)null, $"No warning found with code '{warningIdentifier}'.");
                    }
                }

                return (warningList, target, (string)null);
            }).ConfigureAwait(false);

            var warnings = result.Item1;
            var targetWarning = result.Item2;
            var errorMessage = result.Item3;

            if (errorMessage != null)
            {
                return errorMessage;
            }

            try
            {
                await buildControl.Dispatcher.InvokeAsync(() =>
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
        public async System.Threading.Tasks.Task<string> SelectProjectAsync(
            [Description("Name or partial name of the project")] string projectName)
        {
            if (string.IsNullOrWhiteSpace(projectName))
            {
                return "Error: Project name cannot be empty.";
            }

            // Search for project on background thread
            var foundProject = await System.Threading.Tasks.Task.Run(() =>
            {
                Project result = null;
                build.VisitAllChildren<Project>(p =>
                {
                    if (result != null) return;
                    if (p.Name != null && p.Name.IndexOf(projectName, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        result = p;
                    }
                });
                return result;
            }).ConfigureAwait(false);

            if (foundProject == null)
            {
                return $"No project found matching '{projectName}'.";
            }

            try
            {
                await buildControl.Dispatcher.InvokeAsync(() =>
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
        public async System.Threading.Tasks.Task<string> OpenFileAsync(
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
                await buildControl.Dispatcher.InvokeAsync(() =>
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
        public async System.Threading.Tasks.Task<string> OpenTimelineAsync(
            [Description("Optional: Name of project, target, or task to highlight in timeline")] string nodeName = null)
        {
            try
            {
                // Search for node on background thread if needed
                TimedNode foundNode = null;
                if (!string.IsNullOrWhiteSpace(nodeName))
                {
                    foundNode = await System.Threading.Tasks.Task.Run(() =>
                    {
                        TimedNode result = null;
                        build.VisitAllChildren<TimedNode>(node =>
                        {
                            if (result != null) return;

                            var nodeText = node.ToString();
                            if (nodeText != null && nodeText.IndexOf(nodeName, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                result = node;
                            }
                        });
                        return result;
                    }).ConfigureAwait(false);
                }

                await buildControl.Dispatcher.InvokeAsync(() =>
                {
                    if (foundNode != null)
                    {
                        buildControl.SelectItem(foundNode);
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
        public async System.Threading.Tasks.Task<string> OpenTracingAsync(
            [Description("Optional: Name of project, target, or task to analyze in tracing view")] string nodeName = null)
        {
            try
            {
                // Search for node on background thread if needed
                TimedNode foundNode = null;
                if (!string.IsNullOrWhiteSpace(nodeName))
                {
                    foundNode = await System.Threading.Tasks.Task.Run(() =>
                    {
                        TimedNode result = null;
                        build.VisitAllChildren<TimedNode>(node =>
                        {
                            if (result != null) return;

                            var nodeText = node.ToString();
                            if (nodeText != null && nodeText.IndexOf(nodeName, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                result = node;
                            }
                        });
                        return result;
                    }).ConfigureAwait(false);
                }

                await buildControl.Dispatcher.InvokeAsync(() =>
                {
                    if (foundNode != null)
                    {
                        buildControl.SelectItem(foundNode);
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
        public async System.Threading.Tasks.Task<string> PerformSearchAsync(
            [Description("Search query text")] string searchText,
            [Description("Whether to automatically select the first search result")] bool selectFirst = true)
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                return "Error: Search text cannot be empty.";
            }

            try
            {
                await buildControl.Dispatcher.InvokeAsync(() =>
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
        public async System.Threading.Tasks.Task<string> OpenPropertiesAndItemsAsync(
            [Description("Optional: Project name to set context for")] string projectName = null)
        {
            try
            {
                // Search for project on background thread if needed
                Project foundProject = null;
                if (!string.IsNullOrWhiteSpace(projectName))
                {
                    foundProject = await System.Threading.Tasks.Task.Run(() =>
                    {
                        Project result = null;
                        build.VisitAllChildren<Project>(p =>
                        {
                            if (result != null) return;
                            if (p.Name != null && p.Name.IndexOf(projectName, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                result = p;
                            }
                        });
                        return result;
                    }).ConfigureAwait(false);
                }

                await buildControl.Dispatcher.InvokeAsync(() =>
                {
                    if (foundProject != null)
                    {
                        buildControl.SelectItem(foundProject);
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
        public async System.Threading.Tasks.Task<string> OpenFindInFilesAsync(
            [Description("Optional: Initial search text to populate")] string searchText = null)
        {
            try
            {
                await buildControl.Dispatcher.InvokeAsync(() =>
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
        public async System.Threading.Tasks.Task<string> FocusSearchAsync()
        {
            try
            {
                await buildControl.Dispatcher.InvokeAsync(() =>
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

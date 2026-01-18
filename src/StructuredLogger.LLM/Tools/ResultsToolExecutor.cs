using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using Microsoft.Extensions.AI;

namespace StructuredLogger.LLM
{
    /// <summary>
    /// Tool executor for managing and searching cataloged results.
    /// Provides access to previously generated tool results that may have been truncated.
    /// </summary>
    public class ResultsToolExecutor : IToolsContainer
    {
        private readonly ResultManager resultManager;

        public ResultsToolExecutor()
        {
            this.resultManager = ResultManager.Instance;
        }

        public bool HasGuiTools => false;

        public IEnumerable<(AIFunction Function, AgentPhase ApplicablePhases)> GetTools()
        {
            // ListResults is useful in all phases to see what data is available
            yield return (AIFunctionFactory.Create(ListResultsAsync), AgentPhase.All);
            
            // SearchResult is primarily for research when diving into specific data
            yield return (AIFunctionFactory.Create(SearchResultAsync), AgentPhase.Research | AgentPhase.Summarization);
        }

        [Description(@"Lists all cataloged results from previous tool invocations.

Shows ResultId, truncation status, tool invocation, timestamp, and size for each cataloged result.
Use to discover what data has been retrieved and find ResultIds for SearchResult.

Results cataloged: SearchNodes, GetErrorsAndWarnings, GetProjects, GetProjectTargets, ListEvents, ListEmbeddedFiles, GetEmbeddedFile, SearchEmbeddedFiles.
Not cataloged: ListResults, SearchResult, GetBuildSummary.")]
        public async System.Threading.Tasks.Task<string> ListResultsAsync()
        {
            return await System.Threading.Tasks.Task.Run(() => ListResults());
        }

        [Description(@"Searches within a cataloged result using regex patterns (case-insensitive).

Searches the FULL, UNTRUNCATED content - useful when results were truncated.

Parameters:
- resultId: From ListResults (e.g., ""R001"")
- searchPattern: Regex pattern (e.g., ""Csc"", ""^Target"", ""error.*failed"", ""\\bNuGet\\b"")
- maxMatches: Max results (default 50)

Returns matching lines with context. Invalid patterns or ResultIds return helpful errors.

Common patterns:
- Simple: ""Csc"" (contains text)
- Line start: ""^Target""
- Multiple: ""(Error|Warning)""
- Word boundary: ""\\bNuGet\\b""
- Numbers: ""Duration: [0-9]+\\.[0-9]+s""

Tip: Start simple, refine based on results. Escape special chars: . * + ? [ ] ( ) { } ^ $ | \\")]
        public async System.Threading.Tasks.Task<string> SearchResultAsync(
            [Description("ResultId to search within (e.g., 'R001'). Use ListResults to see available IDs.")] string resultId,
            [Description("Regex pattern to search for (case-insensitive). Example: 'error|warning' or '^Target.*Build'")] string searchPattern,
            [Description("Maximum number of matches to return (default 50)")] int maxMatches = 50)
        {
            return await System.Threading.Tasks.Task.Run(() => SearchResult(resultId, searchPattern, maxMatches));
        }

        private string ListResults()
        {
            var results = resultManager.ListResults().ToList();

            if (!results.Any())
            {
                return @"No results have been cataloged yet.

Results are automatically cataloged when you use other tools like:
- SearchNodesAsync
- GetErrorsAndWarningsAsync
- GetProjectsAsync
- ListEventsAsync
- ListEmbeddedFilesAsync
- GetEmbeddedFileAsync
- SearchEmbeddedFilesAsync

Run one of these tools first, then use ListResults to see what's available.";
            }

            var sb = new StringBuilder();
            sb.AppendLine("Cataloged Results:");
            sb.AppendLine();

            foreach (var result in results)
            {
                sb.AppendLine($"ResultId: {result.ResultId}");
                
                if (result.WasTruncated)
                {
                    sb.AppendLine($"Truncated: Yes ({result.TruncationPercentage}% removed)");
                }
                else
                {
                    sb.AppendLine("Truncated: No");
                }

                // Format the invocation expression
                string invocation;
                if (string.IsNullOrWhiteSpace(result.Arguments))
                {
                    invocation = $"{result.ToolName}()";
                }
                else
                {
                    invocation = $"{result.ToolName}({result.Arguments})";
                }
                sb.AppendLine($"Invocation: {invocation}");
                
                sb.AppendLine($"Timestamp: {result.Timestamp:yyyy-MM-dd HH:mm:ss}");
                
                if (result.WasTruncated)
                {
                    int displayedLength = result.OriginalLength - (result.OriginalLength * result.TruncationPercentage / 100);
                    sb.AppendLine($"Size: {displayedLength:N0} characters (original: {result.OriginalLength:N0})");
                }
                else
                {
                    sb.AppendLine($"Size: {result.OriginalLength:N0} characters");
                }
                
                sb.AppendLine();
            }

            sb.AppendLine($"Total: {results.Count} result{(results.Count == 1 ? "" : "s")} cataloged");
            sb.AppendLine();
            sb.AppendLine("Use SearchResult to search within any of these results.");

            return sb.ToString();
        }

        private string SearchResult(string resultId, string searchPattern, int maxMatches)
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(resultId))
            {
                return "Error: resultId parameter is required. Use ListResults to see available IDs.";
            }

            if (string.IsNullOrWhiteSpace(searchPattern))
            {
                return "Error: searchPattern parameter is required. Provide a regex pattern to search for.";  
            }

            if (maxMatches < 1 || maxMatches > 500)
            {
                return "Error: maxMatches must be between 1 and 500.";
            }

            // Delegate to ResultManager
            string result = resultManager.SearchResult(resultId, searchPattern, maxMatches, contextLines: 2);
            
            // Truncate if result is too large (using same threshold as MonitoredAIFunction: 12,000 chars)
            const int maxChars = 12000;
            if (result.Length > maxChars)
            {
                int truncatedChars = result.Length - maxChars;
                int truncationPercent = (int)((truncatedChars / (double)result.Length) * 100);
                
                result = result.Substring(0, maxChars) + 
                    $"\n\n... [TRUNCATED: {truncatedChars:N0} characters removed ({truncationPercent}%)]\n" +
                    $"The search returned too many matches. Consider:\n" +
                    $"- Using a more specific regex pattern\n" +
                    $"- Reducing maxMatches parameter\n" +
                    $"- Searching within a more specific ResultId\n" +
                    $"Full results are stored internally but not displayed due to size.";
            }
            
            return result;
        }
    }
}

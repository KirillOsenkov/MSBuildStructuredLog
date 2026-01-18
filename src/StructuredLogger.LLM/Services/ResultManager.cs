using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace StructuredLogger.LLM
{
    /// <summary>
    /// Manages in-memory storage of tool results with unique IDs.
    /// Provides search capabilities within stored results and tracks metadata.
    /// Thread-safe singleton implementation.
    /// </summary>
    public class ResultManager
    {
        private static readonly Lazy<ResultManager> instance = new Lazy<ResultManager>(() => new ResultManager());
        private readonly ConcurrentDictionary<string, ResultInfo> results = new ConcurrentDictionary<string, ResultInfo>();
        private int nextResultId = 1;

        /// <summary>
        /// Gets the singleton instance of ResultManager.
        /// </summary>
        public static ResultManager Instance => instance.Value;

        private ResultManager()
        {
        }

        /// <summary>
        /// Stores a result and returns its unique ID.
        /// Automatically detects truncation by comparing full result with what will be returned.
        /// </summary>
        /// <param name="toolName">Name of the tool that generated the result</param>
        /// <param name="arguments">Arguments passed to the tool (formatted as invocation expression)</param>
        /// <param name="fullResult">The complete, untruncated result</param>
        /// <param name="returnedResult">The result that was actually returned (may be truncated)</param>
        /// <returns>Unique ResultId for this result</returns>
        public string StoreResult(string toolName, string arguments, string fullResult, string returnedResult)
        {
            var resultId = $"R{Interlocked.Increment(ref nextResultId):D3}";
            
            bool wasTruncated = fullResult.Length != returnedResult.Length;
            int percentage = 0;
            
            if (wasTruncated)
            {
                int removedChars = fullResult.Length - returnedResult.Length;
                percentage = fullResult.Length > 0 ? (removedChars * 100) / fullResult.Length : 0;
            }
            
            var info = new ResultInfo
            {
                ResultId = resultId,
                ToolName = toolName,
                Arguments = arguments,
                FullResult = fullResult,
                Timestamp = DateTime.Now,
                OriginalLength = fullResult.Length,
                WasTruncated = wasTruncated,
                TruncationPercentage = percentage
            };

            results[resultId] = info;
            return resultId;
        }

        /// <summary>
        /// Gets result information by ID.
        /// </summary>
        public ResultInfo? GetResult(string resultId)
        {
            if (results.TryGetValue(resultId, out var info))
            {
                return info;
            }
            return null;
        }

        /// <summary>
        /// Lists all stored results ordered by timestamp (most recent first).
        /// </summary>
        public IEnumerable<ResultInfo> ListResults()
        {
            return results.Values.OrderByDescending(r => r.Timestamp);
        }

        /// <summary>
        /// Searches within a specific result using a regex pattern.
        /// </summary>
        /// <param name="resultId">The ID of the result to search</param>
        /// <param name="regexPattern">The regex pattern to search for</param>
        /// <param name="maxMatches">Maximum number of matches to return</param>
        /// <param name="contextLines">Number of context lines before and after each match</param>
        /// <returns>Formatted search results or error message</returns>
        public string SearchResult(string resultId, string regexPattern, int maxMatches = 50, int contextLines = 2)
        {
            // Validate result exists
            if (!results.TryGetValue(resultId, out var resultInfo))
            {
                return FormatInvalidResultIdError(resultId);
            }

            // Validate regex pattern
            Regex regex;
            try
            {
                regex = new Regex(regexPattern, RegexOptions.IgnoreCase | RegexOptions.Multiline, TimeSpan.FromSeconds(5));
            }
            catch (ArgumentException ex)
            {
                return $"Error: Invalid regex pattern.\n{ex.Message}\n\nExample patterns:\n  - Simple text: \"Csc\"\n  - Word boundary: \"\\\\bError\\\\b\"\n  - Case sensitive: Use (?-i) prefix\n  - Line start: \"^Target\"";
            }

            // Split result into lines for context
            var lines = resultInfo.FullResult.Split(new[] { '\r', '\n' }, StringSplitOptions.None);
            var matches = new List<(int lineNum, string line, string matchedText)>();

            // Find all matches
            try
            {
                for (int i = 0; i < lines.Length; i++)
                {
                    var match = regex.Match(lines[i]);
                    if (match.Success)
                    {
                        matches.Add((i + 1, lines[i], match.Value));
                        if (matches.Count >= maxMatches * 2) // Get extra in case we want to show more
                            break;
                    }
                }
            }
            catch (RegexMatchTimeoutException)
            {
                return "Error: Regex search timed out. Please use a simpler pattern.";
            }

            // Format results
            var sb = new StringBuilder();
            sb.AppendLine($"Search Results for ResultId: {resultId}");
            sb.AppendLine($"Pattern: \"{regexPattern}\"");
            sb.AppendLine($"Tool: {resultInfo.ToolName}({resultInfo.Arguments})");
            sb.AppendLine();

            if (matches.Count == 0)
            {
                sb.AppendLine("No matches found.");
                sb.AppendLine();
                sb.AppendLine("Tips:");
                sb.AppendLine("- Check your regex pattern syntax");
                sb.AppendLine("- Search is case-insensitive by default");
                sb.AppendLine("- Try a simpler or broader pattern");
                return sb.ToString();
            }

            int displayCount = Math.Min(matches.Count, maxMatches);
            sb.AppendLine($"Matches: {matches.Count} found (showing first {displayCount})");
            sb.AppendLine();

            for (int i = 0; i < displayCount; i++)
            {
                var (lineNum, line, matchedText) = matches[i];
                sb.AppendLine($"--- Match {i + 1} (line {lineNum}) ---");
                
                // Show context before
                for (int j = Math.Max(0, lineNum - contextLines - 1); j < lineNum - 1; j++)
                {
                    if (j < lines.Length)
                    {
                        sb.AppendLine($"  {j + 1}: {TruncateLine(lines[j], 150)}");
                    }
                }

                // Show matched line (highlight if possible)
                sb.AppendLine($"> {lineNum}: {TruncateLine(line, 200)}");
                
                // Show context after
                for (int j = lineNum; j < Math.Min(lines.Length, lineNum + contextLines); j++)
                {
                    sb.AppendLine($"  {j + 1}: {TruncateLine(lines[j], 150)}");
                }
                
                sb.AppendLine();
            }

            if (matches.Count > maxMatches)
            {
                sb.AppendLine($"[{matches.Count - maxMatches} more matches available. Use maxMatches parameter to see more.]");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Prepends metadata header to a result string.
        /// </summary>
        public string PrependMetadata(string resultId, string content)
        {
            var resultInfo = GetResult(resultId);
            if (resultInfo == null)
            {
                return content;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"ResultId: {resultId}");
            
            if (resultInfo.WasTruncated)
            {
                sb.AppendLine($"Truncated: Yes ({resultInfo.TruncationPercentage}% removed)");
            }
            else
            {
                sb.AppendLine("Truncated: No");
            }
            
            sb.AppendLine();
            sb.Append(content);
            
            return sb.ToString();
        }

        /// <summary>
        /// Clears all stored results. Useful for testing or memory management.
        /// </summary>
        public void Clear()
        {
            results.Clear();
            nextResultId = 0;
        }

        private string FormatInvalidResultIdError(string requestedId)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Error: ResultId '{requestedId}' not found.");
            sb.AppendLine();
            
            if (results.IsEmpty)
            {
                sb.AppendLine("No results have been cataloged yet.");
                sb.AppendLine("Run other tools (SearchNodes, ListEvents, etc.) to generate results.");
            }
            else
            {
                sb.AppendLine("Available ResultIds:");
                foreach (var result in results.Values.OrderBy(r => r.ResultId))
                {
                    sb.AppendLine($"  - {result.ResultId}: {result.ToolName}");
                }
            }
            
            return sb.ToString();
        }

        private string TruncateLine(string line, int maxLength)
        {
            if (string.IsNullOrEmpty(line) || line.Length <= maxLength)
                return line;
            
            return line.Substring(0, maxLength) + "...";
        }

        /// <summary>
        /// Gets the count of stored results.
        /// </summary>
        public int Count => results.Count;
    }

    /// <summary>
    /// Represents information about a stored result.
    /// </summary>
    public class ResultInfo
    {
        /// <summary>
        /// Unique identifier for this result (e.g., "R001", "R002").
        /// </summary>
        public string ResultId { get; set; } = "";

        /// <summary>
        /// Name of the tool that generated this result.
        /// </summary>
        public string ToolName { get; set; } = "";

        /// <summary>
        /// Arguments/parameters passed to the tool, formatted for display.
        /// </summary>
        public string Arguments { get; set; } = "";

        /// <summary>
        /// The complete, untruncated result content.
        /// </summary>
        public string FullResult { get; set; } = "";

        /// <summary>
        /// Timestamp when this result was stored.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Original length of the result in characters.
        /// </summary>
        public int OriginalLength { get; set; }

        /// <summary>
        /// Whether this result was truncated when returned to the LLM.
        /// </summary>
        public bool WasTruncated { get; set; }

        /// <summary>
        /// Percentage of content removed if truncated (0-100).
        /// </summary>
        public int TruncationPercentage { get; set; }
    }
}

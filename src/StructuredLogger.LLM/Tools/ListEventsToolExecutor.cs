using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using Microsoft.Build.Logging.StructuredLogger;
using Microsoft.Extensions.AI;

namespace StructuredLogger.LLM
{
    /// <summary>
    /// Tool for listing and filtering build events with detailed information.
    /// Provides chronological access to projects, targets, tasks, errors, warnings, and messages.
    /// </summary>
    public class ListEventsToolExecutor : IToolsContainer
    {
        private readonly Build build;
        private readonly ListEventsToolExecutorCore core;

        public ListEventsToolExecutor(Build build)
        {
            this.build = build ?? throw new ArgumentNullException(nameof(build));
            this.core = new ListEventsToolExecutorCore(build);
        }

        public bool HasGuiTools => false;

        public IEnumerable<(AIFunction Function, AgentPhase ApplicablePhases)> GetTools()
        {
            yield return (AIFunctionFactory.Create(ListEventsAsync), AgentPhase.Research | AgentPhase.Summarization);
        }

        [Description(@"Lists build events from the binlog with detailed filtering and pagination.

This tool provides access to the chronological sequence of build events (projects, targets, 
tasks, errors, warnings, messages) with rich filtering capabilities.

KEY CONCEPTS:
- Events are chronologically ordered by start time (unless sorted differently)
- Each event includes timestamp, duration (for timed events), and context (parent project/target)
- Large builds may have thousands of events - use filters to narrow results

EVENT TYPES:
- Project: MSBuild project execution
- Target: Target execution within a project
- Task: Individual task (Csc, Copy, etc.) execution
- Error: Build errors with code, file, line number
- Warning: Build warnings with code, file, line number
- Message: Build messages and output

FILTERING OPTIONS:

Time-based filters:
  - startAfter/startBefore: Filter by event start time (format: 'yyyy-MM-dd HH:mm:ss' or 'yyyy-MM-ddTHH:mm:ss')
  - endAfter/endBefore: Filter by event end time
  - minDuration/maxDuration: Filter by event duration (format: 'HH:mm:ss' or seconds as number)

Context-based filters:
  - projectName: Filter by project name (partial match, case-insensitive)
  - projectPath: Filter by exact project file path
  - targetName: Filter by target name (partial match)
  - taskName: Filter by task name (e.g., 'Csc', 'Copy')

Content-based filters:
  - searchText: Search in event text and properties (case-insensitive)
  - errorCode/warningCode: Filter diagnostics by code (e.g., 'CS0103', 'MSB3644')

Status filters:
  - succeeded: true for succeeded only, false for failed only, null for all
  - includeSkipped: Include skipped targets (default: true)
  - includeLowRelevance: Include low-relevance messages (default: false)

Pagination and sorting:
  - maxResults: Limit number of results (default 50, recommended max 200)
  - skip: Skip first N results for pagination
  - sortBy: 'startTime' (default), 'duration', 'name'
  - descending: true for descending order (default: false)

USAGE EXAMPLES:

1. Find long-running tasks:
   eventTypes: ['Task'], minDuration: '00:00:05', sortBy: 'duration', descending: true

2. Errors in specific time range:
   eventTypes: ['Error'], startAfter: '2024-01-02 10:30:00', projectName: 'MyApp'

3. All compiler invocations:
   eventTypes: ['Task'], taskName: 'Csc'

4. Target and its tasks:
   eventTypes: ['Target', 'Task'], targetName: 'CoreCompile', projectName: 'MyApp'

5. Build timeline overview:
   eventTypes: ['Project', 'Target'], sortBy: 'startTime'

TIPS:
- Start with broader filters and narrow down based on results
- Use time filters when investigating specific build phases
- Combine event types for related analysis (e.g., Target + Task)
- Use pagination (skip/maxResults) for large result sets
- Sort by duration to find performance bottlenecks
")]
        public async System.Threading.Tasks.Task<string> ListEventsAsync(
            [Description("Array of event types to include: 'Project', 'Target', 'Task', 'Error', 'Warning', 'Message'. Defaults to all types if not specified.")]
            string[]? eventTypes = null,

            [Description("Filter events that started after this time (format: 'yyyy-MM-dd HH:mm:ss' or 'yyyy-MM-ddTHH:mm:ss')")]
            string? startAfter = null,

            [Description("Filter events that started before this time")]
            string? startBefore = null,

            [Description("Filter events that ended after this time")]
            string? endAfter = null,

            [Description("Filter events that ended before this time")]
            string? endBefore = null,

            [Description("Filter events with duration >= this value (format: 'HH:mm:ss' or seconds as string)")]
            string? minDuration = null,

            [Description("Filter events with duration <= this value")]
            string? maxDuration = null,

            [Description("Filter by project name (partial match, case-insensitive)")]
            string? projectName = null,

            [Description("Filter by exact project file path")]
            string? projectPath = null,

            [Description("Filter by target name (partial match, case-insensitive)")]
            string? targetName = null,

            [Description("Filter by task name (e.g., 'Csc', 'Copy', 'MSBuild')")]
            string? taskName = null,

            [Description("Search text in event content (case-insensitive)")]
            string? searchText = null,

            [Description("Filter errors by code (e.g., 'CS0103')")]
            string? errorCode = null,

            [Description("Filter warnings by code (e.g., 'MSB3644')")]
            string? warningCode = null,

            [Description("Filter by success status: true for succeeded, false for failed, null for all")]
            bool? succeeded = null,

            [Description("Include skipped targets in results (default: true)")]
            bool includeSkipped = true,

            [Description("Include low-relevance messages in results (default: false)")]
            bool includeLowRelevance = false,

            [Description("Maximum number of results to return (default: 50)")]
            int maxResults = 50,

            [Description("Number of results to skip for pagination (default: 0)")]
            int skip = 0,

            [Description("Sort results by: 'startTime' (default), 'duration', 'name'")]
            string sortBy = "startTime",

            [Description("Sort in descending order (default: false)")]
            bool descending = false)
        {
            return await System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    var filters = new EventFilters
                    {
                        EventTypes = eventTypes,
                        StartAfter = ParseDateTime(startAfter),
                        StartBefore = ParseDateTime(startBefore),
                        EndAfter = ParseDateTime(endAfter),
                        EndBefore = ParseDateTime(endBefore),
                        MinDuration = ParseTimeSpan(minDuration),
                        MaxDuration = ParseTimeSpan(maxDuration),
                        ProjectName = projectName,
                        ProjectPath = projectPath,
                        TargetName = targetName,
                        TaskName = taskName,
                        SearchText = searchText,
                        ErrorCode = errorCode,
                        WarningCode = warningCode,
                        Succeeded = succeeded,
                        IncludeSkipped = includeSkipped,
                        IncludeLowRelevance = includeLowRelevance,
                        MaxResults = maxResults,
                        Skip = skip,
                        SortBy = sortBy,
                        Descending = descending
                    };

                    return core.ListEvents(filters);
                }
                catch (Exception ex)
                {
                    return $"Error executing ListEvents: {ex.Message}";
                }
            });
        }

        private DateTime? ParseDateTime(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            if (DateTime.TryParse(value, out var result))
                return result;

            throw new ArgumentException($"Invalid date/time format: '{value}'. Use 'yyyy-MM-dd HH:mm:ss' or 'yyyy-MM-ddTHH:mm:ss'");
        }

        private TimeSpan? ParseTimeSpan(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            // Try parsing as TimeSpan (HH:mm:ss format)
            if (TimeSpan.TryParse(value, out var result))
                return result;

            // Try parsing as seconds
            if (double.TryParse(value, out var seconds))
                return TimeSpan.FromSeconds(seconds);

            throw new ArgumentException($"Invalid duration format: '{value}'. Use 'HH:mm:ss' or seconds as number");
        }
    }
}

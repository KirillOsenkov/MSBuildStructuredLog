using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Build.Logging.StructuredLogger;

namespace StructuredLogger.LLM
{
    /// <summary>
    /// Core implementation of event listing and filtering logic.
    /// </summary>
    internal class ListEventsToolExecutorCore
    {
        private readonly Build build;
        private const int MaxOutputTokensPerTool = 3000; // Roughly 12,000 characters

        public ListEventsToolExecutorCore(Build build)
        {
            this.build = build ?? throw new ArgumentNullException(nameof(build));
        }

        public string ListEvents(EventFilters filters)
        {
            // Validate inputs
            ValidateFilters(filters);

            // Collect matching events
            var events = CollectEvents(filters);

            // Apply sorting
            events = SortEvents(events, filters.SortBy, filters.Descending);

            // Get total count before pagination
            int totalCount = events.Count;

            // Apply pagination
            events = events.Skip(filters.Skip).Take(filters.MaxResults).ToList();

            // Format output
            return FormatEvents(events, totalCount, filters);
        }

        private void ValidateFilters(EventFilters filters)
        {
            if (filters.MaxResults < 1 || filters.MaxResults > 1000)
            {
                throw new ArgumentException("maxResults must be between 1 and 1000");
            }

            if (filters.Skip < 0)
            {
                throw new ArgumentException("skip must be >= 0");
            }

            if (filters.StartAfter.HasValue && filters.StartBefore.HasValue &&
                filters.StartAfter.Value >= filters.StartBefore.Value)
            {
                throw new ArgumentException("startAfter must be before startBefore");
            }

            if (filters.EndAfter.HasValue && filters.EndBefore.HasValue &&
                filters.EndAfter.Value >= filters.EndBefore.Value)
            {
                throw new ArgumentException("endAfter must be before endBefore");
            }

            if (filters.MinDuration.HasValue && filters.MaxDuration.HasValue &&
                filters.MinDuration.Value >= filters.MaxDuration.Value)
            {
                throw new ArgumentException("minDuration must be less than maxDuration");
            }

            var validSortBy = new[] { "starttime", "duration", "name" };
            if (!validSortBy.Contains(filters.SortBy.ToLowerInvariant()))
            {
                throw new ArgumentException($"sortBy must be one of: {string.Join(", ", validSortBy)}");
            }
        }

        private List<EventInfo> CollectEvents(EventFilters filters)
        {
            var events = new List<EventInfo>();

            // Determine which types to include
            var includeTypes = GetIncludedTypes(filters.EventTypes);

            build.VisitAllChildren<BaseNode>(node =>
            {
                var eventInfo = CreateEventInfo(node, includeTypes, filters);
                if (eventInfo != null)
                {
                    events.Add(eventInfo);
                }
            });

            return events;
        }

        private HashSet<string> GetIncludedTypes(string[]? eventTypes)
        {
            if (eventTypes == null || eventTypes.Length == 0)
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "Project", "Target", "Task", "Error", "Warning", "Message"
                };
            }

            return new HashSet<string>(eventTypes, StringComparer.OrdinalIgnoreCase);
        }

        private EventInfo? CreateEventInfo(BaseNode node, HashSet<string> includeTypes, EventFilters filters)
        {
            EventInfo? info = null;

            // Check event type and create appropriate info
            if (node is Project project && includeTypes.Contains("Project"))
            {
                info = CreateProjectInfo(project);
            }
            else if (node is Target target && includeTypes.Contains("Target"))
            {
                info = CreateTargetInfo(target);
            }
            else if (node is Task task && includeTypes.Contains("Task"))
            {
                info = CreateTaskInfo(task);
            }
            else if (node is Error error && includeTypes.Contains("Error"))
            {
                info = CreateErrorInfo(error);
            }
            else if (node is Warning warning && includeTypes.Contains("Warning"))
            {
                info = CreateWarningInfo(warning);
            }
            else if (node is Message message && includeTypes.Contains("Message"))
            {
                info = CreateMessageInfo(message);
            }

            // Apply filters
            if (info != null && !PassesFilters(info, filters))
            {
                return null;
            }

            return info;
        }

        private EventInfo CreateProjectInfo(Project project)
        {
            return new EventInfo
            {
                Type = "Project",
                Name = project.Name,
                StartTime = project.StartTime,
                EndTime = project.EndTime,
                Duration = project.Duration,
                Node = project,
                Details = new Dictionary<string, string>
                {
                    ["Path"] = project.ProjectFile ?? "",
                    ["Framework"] = project.TargetFramework ?? "",
                    ["Configuration"] = project.Configuration ?? "",
                    ["Platform"] = project.Platform ?? "",
                    ["Status"] = project is TimedNode tn && tn.EndTime != default ? "Completed" : "In Progress"
                }
            };
        }

        private EventInfo CreateTargetInfo(Target target)
        {
            return new EventInfo
            {
                Type = "Target",
                Name = target.Name,
                StartTime = target.StartTime,
                EndTime = target.EndTime,
                Duration = target.Duration,
                Node = target,
                Details = new Dictionary<string, string>
                {
                    ["Project"] = target.Project?.Name ?? "Unknown",
                    ["Status"] = target.Succeeded ? "Succeeded" : "Failed",
                    ["Skipped"] = target.Skipped.ToString(),
                    ["Reason"] = target.ParentTarget ?? target.TargetBuiltReason.ToString(),
                    ["SourceFile"] = target.SourceFilePath ?? ""
                }
            };
        }

        private EventInfo CreateTaskInfo(Task task)
        {
            return new EventInfo
            {
                Type = "Task",
                Name = task.Name,
                StartTime = task.StartTime,
                EndTime = task.EndTime,
                Duration = task.Duration,
                Node = task,
                Details = new Dictionary<string, string>
                {
                    ["Target"] = task.GetNearestParent<Target>()?.Name ?? "Unknown",
                    ["Project"] = task.GetNearestParent<Project>()?.Name ?? "Unknown",
                    ["Assembly"] = task.FromAssembly ?? "",
                    ["SourceFile"] = task.SourceFilePath ?? "",
                    ["LineNumber"] = task.LineNumber?.ToString() ?? "",
                    ["CommandLine"] = TruncateString(task.CommandLineArguments, 200) ?? ""
                }
            };
        }

        private EventInfo CreateErrorInfo(Error error)
        {
            return new EventInfo
            {
                Type = "Error",
                Name = error.Code ?? "Error",
                StartTime = error.Timestamp,
                EndTime = error.Timestamp,
                Duration = TimeSpan.Zero,
                Node = error,
                Details = new Dictionary<string, string>
                {
                    ["Code"] = error.Code ?? "",
                    ["Message"] = error.Text ?? "",
                    ["File"] = error.File ?? "",
                    ["Line"] = error.LineNumber.ToString(),
                    ["Column"] = error.ColumnNumber.ToString(),
                    ["Project"] = error.GetNearestParent<Project>()?.Name ?? "Unknown",
                    ["ProjectFile"] = error.ProjectFile ?? ""
                }
            };
        }

        private EventInfo CreateWarningInfo(Warning warning)
        {
            return new EventInfo
            {
                Type = "Warning",
                Name = warning.Code ?? "Warning",
                StartTime = warning.Timestamp,
                EndTime = warning.Timestamp,
                Duration = TimeSpan.Zero,
                Node = warning,
                Details = new Dictionary<string, string>
                {
                    ["Code"] = warning.Code ?? "",
                    ["Message"] = warning.Text ?? "",
                    ["File"] = warning.File ?? "",
                    ["Line"] = warning.LineNumber.ToString(),
                    ["Column"] = warning.ColumnNumber.ToString(),
                    ["Project"] = warning.GetNearestParent<Project>()?.Name ?? "Unknown",
                    ["ProjectFile"] = warning.ProjectFile ?? ""
                }
            };
        }

        private EventInfo CreateMessageInfo(Message message)
        {
            var isLowRelevance = message is IHasRelevance relevance && relevance.IsLowRelevance;

            return new EventInfo
            {
                Type = "Message",
                Name = TruncateString(message.Text, 80) ?? "",
                StartTime = message.Timestamp,
                EndTime = message.Timestamp,
                Duration = TimeSpan.Zero,
                Node = message,
                Details = new Dictionary<string, string>
                {
                    ["Text"] = message.Text ?? "",
                    ["Project"] = message.GetNearestParent<Project>()?.Name ?? "Unknown",
                    ["Target"] = message.GetNearestParent<Target>()?.Name ?? "",
                    ["LowRelevance"] = isLowRelevance.ToString()
                }
            };
        }

        private bool PassesFilters(EventInfo info, EventFilters filters)
        {
            // Time filters
            if (filters.StartAfter.HasValue && info.StartTime < filters.StartAfter.Value)
                return false;

            if (filters.StartBefore.HasValue && info.StartTime >= filters.StartBefore.Value)
                return false;

            if (filters.EndAfter.HasValue && info.EndTime < filters.EndAfter.Value)
                return false;

            if (filters.EndBefore.HasValue && info.EndTime >= filters.EndBefore.Value)
                return false;

            // Duration filters
            if (filters.MinDuration.HasValue && info.Duration < filters.MinDuration.Value)
                return false;

            if (filters.MaxDuration.HasValue && info.Duration > filters.MaxDuration.Value)
                return false;

            // Context filters
            if (!string.IsNullOrWhiteSpace(filters.ProjectName))
            {
                string? projectName;
                var projectNameVal = info.Details.TryGetValue("Project", out projectName!) ? projectName : null;
                if (projectNameVal == null)
                {
                    projectNameVal = (info.Node as Project)?.Name;
                }
                if (projectNameVal == null || projectNameVal.IndexOf(filters.ProjectName, StringComparison.OrdinalIgnoreCase) < 0)
                    return false;
            }

            if (!string.IsNullOrWhiteSpace(filters.ProjectPath))
            {
                string? projectPath;
                if (!info.Details.TryGetValue("Path", out projectPath!))
                {
                    info.Details.TryGetValue("ProjectFile", out projectPath!);
                }
                if (!string.Equals(projectPath, filters.ProjectPath, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            if (!string.IsNullOrWhiteSpace(filters.TargetName))
            {
                string? targetName;
                var targetNameVal = info.Details.TryGetValue("Target", out targetName!) ? targetName : null;
                if (targetNameVal == null)
                {
                    targetNameVal = (info.Node as Target)?.Name;
                }
                if (targetNameVal == null || targetNameVal.IndexOf(filters.TargetName, StringComparison.OrdinalIgnoreCase) < 0)
                    return false;
            }

            if (!string.IsNullOrWhiteSpace(filters.TaskName))
            {
                if (info.Type != "Task" || info.Name.IndexOf(filters.TaskName, StringComparison.OrdinalIgnoreCase) < 0)
                    return false;
            }

            // Content filters
            if (!string.IsNullOrWhiteSpace(filters.SearchText))
            {
                var searchIn = info.Name + " " + string.Join(" ", info.Details.Values);
                if (searchIn.IndexOf(filters.SearchText, StringComparison.OrdinalIgnoreCase) < 0)
                    return false;
            }

            if (!string.IsNullOrWhiteSpace(filters.ErrorCode))
            {
                string? errorCode;
                if (info.Type != "Error" || !info.Details.TryGetValue("Code", out errorCode!) || 
                    !string.Equals(errorCode, filters.ErrorCode, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            if (!string.IsNullOrWhiteSpace(filters.WarningCode))
            {
                string? warningCode;
                if (info.Type != "Warning" || !info.Details.TryGetValue("Code", out warningCode!) || 
                    !string.Equals(warningCode, filters.WarningCode, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            // Status filters
            if (filters.Succeeded.HasValue)
            {
                bool succeeded = false;
                if (info.Node is Project p)
                    succeeded = true; // Project nodes don't have Succeeded property in all cases
                else if (info.Node is Target t)
                    succeeded = t.Succeeded;
                else if (info.Type == "Error")
                    succeeded = false;
                else
                    return true; // Non-applicable filter

                if (succeeded != filters.Succeeded.Value)
                    return false;
            }

            string? skippedValue;
            if (!filters.IncludeSkipped && info.Details.TryGetValue("Skipped", out skippedValue!) && skippedValue == "True")
                return false;

            string? lowRelevanceValue;
            if (!filters.IncludeLowRelevance && info.Details.TryGetValue("LowRelevance", out lowRelevanceValue!) && lowRelevanceValue == "True")
                return false;

            return true;
        }

        private List<EventInfo> SortEvents(List<EventInfo> events, string sortBy, bool descending)
        {
            IOrderedEnumerable<EventInfo> sorted;

            switch (sortBy.ToLowerInvariant())
            {
                case "duration":
                    sorted = descending
                        ? events.OrderByDescending(e => e.Duration)
                        : events.OrderBy(e => e.Duration);
                    break;

                case "name":
                    sorted = descending
                        ? events.OrderByDescending(e => e.Name)
                        : events.OrderBy(e => e.Name);
                    break;

                case "starttime":
                default:
                    sorted = descending
                        ? events.OrderByDescending(e => e.StartTime)
                        : events.OrderBy(e => e.StartTime);
                    break;
            }

            return sorted.ToList();
        }

        private string FormatEvents(List<EventInfo> events, int totalCount, EventFilters filters)
        {
            var sb = new StringBuilder();

            // Header
            sb.AppendLine($"=== Build Events ===");
            sb.AppendLine($"Total matching: {totalCount}");
            sb.AppendLine($"Showing: {events.Count} events (skip: {filters.Skip}, max: {filters.MaxResults})");
            if (filters.EventTypes != null && filters.EventTypes.Length > 0)
            {
                sb.AppendLine($"Filtered by types: {string.Join(", ", filters.EventTypes!)}");
            }
            sb.AppendLine();

            // Events
            int displayIndex = 1;
            foreach (var evt in events)
            {
                sb.AppendLine($"Event #{displayIndex + filters.Skip}: {evt.Type}");
                sb.AppendLine($"  Name: {evt.Name}");

                // Time information (for timed events)
                if (evt.Duration > TimeSpan.Zero || evt.StartTime != default)
                {
                    if (evt.StartTime != default)
                    {
                        sb.AppendLine($"  Start: {evt.StartTime:yyyy-MM-dd HH:mm:ss.fff}");
                    }
                    if (evt.EndTime != default && evt.EndTime != evt.StartTime)
                    {
                        sb.AppendLine($"  End: {evt.EndTime:yyyy-MM-dd HH:mm:ss.fff}");
                    }
                    if (evt.Duration > TimeSpan.Zero)
                    {
                        sb.AppendLine($"  Duration: {FormatDuration(evt.Duration)}");
                    }
                }
                else if (evt.StartTime != default)
                {
                    sb.AppendLine($"  Timestamp: {evt.StartTime:yyyy-MM-dd HH:mm:ss.fff}");
                }

                // Details
                foreach (var detail in evt.Details)
                {
                    if (!string.IsNullOrWhiteSpace(detail.Value) && detail.Value != "0" && detail.Value != "Unknown")
                    {
                        sb.AppendLine($"  {detail.Key}: {detail.Value}");
                    }
                }

                sb.AppendLine();
                displayIndex++;
            }

            // Footer
            if (totalCount > events.Count + filters.Skip)
            {
                sb.AppendLine($"[{totalCount - events.Count - filters.Skip} more events available. Use skip={filters.Skip + events.Count} to see next page.]");
            }

            if (totalCount == 0)
            {
                sb.AppendLine("No events match the specified filters.");
                sb.AppendLine();
                sb.AppendLine("Tips:");
                sb.AppendLine("- Try removing or relaxing some filters");
                sb.AppendLine("- Check time ranges are correct");
                sb.AppendLine("- Verify names match actual project/target/task names");
            }

            return TruncateIfNeeded(sb.ToString());
        }

        private string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalSeconds < 1)
            {
                return $"{duration.TotalMilliseconds:F0}ms";
            }
            else if (duration.TotalMinutes < 1)
            {
                return $"{duration.TotalSeconds:F2}s";
            }
            else
            {
                return $"{(int)duration.TotalMinutes}m {duration.Seconds}s";
            }
        }

        private string? TruncateString(string? value, int maxLength)
        {
            if (value == null || value.Length <= maxLength)
                return value;

            return value.Substring(0, maxLength) + "...";
        }

        private string TruncateIfNeeded(string result)
        {
            const int maxChars = MaxOutputTokensPerTool * 4; // Conservative estimate
            if (result.Length > maxChars)
            {
                return result.Substring(0, maxChars) + "\n\n[Output truncated due to length. Use more specific filters to narrow results.]";
            }
            return result;
        }
    }

    /// <summary>
    /// Represents filters for event listing.
    /// </summary>
    internal class EventFilters
    {
        public string[]? EventTypes { get; set; }
        public DateTime? StartAfter { get; set; }
        public DateTime? StartBefore { get; set; }
        public DateTime? EndAfter { get; set; }
        public DateTime? EndBefore { get; set; }
        public TimeSpan? MinDuration { get; set; }
        public TimeSpan? MaxDuration { get; set; }
        public string? ProjectName { get; set; }
        public string? ProjectPath { get; set; }
        public string? TargetName { get; set; }
        public string? TaskName { get; set; }
        public string? SearchText { get; set; }
        public string? ErrorCode { get; set; }
        public string? WarningCode { get; set; }
        public bool? Succeeded { get; set; }
        public bool IncludeSkipped { get; set; } = true;
        public bool IncludeLowRelevance { get; set; } = false;
        public int MaxResults { get; set; } = 50;
        public int Skip { get; set; } = 0;
        public string SortBy { get; set; } = "startTime";
        public bool Descending { get; set; } = false;
    }

    /// <summary>
    /// Represents information about a build event.
    /// </summary>
    internal class EventInfo
    {
        public string Type { get; set; } = "";
        public string Name { get; set; } = "";
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration { get; set; }
        public BaseNode Node { get; set; } = null!;
        public Dictionary<string, string> Details { get; set; } = new Dictionary<string, string>();
    }
}

using System;
using System.Collections.Generic;

namespace StructuredLogger.LLM
{
    /// <summary>
    /// Represents the current execution phase status of an agent workflow.
    /// Different from <see cref="AgentPhase"/> which is a Flags enum for tool availability.
    /// </summary>
    public enum AgentExecutionPhase
    {
        Idle,
        Planning,
        Research,
        Summarization,
        Complete,
        Failed
    }

    /// <summary>
    /// Represents the status of an individual research task.
    /// </summary>
    public enum TaskStatus
    {
        NotStarted,
        InProgress,
        Complete,
        Failed
    }

    /// <summary>
    /// Represents a single research task in the agent plan.
    /// </summary>
    public class ResearchTask
    {
        public string Id { get; set; }
        public string Description { get; set; }
        public string Goal { get; set; }
        public TaskStatus Status { get; set; }
        public string Findings { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string Error { get; set; }

        public TimeSpan? Duration => EndTime.HasValue && StartTime.HasValue 
            ? EndTime.Value - StartTime.Value 
            : null;

        public string StatusEmoji => Status switch
        {
            TaskStatus.Complete => "✓",
            TaskStatus.InProgress => "⏳",
            TaskStatus.Failed => "❌",
            _ => "○"
        };

        public ResearchTask(string id, string description, string goal)
        {
            Id = id;
            Description = description;
            Goal = goal;
            Status = TaskStatus.NotStarted;
            Findings = string.Empty;
            Error = string.Empty;
        }
    }

    /// <summary>
    /// Represents the overall agent plan with multiple research tasks.
    /// </summary>
    public class AgentPlan
    {
        public string UserQuery { get; set; }
        public List<ResearchTask> ResearchTasks { get; set; }
        public AgentExecutionPhase Phase { get; set; }
        public int CurrentTaskIndex { get; set; }
        public Dictionary<string, string> Findings { get; set; }
        public string? FinalSummary { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string? Error { get; set; }

        public TimeSpan? Duration => EndTime.HasValue 
            ? EndTime.Value - StartTime 
            : (DateTime.Now - StartTime);

        public ResearchTask? CurrentTask => CurrentTaskIndex >= 0 && CurrentTaskIndex < ResearchTasks.Count 
            ? ResearchTasks[CurrentTaskIndex] 
            : null;

        public int CompletedTaskCount => ResearchTasks.FindAll(t => t.Status == TaskStatus.Complete).Count;
        public int FailedTaskCount => ResearchTasks.FindAll(t => t.Status == TaskStatus.Failed).Count;

        public AgentPlan(string userQuery)
        {
            UserQuery = userQuery;
            ResearchTasks = new List<ResearchTask>();
            Phase = AgentExecutionPhase.Idle;
            CurrentTaskIndex = -1;
            Findings = new Dictionary<string, string>();
            StartTime = DateTime.Now;
        }
    }

    /// <summary>
    /// Event args for agent progress updates.
    /// </summary>
    public class AgentProgressEventArgs : EventArgs
    {
        public AgentExecutionPhase Phase { get; set; }
        public ResearchTask? CurrentTask { get; set; }
        public AgentPlan Plan { get; set; }
        public string? Message { get; set; }
        public bool IsError { get; set; }

        public AgentProgressEventArgs(AgentPlan plan, string? message = null, bool isError = false)
        {
            Plan = plan;
            Phase = plan.Phase;
            CurrentTask = plan.CurrentTask;
            Message = message;
            IsError = isError;
        }
    }
}

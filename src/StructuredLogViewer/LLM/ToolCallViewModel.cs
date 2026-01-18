using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using StructuredLogger.LLM;

namespace StructuredLogViewer.LLM
{
    /// <summary>
    /// View model for displaying tool call information in the UI.
    /// Provides user-friendly formatting and collapsible display support.
    /// </summary>
    public class ToolCallViewModel : INotifyPropertyChanged
    {
        private bool isExpanded;
        private bool isInProgress;
        private string resultText;
        private TimeSpan? duration;
        private bool isError;
        private string errorMessage;

        public ToolCallViewModel(ToolCallInfo toolCallInfo)
        {
            if (toolCallInfo == null)
            {
                throw new ArgumentNullException(nameof(toolCallInfo));
            }

            CallId = toolCallInfo.CallId;
            ToolName = toolCallInfo.ToolName;
            StartTime = toolCallInfo.StartTime;
            duration = toolCallInfo.Duration;
            isError = toolCallInfo.IsError;
            errorMessage = toolCallInfo.ErrorMessage;
            resultText = toolCallInfo.ResultText;

            // If no end time, this is an in-progress call
            isInProgress = !toolCallInfo.EndTime.HasValue;

            // Parse arguments for structured display
            ParsedArguments = toolCallInfo.GetParsedArguments();
            ArgumentsSummary = toolCallInfo.GetArgumentsSummary(80);
        }

        public Guid CallId { get; }
        public string ToolName { get; }
        public string ArgumentsSummary { get; }
        public Dictionary<string, string> ParsedArguments { get; }
        public DateTime StartTime { get; }

        public string ResultText
        {
            get => resultText;
            private set
            {
                if (resultText != value)
                {
                    resultText = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(FormattedResult));
                }
            }
        }

        public TimeSpan? Duration
        {
            get => duration;
            private set
            {
                if (duration != value)
                {
                    duration = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DurationText));
                }
            }
        }

        public bool IsError
        {
            get => isError;
            private set
            {
                if (isError != value)
                {
                    isError = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ErrorMessage
        {
            get => errorMessage;
            private set
            {
                if (errorMessage != value)
                {
                    errorMessage = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsInProgress
        {
            get => isInProgress;
            private set
            {
                if (isInProgress != value)
                {
                    isInProgress = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HeaderText));
                    OnPropertyChanged(nameof(DurationText));
                }
            }
        }

        public bool IsExpanded
        {
            get => isExpanded;
            set
            {
                if (isExpanded != value)
                {
                    isExpanded = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets the header text shown in collapsed state.
        /// </summary>
        public string HeaderText => IsInProgress
            ? $"⏳ {ToolName}: {ArgumentsSummary} (In Progress...)"
            : $"🔧 {ToolName}: {ArgumentsSummary}";

        /// <summary>
        /// Gets formatted duration text.
        /// </summary>
        public string DurationText => IsInProgress
            ? "In progress..."
            : (Duration.HasValue ? $"{Duration.Value.TotalMilliseconds:F0}ms" : "N/A");

        /// <summary>
        /// Updates this view model with completion data from a ToolCallInfo.
        /// </summary>
        public void UpdateWithCompletion(ToolCallInfo completedCallInfo)
        {
            if (completedCallInfo.CallId != CallId)
            {
                throw new InvalidOperationException("CallId mismatch when updating tool call");
            }

            IsInProgress = false;
            Duration = completedCallInfo.Duration;
            IsError = completedCallInfo.IsError;
            ErrorMessage = completedCallInfo.ErrorMessage;
            ResultText = completedCallInfo.ResultText ?? "(no result)";
        }

        /// <summary>
        /// Gets formatted arguments for display.
        /// </summary>
        public string FormattedArguments
        {
            get
            {
                if (ParsedArguments == null || ParsedArguments.Count == 0)
                {
                    return "(no arguments)";
                }

                return string.Join("\n", ParsedArguments.Select(kvp => $"{kvp.Key}: {kvp.Value}"));
            }
        }

        /// <summary>
        /// Gets formatted result text with truncation if too long.
        /// </summary>
        public string FormattedResult
        {
            get
            {
                if (string.IsNullOrWhiteSpace(ResultText))
                {
                    return "(no result)";
                }

                // Truncate very long results
                const int maxLength = 5000;
                if (ResultText.Length > maxLength)
                {
                    return ResultText.Substring(0, maxLength) + "\n\n... (truncated)";
                }

                return ResultText;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace StructuredLogViewer.LLM
{
    /// <summary>
    /// View model for displaying tool call information in the UI.
    /// Provides user-friendly formatting and collapsible display support.
    /// </summary>
    public class ToolCallViewModel : INotifyPropertyChanged
    {
        private bool isExpanded;

        public ToolCallViewModel(ToolCallInfo toolCallInfo)
        {
            if (toolCallInfo == null)
            {
                throw new ArgumentNullException(nameof(toolCallInfo));
            }

            ToolName = toolCallInfo.ToolName;
            StartTime = toolCallInfo.StartTime;
            Duration = toolCallInfo.Duration;
            IsError = toolCallInfo.IsError;
            ErrorMessage = toolCallInfo.ErrorMessage;
            ResultText = toolCallInfo.ResultText;

            // Parse arguments for structured display
            ParsedArguments = toolCallInfo.GetParsedArguments();
            ArgumentsSummary = toolCallInfo.GetArgumentsSummary(80);
        }

        public string ToolName { get; }
        public string ArgumentsSummary { get; }
        public Dictionary<string, string> ParsedArguments { get; }
        public string ResultText { get; }
        public DateTime StartTime { get; }
        public TimeSpan? Duration { get; }
        public bool IsError { get; }
        public string ErrorMessage { get; }

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
        public string HeaderText => $"🔧 {ToolName}: {ArgumentsSummary}";

        /// <summary>
        /// Gets formatted duration text.
        /// </summary>
        public string DurationText => Duration.HasValue 
            ? $"{Duration.Value.TotalMilliseconds:F0}ms" 
            : "N/A";

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

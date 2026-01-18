using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using StructuredLogger.LLM;

namespace StructuredLogViewer.Controls
{
    public partial class AgentProgressPanel : UserControl
    {
        private readonly ObservableCollection<ResearchTask> tasks;
        private bool isCollapsed = false;
        private double expandedHeight = 150;

        public AgentProgressPanel()
        {
            InitializeComponent();
            tasks = new ObservableCollection<ResearchTask>();
            tasksPanel.ItemsSource = tasks;
            this.Visibility = Visibility.Collapsed;
        }

        public void UpdateProgress(AgentProgressEventArgs args)
        {
            Dispatcher.Invoke(() =>
            {
                // Show panel if hidden
                if (this.Visibility != Visibility.Visible)
                {
                    this.Visibility = Visibility.Visible;
                }

                var plan = args.Plan;

                // Update header
                headerText.Text = $"Agent Mode: {plan.Phase}";
                if (plan.Phase == AgentExecutionPhase.Research && plan.CurrentTask != null)
                {
                    headerText.Text += $" (Task {plan.CurrentTaskIndex + 1}/{plan.ResearchTasks.Count})";
                }

                // Update progress message
                if (!string.IsNullOrEmpty(args.Message))
                {
                    progressMessage.Text = args.Message;
                    progressMessage.Foreground = args.IsError 
                        ? System.Windows.Media.Brushes.Red 
                        : System.Windows.Media.Brushes.Black;
                }

                // Update tasks list
                tasks.Clear();
                foreach (var task in plan.ResearchTasks)
                {
                    tasks.Add(task);
                }

                // Update progress bar
                if (plan.ResearchTasks.Count > 0)
                {
                    var progress = (double)plan.CompletedTaskCount / plan.ResearchTasks.Count * 100;
                    progressBar.Value = progress;
                }

                // Update status text
                var duration = plan.Duration?.ToString(@"mm\:ss") ?? "0:00";
                statusText.Text = $"{plan.CompletedTaskCount}/{plan.ResearchTasks.Count} complete";
                
                if (plan.FailedTaskCount > 0)
                {
                    statusText.Text += $" ({plan.FailedTaskCount} failed)";
                }
                
                statusText.Text += $" • {duration}";

                // Hide panel when complete
                if (plan.Phase == AgentExecutionPhase.Complete || plan.Phase == AgentExecutionPhase.Failed)
                {
                    // Keep visible for a few seconds, then fade out
                    var timer = new System.Windows.Threading.DispatcherTimer
                    {
                        Interval = TimeSpan.FromSeconds(5)
                    };
                    timer.Tick += (s, e) =>
                    {
                        this.Visibility = Visibility.Collapsed;
                        timer.Stop();
                    };
                    timer.Start();
                }
            });
        }

        public void Clear()
        {
            Dispatcher.Invoke(() =>
            {
                tasks.Clear();
                progressMessage.Text = "Ready";
                statusText.Text = "Ready";
                progressBar.Value = 0;
                this.Visibility = Visibility.Collapsed;
            });
        }

        private void CollapseButton_Click(object sender, RoutedEventArgs e)
        {
            if (isCollapsed)
            {
                // Expand
                contentPanel.Visibility = Visibility.Visible;
                collapseButton.Content = "▲";
                this.Height = expandedHeight;
                isCollapsed = false;
            }
            else
            {
                // Collapse
                expandedHeight = this.ActualHeight;
                contentPanel.Visibility = Visibility.Collapsed;
                collapseButton.Content = "▼";
                this.Height = double.NaN; // Auto height
                isCollapsed = true;
            }
        }
    }
}

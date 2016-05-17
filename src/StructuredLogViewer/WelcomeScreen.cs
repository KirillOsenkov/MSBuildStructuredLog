using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using StructuredLogViewer;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class WelcomeScreen
    {
        private IEnumerable<string> recentLogs;
        public IEnumerable<string> RecentLogs => recentLogs ?? (recentLogs = SettingsService.GetRecentLogFiles());

        private IEnumerable<string> recentProjects;
        public IEnumerable<string> RecentProjects => recentProjects ?? (recentProjects = SettingsService.GetRecentProjects());

        public bool ShowRecentLogs => RecentLogs.Any();
        public bool ShowRecentProjects => RecentProjects.Any();

        public event Action<string> RecentLogSelected;
        public event Action<string> RecentProjectSelected;
        public event Action OpenProjectRequested;
        public event Action OpenLogFileRequested;

        public string Version
        {
            get
            {
                var version = Assembly.GetEntryAssembly().GetName().Version;
                return $"Version {version.Major}.{version.Minor}.{version.Build}";
            }
        }

        public string SelectedLog
        {
            set
            {
                if (value == null)
                {
                    return;
                }

                if (!File.Exists(value))
                {
                    MessageBox.Show($"File {value} doesn't exist.");
                    return;
                }

                RecentLogSelected?.Invoke(value);
            }
        }

        public string SelectedProject
        {
            set
            {
                if (value == null)
                {
                    return;
                }

                if (!File.Exists(value))
                {
                    MessageBox.Show($"Project {value} doesn't exist.");
                    return;
                }

                RecentProjectSelected?.Invoke(value);
            }
        }

        private ICommand openProjectCommand;
        public ICommand OpenProjectCommand => openProjectCommand ?? (openProjectCommand = new Command(OpenProject));
        private void OpenProject() => OpenProjectRequested?.Invoke();

        private ICommand openLogFileCommand;
        public ICommand OpenLogFileCommand => openLogFileCommand ?? (openLogFileCommand = new Command(OpenLogFile));
        private void OpenLogFile() => OpenLogFileRequested?.Invoke();
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Input;
using StructuredLogViewer;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class WelcomeScreen : ObservableObject
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

        private string version = GetVersion();
        public string Version
        {
            get => version;
            set => SetField(ref version, value);
        }

        private string message;
        public string Message
        {
            get => message;
            set => SetField(ref message, value);
        }

        private static string GetVersion()
        {
            return $"Version {ThisAssembly.AssemblyInformationalVersion}";
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
                    DialogService.ShowMessageBox($"File {value} doesn't exist.");
                    SettingsService.RemoveRecentLogFile(value);
                    recentLogs = null;
                    RaisePropertyChanged(nameof(RecentLogs));
                    RaisePropertyChanged(nameof(ShowRecentLogs));
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
                    DialogService.ShowMessageBox($"Project {value} doesn't exist.");
                    SettingsService.RemoveRecentProject(value);
                    recentProjects = null;
                    RaisePropertyChanged(nameof(RecentProjects));
                    RaisePropertyChanged(nameof(ShowRecentProjects));
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

        public bool EnableVirtualization
        {
            get => SettingsService.EnableTreeViewVirtualization;
            set => SettingsService.EnableTreeViewVirtualization = value;
        }

        public bool ParentAllTargetsUnderProject
        {
            get => SettingsService.ParentAllTargetsUnderProject;
            set => SettingsService.ParentAllTargetsUnderProject = value;
        }

        public bool MarkResultsInTree
        {
            get => SettingsService.MarkResultsInTree;
            set => SettingsService.MarkResultsInTree = value;
        }

        public bool UseDarkTheme
        {
            get => SettingsService.UseDarkTheme;
            set
            {
                SettingsService.UseDarkTheme = value;
                RaisePropertyChanged();
            }
        }
    }
}

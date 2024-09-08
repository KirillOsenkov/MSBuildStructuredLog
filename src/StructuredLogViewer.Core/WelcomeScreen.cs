﻿using System;
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
        public IEnumerable<string> RecentLogs => recentLogs ??= SettingsService.GetRecentLogFiles();

        private IEnumerable<string> recentProjects;
        public IEnumerable<string> RecentProjects => recentProjects ??= SettingsService.GetRecentProjects();

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

        private string selectedLog;
        public string SelectedLog
        {
            get => selectedLog;

            set
            {
                if (value == null)
                {
                    return;
                }

                selectedLog = value;

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

        private string selectedProject;
        public string SelectedProject
        {
            get => selectedProject;

            set
            {
                if (value == null)
                {
                    return;
                }

                selectedProject = value;

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
        public ICommand OpenProjectCommand => openProjectCommand ??= new Command(OpenProject);
        private void OpenProject() => OpenProjectRequested?.Invoke();

        private ICommand openLogFileCommand;
        public ICommand OpenLogFileCommand => openLogFileCommand ??= new Command(OpenLogFile);
        private void OpenLogFile() => OpenLogFileRequested?.Invoke();

        public bool EnableVirtualization
        {
            get => SettingsService.EnableTreeViewVirtualization;
            set
            {
                SettingsService.EnableTreeViewVirtualization = value;
                RaisePropertyChanged();
            }
        }

        public bool MarkResultsInTree
        {
            get => SettingsService.MarkResultsInTree;
            set
            {
                SettingsService.MarkResultsInTree = value;
                RaisePropertyChanged();
            }
        }

        public bool ShowConfigurationAndPlatform
        {
            get => SettingsService.ShowConfigurationAndPlatform;
            set
            {
                SettingsService.ShowConfigurationAndPlatform = value;
                RaisePropertyChanged();
            }
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

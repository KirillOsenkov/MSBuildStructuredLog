﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Build.Logging.StructuredLogger;
using Microsoft.Win32;
using Squirrel;
using StructuredLogViewer.Controls;

namespace StructuredLogViewer
{
    public partial class MainWindow : Window
    {
        private string logFilePath;
        private string projectFilePath;
        private BuildControl currentBuild;

        public const string DefaultTitle = "MSBuild Structured Log Viewer";

        public MainWindow()
        {
            InitializeComponent();
            var uri = new Uri("StructuredLogViewer;component/themes/Generic.xaml", UriKind.Relative);
            var generic = (ResourceDictionary)Application.LoadComponent(uri);
            Application.Current.Resources.MergedDictionaries.Add(generic);

            Loaded += MainWindow_Loaded;
            Drop += MainWindow_Drop;
        }

        private const string ClipboardFileFormat = "FileDrop";

        private void MainWindow_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(ClipboardFileFormat))
            {
                var data = e.Data.GetData(ClipboardFileFormat) as string[];
                if (data != null && data.Length == 1)
                {
                    var filePath = data[0];
                    OpenFile(filePath);
                }
            }
        }

        private bool TryOpenFromClipboard()
        {
            var text = GetSingleFileFromClipboard();
            if (string.IsNullOrEmpty(text) || text.Length > 260)
            {
                return false;
            }

            text = text.TrimStart('"').TrimEnd('"');

            // only open a file from clipboard if it's not listed in the recent files
            var recentFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            recentFiles.UnionWith(SettingsService.GetRecentLogFiles());
            recentFiles.UnionWith(SettingsService.GetRecentProjects());

            if (!recentFiles.Contains(text) && OpenFile(text))
            {
                return true;
            }

            return false;
        }

        private string GetSingleFileFromClipboard()
        {
            if (Clipboard.ContainsFileDropList())
            {
                var fileDropList = Clipboard.GetFileDropList();
                if (fileDropList.Count == 1)
                {
                    return fileDropList[0];
                }
            }

            return Clipboard.GetText();
        }

        private void DisplayWelcomeScreen(string message = "")
        {
            this.projectFilePath = null;
            this.logFilePath = null;
            this.currentBuild = null;
            Title = DefaultTitle;

            var welcomeScreen = new WelcomeScreen();
            welcomeScreen.Message = message;
            SetContent(welcomeScreen);
            welcomeScreen.RecentLogSelected += log => OpenLogFile(log);
            welcomeScreen.RecentProjectSelected += project => BuildProject(project);
            welcomeScreen.OpenProjectRequested += () => OpenProjectOrSolution();
            welcomeScreen.OpenLogFileRequested += () => OpenLogFile();
            UpdateRecentItemsMenu();
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var args = Environment.GetCommandLineArgs();
                if (args.Length > 1 && HandleArguments(args))
                {
                    return;
                }

                if (TryOpenFromClipboard())
                {
                    return;
                }

                DisplayWelcomeScreen();

                // only check for updates if there were no command-line arguments and debugger not attached
                if (Debugger.IsAttached || SettingsService.DisableUpdates)
                {
                    return;
                }

                using (var updateManager = await UpdateManager.GitHubUpdateManager("https://github.com/KirillOsenkov/MSBuildStructuredLog"))
                {
                    var result = await updateManager.UpdateApp();
                    var currentVersion = updateManager.CurrentlyInstalledVersion();
                    string message;
                    if (result == null || result.Version == currentVersion)
                    {
                        message = "You have the latest version: " + currentVersion.ToString();
                        FileAssociations.EnsureAssociationsSet();
                    }
                    else if (result.Version > currentVersion)
                    {
                        message = "After restarting the app you will be on version " + result.Version.ToString();
                    }
                    else
                    {
                        message = $"You're running a version ({currentVersion.ToString()}) which is newer than the latest stable ({result.Version}).";
                    }

                    var welcomeScreen = mainContent.Content as WelcomeScreen;
                    if (welcomeScreen != null)
                    {
                        welcomeScreen.Version = message;
                    }
                }
            }
            catch (Exception ex)
            {
                var welcomeScreen = mainContent.Content as WelcomeScreen;
                if (welcomeScreen != null)
                {
                    var text = ex.ToString();
                    if (text.Contains("Update.exe not found"))
                    {
                        text = "Update.exe not found; app will not update.";
                    }

                    welcomeScreen.Message = text;
                }
            }
        }

        private bool HandleArguments(string[] args)
        {
            if (args.Length > 2)
            {
                DisplayWelcomeScreen("Structured Log Viewer can only accept a single command-line argument: a full path to an existing log file or MSBuild project/solution.");
                return true;
            }

            var argument = args[1];
            if (argument.StartsWith("--"))
            {
                // we don't do anything about the potential "--squirrel-firstrun" argument
                return false;
            }

            var filePath = args[1];
            if (!File.Exists(filePath))
            {
                DisplayWelcomeScreen($"File {filePath} not found.");
                return true;
            }

            if (OpenFile(filePath))
            {
                return true;
            }

            DisplayWelcomeScreen($"File extension not supported: {filePath}");
            return true;
        }

        public bool OpenFile(string filePath)
        {
            if (filePath.IndexOfAny(Path.GetInvalidPathChars()) != -1)
            {
                return false;
            }

            if (!File.Exists(filePath))
            {
                return false;
            }

            if (filePath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) ||
                filePath.EndsWith(".buildlog", StringComparison.OrdinalIgnoreCase) ||
                filePath.EndsWith(".binlog", StringComparison.OrdinalIgnoreCase))
            {
                OpenLogFile(filePath);
                return true;
            }

            if (filePath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) ||
                filePath.EndsWith("proj", StringComparison.OrdinalIgnoreCase))
            {
                BuildProject(filePath);
                return true;
            }

            return false;
        }

        private void UpdateRecentItemsMenu(WelcomeScreen welcomeScreen = null)
        {
            welcomeScreen = welcomeScreen ?? new WelcomeScreen();
            if (welcomeScreen.ShowRecentProjects)
            {
                RecentProjectsMenu.Items.Clear();
                RecentProjectsMenu.Visibility = Visibility.Visible;
                RecentItemsSeparator.Visibility = Visibility.Visible;
                foreach (var recentProjectFile in welcomeScreen.RecentProjects)
                {
                    var menuItem = new MenuItem { Header = recentProjectFile };
                    menuItem.Click += RecentProjectClick;
                    RecentProjectsMenu.Items.Add(menuItem);
                }
            }

            if (welcomeScreen.ShowRecentLogs)
            {
                RecentLogsMenu.Items.Clear();
                RecentLogsMenu.Visibility = Visibility.Visible;
                RecentItemsSeparator.Visibility = Visibility.Visible;
                foreach (var recentLog in welcomeScreen.RecentLogs)
                {
                    var menuItem = new MenuItem { Header = recentLog };
                    menuItem.Click += RecentLogFileClick;
                    RecentLogsMenu.Items.Add(menuItem);
                }
            }
        }

        private BuildControl CurrentBuildControl => mainContent.Content as BuildControl;

        private void SetContent(object content)
        {
            mainContent.Content = content;
            if (content == null)
            {
                logFilePath = null;
                projectFilePath = null;
                currentBuild = null;
            }

            if (content is BuildControl)
            {
                ReloadMenu.Visibility = logFilePath != null ? Visibility.Visible : Visibility.Collapsed;
                SaveAsMenu.Visibility = Visibility.Visible;
            }
            else
            {
                ReloadMenu.Visibility = Visibility.Collapsed;
                SaveAsMenu.Visibility = Visibility.Collapsed;
            }
        }

        private void RecentProjectClick(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            BuildProject(Convert.ToString(menuItem.Header));
        }

        private void RecentLogFileClick(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            OpenLogFile(Convert.ToString(menuItem.Header));
        }

        private async void OpenLogFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return;
            }

            DisplayBuild(null);
            this.logFilePath = filePath;
            SettingsService.AddRecentLogFile(filePath);
            UpdateRecentItemsMenu();
            Title = filePath + " - " + DefaultTitle;

            var progress = new BuildProgress();
            progress.ProgressText = "Opening " + filePath + "...";
            SetContent(progress);

            bool shouldAnalyze = true;

            Build build = await System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    return Serialization.Read(filePath);
                }
                catch (Exception ex)
                {
                    ex = ExceptionHandler.Unwrap(ex);
                    shouldAnalyze = false;
                    return GetErrorBuild(filePath, ex.ToString());
                }
            });

            if (build == null)
            {
                build = GetErrorBuild(filePath, "");
                shouldAnalyze = false;
            }

            if (shouldAnalyze)
            {
                progress.ProgressText = "Analyzing " + filePath + "...";
                await System.Threading.Tasks.Task.Run(() => BuildAnalyzer.AnalyzeBuild(build));
            }

            progress.ProgressText = "Rendering tree...";
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Loaded); // let the progress message be rendered before we block the UI again

            DisplayBuild(build);
        }

        private static Build GetErrorBuild(string filePath, string message)
        {
            var build = new Build() { Succeeded = false };
            build.AddChild(new Error() { Text = "Error when opening file: " + filePath });
            build.AddChild(new Error() { Text = message });
            return build;
        }

        private void BuildProject(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return;
            }

            DisplayBuild(null);
            this.projectFilePath = filePath;
            SettingsService.AddRecentProject(projectFilePath);
            UpdateRecentItemsMenu();
            Title = projectFilePath + " - " + DefaultTitle;

            string customArguments = SettingsService.GetCustomArguments(filePath);
            var parametersScreen = new BuildParametersScreen();
            parametersScreen.BrowseForMSBuild += BrowseForMSBuildExe;
            parametersScreen.PrefixArguments = HostedBuild.QuoteIfNeeded(filePath);
            parametersScreen.MSBuildArguments = customArguments;
            parametersScreen.PostfixArguments = HostedBuild.GetPostfixArguments();
            parametersScreen.BuildRequested += () =>
            {
                parametersScreen.SaveSelectedMSBuild();
                SettingsService.SaveCustomArguments(filePath, parametersScreen.MSBuildArguments);
                BuildCore(projectFilePath, parametersScreen.MSBuildArguments);
            };
            parametersScreen.CancelRequested += () =>
            {
                DisplayWelcomeScreen();
            };
            SetContent(parametersScreen);
        }

        private async void BuildCore(string projectFilePath, string customArguments)
        {
            var progress = new BuildProgress();
            progress.ProgressText = $"Building {projectFilePath}...";
            SetContent(progress);
            var buildHost = new HostedBuild(projectFilePath, customArguments);
            Build result = await buildHost.BuildAndGetResult(progress);
            progress.ProgressText = "Analyzing build...";
            await System.Threading.Tasks.Task.Run(() => BuildAnalyzer.AnalyzeBuild(result));
            DisplayBuild(result);
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            OpenLogFile();
        }

        private void Build_Click(object sender, RoutedEventArgs e)
        {
            OpenProjectOrSolution();
        }

        private void OpenLogFile()
        {
            var openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = Serialization.OpenFileDialogFilter;
            openFileDialog.Title = "Open a build log file";
            openFileDialog.CheckFileExists = true;
            var result = openFileDialog.ShowDialog(this);
            if (result != true)
            {
                return;
            }

            OpenLogFile(openFileDialog.FileName);
        }

        private void OpenProjectOrSolution()
        {
            var openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "MSBuild projects and solutions (*.sln;*.*proj)|*.sln;*.*proj";
            openFileDialog.Title = "Open a solution or project";
            openFileDialog.CheckFileExists = true;
            var result = openFileDialog.ShowDialog(this);
            if (result != true)
            {
                return;
            }

            BuildProject(openFileDialog.FileName);
        }

        private void RebuildProjectOrSolution()
        {
            if (!string.IsNullOrEmpty(projectFilePath))
            {
                var args = SettingsService.GetCustomArguments(projectFilePath);
                BuildCore(projectFilePath, args);
            }
        }

        private void DisplayBuild(Build build)
        {
            currentBuild = build != null ? new BuildControl(build, logFilePath) : null;
            SetContent(currentBuild);

            GC.Collect();
        }

        private void Reload()
        {
            OpenLogFile(logFilePath);
        }

        private void SaveAs()
        {
            if (currentBuild != null)
            {
                var saveFileDialog = new SaveFileDialog();
                saveFileDialog.Filter = Serialization.FileDialogFilter;
                saveFileDialog.Title = "Save log file as";
                saveFileDialog.CheckFileExists = false;
                saveFileDialog.OverwritePrompt = true;
                saveFileDialog.ValidateNames = true;
                var result = saveFileDialog.ShowDialog(this);
                if (result != true)
                {
                    return;
                }

                logFilePath = saveFileDialog.FileName;
                System.Threading.Tasks.Task.Run(() =>
                {
                    Serialization.Write(currentBuild.Build, logFilePath);
                    Dispatcher.InvokeAsync(() =>
                    {
                        currentBuild.UpdateBreadcrumb(new Message { Text = $"Saved {logFilePath}" });
                    });
                    SettingsService.AddRecentLogFile(logFilePath);
                });
            }
        }

        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F5)
            {
                Reload();
            }
            else if (e.Key == Key.F6 && e.KeyboardDevice.Modifiers == ModifierKeys.Shift)
            {
                RebuildProjectOrSolution();
            }
            else if (e.Key == Key.F6)
            {
                OpenProjectOrSolution();
            }
            else if (e.Key == Key.O && e.KeyboardDevice.Modifiers == ModifierKeys.Control)
            {
                OpenLogFile();
            }
            else if (e.Key == Key.F && e.KeyboardDevice.Modifiers == ModifierKeys.Control)
            {
                FocusSearch();
            }
            else if (e.Key == Key.C && e.KeyboardDevice.Modifiers == ModifierKeys.Control)
            {
                var content = mainContent.Content as BuildProgress;
                if (content != null)
                {
                    Clipboard.SetText(content.MSBuildCommandLine);
                }
            }
            else if (e.Key == Key.S && e.KeyboardDevice.Modifiers == ModifierKeys.Control)
            {
                SaveAs();
            }
        }

        private void FocusSearch()
        {
            CurrentBuildControl?.FocusSearch();
        }

        private void Reload_Click(object sender, RoutedEventArgs e)
        {
            Reload();
        }

        private void Rebuild_Click(object sender, RoutedEventArgs e)
        {
            RebuildProjectOrSolution();
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            if (currentBuild != null)
            {
                currentBuild.CopySubtree();
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (currentBuild != null)
            {
                currentBuild.Delete();
            }
        }

        private void SetMSBuild_Click(object sender, RoutedEventArgs e)
        {
            BrowseForMSBuildExe();

            var buildParametersScreen = mainContent.Content as BuildParametersScreen;
            if (buildParametersScreen != null)
            {
                buildParametersScreen.UpdateMSBuildLocations();
            }
        }

        private static void BrowseForMSBuildExe()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "MSBuild.exe|MSBuild.exe",
                Title = "Select MSBuild.exe location",
                CheckFileExists = true
            };

            if (openFileDialog.ShowDialog() != true)
            {
                return;
            }

            SettingsService.AddRecentMSBuildLocation(openFileDialog.FileName);
        }

        private void SaveAs_Click(object sender, RoutedEventArgs e)
        {
            SaveAs();
        }

        private void HelpLink_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://github.com/KirillOsenkov/MSBuildStructuredLog");
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void StartPage_Click(object sender, RoutedEventArgs e)
        {
            DisplayWelcomeScreen();
        }
    }
}

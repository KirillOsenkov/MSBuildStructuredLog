using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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
        private string lastSearchText;
        private double scale = 1.0;

        public const string DefaultTitle = "MSBuild Structured Log Viewer";

        public string VersionMessage { get; set; } = "Locally built version";

        public MainWindow()
        {
            InitializeComponent();
            var uri = new Uri("StructuredLogViewer;component/themes/Generic.xaml", UriKind.Relative);
            var generic = new ResourceDictionary { Source = uri };
            Application.Current.Resources.MergedDictionaries.Add(generic);

            Loaded += MainWindow_Loaded;
            Drop += MainWindow_Drop;

            ThemeManager.UseDarkTheme = SettingsService.UseDarkTheme;
            ThemeManager.UpdateTheme();
        }

        protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                const double min = 1;
                const double max = 4;
                const double step = 0.1;

                if (e.Delta > 0 && scale < max)
                {
                    scale = Math.Min(max, scale + step);
                    UpdateZoomLevel();
                }
                else if (e.Delta < 0 && scale > min)
                {
                    scale = Math.Max(min, scale - step);
                    UpdateZoomLevel();
                }

                e.Handled = true;
                return;
            }

            base.OnPreviewMouseWheel(e);
        }

        private void UpdateZoomLevel(double zoom = double.NaN)
        {
            if (double.IsNaN(zoom))
            {
                zoom = scale;
            }

            scaleTransform.ScaleX = scaleTransform.ScaleY = zoom;
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
            welcomeScreen.PropertyChanged += WelcomeScreen_PropertyChanged;
            UpdateRecentItemsMenu();
        }

        private void WelcomeScreen_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(WelcomeScreen.UseDarkTheme))
            {
                ThemeManager.UseDarkTheme = SettingsService.UseDarkTheme;
                ThemeManager.UpdateTheme();
            }
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!HandleArguments() && !TryOpenFromClipboard())
                {
                    DisplayWelcomeScreen();
                }

                await UpdateApplicationAsync();
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

                    if (ex is HttpRequestException)
                    {
                        text = "Unable to update the app (no internet connection?)";
                    }

                    welcomeScreen.Message = text;
                }
            }
        }

        private async System.Threading.Tasks.Task UpdateApplicationAsync()
        {
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
                    var versionText = result.Version.ToString();
                    message = "After restarting the app you will be on version " + versionText;
                    FileAssociations.EnsureAssociationsSet(versionText);
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

                VersionMessage = message;
            }
        }

        private bool HandleArguments()
        {
            var args = Environment.GetCommandLineArgs();

            if (args.Length <= 1)
            {
                return false;
            }

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

            filePath = Path.GetFullPath(filePath);

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
            // We save build control to allow to bring back states to new one
            if (mainContent.Content is BuildControl current)
            {
                lastSearchText = current.searchLogControl.SearchText;
            }

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

            // If we had text inside search log control bring it back
            if (mainContent.Content is BuildControl currentContent && !string.IsNullOrEmpty(lastSearchText))
            {
                currentContent.searchLogControl.searchTextBox.SelectedText = lastSearchText;
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
            progress.Progress.Updated += update =>
            {
                Dispatcher.InvokeAsync(() =>
                {
                    progress.Value = update.Ratio;
                }, DispatcherPriority.Background);
            };
            progress.ProgressText = "Opening " + filePath + "...";
            SetContent(progress);

            bool shouldAnalyze = true;

            var stopwatch = Stopwatch.StartNew();

            Build build = await System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    return Serialization.Read(filePath, progress.Progress);
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

            var elapsed = stopwatch.Elapsed;
            if (currentBuild != null)
            {
                currentBuild.UpdateBreadcrumb($"Load time: {elapsed}");
            }
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
            parametersScreen.PrefixArguments = filePath.QuoteIfNeeded();
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
                parametersScreen.SaveSelectedMSBuild();
                DisplayWelcomeScreen();
            };
            SetContent(parametersScreen);
        }

        private async void BuildCore(string projectFilePath, string customArguments, string searchText = null)
        {
            var progress = new BuildProgress { IsIndeterminate = true };
            progress.ProgressText = $"Building {projectFilePath}...";
            SetContent(progress);
            var buildHost = new HostedBuild(projectFilePath, customArguments);
            Build result = await buildHost.BuildAndGetResult(progress);
            progress.ProgressText = "Analyzing build...";
            await System.Threading.Tasks.Task.Run(() => BuildAnalyzer.AnalyzeBuild(result));
            DisplayBuild(result);
            if (searchText != null && CurrentBuildControl != null)
            {
                CurrentBuildControl.InitialSearchText = searchText;
            }
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
            openFileDialog.Filter = "MSBuild projects and solutions (*.sln;*.*proj)|*.sln;*.*proj|All files (*.*)|*";
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

                string searchText = null;
                if (CurrentBuildControl != null)
                {
                    searchText = CurrentBuildControl.searchLogControl.SearchText;
                }

                BuildCore(projectFilePath, args, searchText);
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
                string currentFilePath = currentBuild.LogFilePath;

                var saveFileDialog = new SaveFileDialog();
                saveFileDialog.Filter = currentFilePath != null && currentFilePath.EndsWith(".binlog", StringComparison.OrdinalIgnoreCase) ? Serialization.BinlogFileDialogFilter : Serialization.FileDialogFilter;
                saveFileDialog.Title = "Save log file as";
                saveFileDialog.CheckFileExists = false;
                saveFileDialog.OverwritePrompt = true;
                saveFileDialog.ValidateNames = true;
                var result = saveFileDialog.ShowDialog(this);
                if (result != true)
                {
                    return;
                }

                string newFilePath = saveFileDialog.FileName;
                if (string.IsNullOrEmpty(newFilePath) || string.Equals(currentFilePath, newFilePath, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                logFilePath = saveFileDialog.FileName;

                lock (inProgressOperationLock)
                {
                    InProgressTask = InProgressTask.ContinueWith(t =>
                    {
                        try
                        {
                            if (logFilePath.EndsWith(".binlog", StringComparison.OrdinalIgnoreCase))
                            {
                                File.Copy(currentFilePath, logFilePath, overwrite: true);
                            }
                            else
                            {
                                Serialization.Write(currentBuild.Build, logFilePath);
                            }

                            currentBuild.Build.LogFilePath = logFilePath;

                            Dispatcher.InvokeAsync(() =>
                            {
                                currentBuild.UpdateBreadcrumb(new Message { Text = $"Saved {logFilePath}" });
                            });
                            SettingsService.AddRecentLogFile(logFilePath);
                        }
                        catch
                        {
                        }
                    });
                }
            }
        }

        private object inProgressOperationLock = new object();
        public System.Threading.Tasks.Task InProgressTask = System.Threading.Tasks.Task.CompletedTask;

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
            else if (e.Key == Key.D0 && e.KeyboardDevice.Modifiers == ModifierKeys.Control)
            {
                UpdateZoomLevel(1.0);
                e.Handled = true;
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
            MSBuildLocator.BrowseForMSBuildExe();

            if (mainContent.Content is BuildParametersScreen buildParametersScreen)
            {
                buildParametersScreen.UpdateMSBuildLocations();
            }
        }

        private void Stats_Click(object sender, RoutedEventArgs e)
        {
            if (currentBuild != null)
            {
                currentBuild.DisplayStats();
            }
        }

        private void SaveAs_Click(object sender, RoutedEventArgs e)
        {
            SaveAs();
        }

        private void HelpLink_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://github.com/KirillOsenkov/MSBuildStructuredLog") { UseShellExecute = true });
        }

        private void HelpLink2_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("http://msbuildlog.com") { UseShellExecute = true });
        }

        private void HelpAbout_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(VersionMessage);
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

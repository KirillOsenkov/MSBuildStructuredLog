using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Build.Logging.StructuredLogger;
using Microsoft.Win32;
using Squirrel;
using StructuredLogger.Utils;
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

            SourceInitialized += MainWindow_SourceInitialized;
            Loaded += MainWindow_Loaded;
            Drop += MainWindow_Drop;
            Closing += MainWindow_Closing;

            ThemeManager.UseDarkTheme = SettingsService.UseDarkTheme;
            ThemeManager.UpdateTheme();
        }

        private void MainWindow_SourceInitialized(object sender, EventArgs e)
        {
            WindowPosition.RestoreWindowPosition(this, SettingsService.WindowPosition);
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            var windowPosition = WindowPosition.GetWindowPosition(this);
            SettingsService.WindowPosition = windowPosition;
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
            // Dispose of current build.
            DisplayBuild(null);

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
                    BinaryLogger.IsNewerVersionAvailable = true;
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

            RecentItemsSeparator.Visibility = Visibility.Collapsed;
            RecentProjectsMenu.Items.Clear();
            if (welcomeScreen.ShowRecentProjects)
            {
                RecentProjectsMenu.Visibility = Visibility.Visible;
                RecentItemsSeparator.Visibility = Visibility.Visible;
                foreach (var recentProjectFile in welcomeScreen.RecentProjects)
                {
                    var menuItem = new MenuItem { Header = recentProjectFile };
                    menuItem.Click += RecentProjectClick;
                    RecentProjectsMenu.Items.Add(menuItem);
                }

                var clearHistory = new MenuItem { Header = "Clear Recent Projects" };
                clearHistory.Click += ClearRecentProjects;
                RecentProjectsMenu.Items.Add(clearHistory);
            }
            else
            {
                RecentProjectsMenu.Visibility = Visibility.Collapsed;
            }

            RecentLogsMenu.Items.Clear();
            if (welcomeScreen.ShowRecentLogs)
            {
                RecentLogsMenu.Visibility = Visibility.Visible;
                RecentItemsSeparator.Visibility = Visibility.Visible;
                foreach (var recentLog in welcomeScreen.RecentLogs)
                {
                    var menuItem = new MenuItem { Header = recentLog };
                    menuItem.Click += RecentLogFileClick;
                    RecentLogsMenu.Items.Add(menuItem);
                }

                var clearHistory = new MenuItem { Header = "Clear Recent Logs" };
                clearHistory.Click += ClearRecentLogFiles;
                RecentLogsMenu.Items.Add(clearHistory);
            }
            else
            {
                RecentLogsMenu.Visibility = Visibility.Collapsed;
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
                RedactSecretsMenu.Visibility = Visibility.Visible;
            }
            else
            {
                ReloadMenu.Visibility = Visibility.Collapsed;
                SaveAsMenu.Visibility = Visibility.Collapsed;
                RedactSecretsMenu.Visibility = Visibility.Collapsed;
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

        private void ClearRecentProjects(object sender, RoutedEventArgs e)
        {
            SettingsService.RemoveAllRecentProjects();

            // Re-draw the welcome screen if it is active
            // or only update the menu
            if (this.mainContent.Content is WelcomeScreen)
            {
                DisplayWelcomeScreen();
            }
            else
            {
                UpdateRecentItemsMenu();
            }
        }

        private void ClearRecentLogFiles(object sender, RoutedEventArgs e)
        {
            SettingsService.RemoveAllRecentLogFiles();

            // Re-draw the welcome screen if it is active
            // or only update the menu
            if (this.mainContent.Content is WelcomeScreen)
            {
                DisplayWelcomeScreen();
            }
            else
            {
                UpdateRecentItemsMenu();
            }
        }

        private async void OpenLogFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return;
            }

            long allocatedBefore = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;

            DisplayBuild(null);

            this.logFilePath = filePath;
            SettingsService.AddRecentLogFile(filePath);
            Build.IgnoreEmbeddedFiles = SettingsService.IgnoreEmbeddedFiles;
            UpdateRecentItemsMenu();
            Title = filePath + " - " + DefaultTitle;

            var progress = new BuildProgress();
            progress.Progress.Updated += update =>
            {
                Dispatcher.InvokeAsync(() =>
                {
                    progress.Value = update.Ratio;
                    string bufferText = null;
                    if (update.BufferLength > 0)
                    {
                        bufferText = $"Buffer length: {update.BufferLength:n0}";
                    }

                    progress.BufferText = bufferText;
                }, DispatcherPriority.Background);
            };
            progress.ProgressText = "Opening " + filePath + "...";
            SetContent(progress);

            bool shouldAnalyze = true;

            var stopwatch = Stopwatch.StartNew();
            var totalStopwatch = Stopwatch.StartNew();

            Build build = await System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    return Serialization.Read(filePath, progress.Progress, ReaderSettings.Default);
                }
                catch (Exception ex)
                {
                    ex = ExceptionHandler.Unwrap(ex);
                    shouldAnalyze = false;
                    return GetErrorBuild(filePath, ex.ToString());
                }
            });

            var openTime = stopwatch.Elapsed;
            stopwatch.Restart();

            if (build == null)
            {
                build = GetErrorBuild(filePath, "");
                shouldAnalyze = false;
            }

            if (shouldAnalyze)
            {
                progress.ProgressText = "Analyzing " + filePath + "...";
                await QueueAnalyzeBuild(build);
            }

            var analyzingTime = stopwatch.Elapsed;

            progress.ProgressText = "Indexing...";
            stopwatch.Restart();
            await System.Threading.Tasks.Task.Run(() => build.SearchIndex = new SearchIndex(build));
            var indexingTime = stopwatch.Elapsed;

            progress.ProgressText = "Reading embedded files...";
            TimeSpan embeddedFilesTime = TimeSpan.Zero;
            await System.Threading.Tasks.Task.Run(() =>
            {
                stopwatch.Restart();
                var sourceFiles = build.SourceFiles;
                if (sourceFiles != null &&
                    sourceFiles.FirstOrDefault(s =>
                        s.FullPath.EndsWith("project.assets.json", StringComparison.OrdinalIgnoreCase)) != null)
                {
                    AddNuGetNode(build);
                }

                embeddedFilesTime = stopwatch.Elapsed;
            });

            progress.ProgressText = "Rendering tree...";
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Loaded); // let the progress message be rendered before we block the UI again

            DisplayBuild(build);

            if (currentBuild != null)
            {
                long allocatedAfter = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
                long allocated = allocatedAfter - allocatedBefore;
                string readingFilesText = TextUtilities.DisplayDuration(embeddedFilesTime);

                string text = $"Total opening time: {TextUtilities.DisplayDuration(totalStopwatch.Elapsed)}";

                text += $", Loading: {TextUtilities.DisplayDuration(openTime)}";
                text += $", Analyzing: {TextUtilities.DisplayDuration(analyzingTime)}";
                text += $", Indexing: {TextUtilities.DisplayDuration(indexingTime)}";
                if (!string.IsNullOrEmpty(readingFilesText))
                {
                    text += $", Reading files: {readingFilesText}";
                }

                text += $", Allocated: {allocated:n0} bytes";

                if (currentBuild.Build.SearchIndex is { } index)
                {
                    text += $", Nodes: {index.NodeCount:n0}";
                    text += $", Strings: {index.Strings.Length:n0}";
                }

                currentBuild.UpdateBreadcrumb(text);
            }
        }

        private void AddNuGetNode(Build build)
        {
            var nuget = new Package { Name = "NuGet" };
            var note = new Note { Text = @"This binlog contains project.assets.json files.
You can search for NuGet packages (by name or version), dependencies (direct or transitive)
and files coming from NuGet packages:

List MyProject.csproj dependencies:
    $nuget project(MyProject.csproj)

Search for Package.Name in both dependencies and resolved packages:
    $nuget project(MyProject.csproj) Package.Name

Search for a file coming from a NuGet package:
    $nuget project(MyProject.csproj) File.dll

Search for a specific version or version range:
    $nuget project(.csproj) 13.0.3

Use project(.) or project(.csproj) to search all projects (slow)." };
            nuget.AddChild(note);
            build.AddChild(nuget);
        }

        private async System.Threading.Tasks.Task QueueAnalyzeBuild(Build build)
        {
            await System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    BuildAnalyzer.AnalyzeBuild(build);
                    build.SearchExtensions.Add(new NuGetSearch(build));
                }
                catch (Exception ex)
                {
                    DialogService.ShowMessageBox(
                    "Error while analyzing build. Sorry about that. Please Ctrl+C to copy this text and file an issue on https://github.com/KirillOsenkov/MSBuildStructuredLog/issues/new \r\n" + ex.ToString());
                }
            });
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
            await QueueAnalyzeBuild(result);
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
            if (currentBuild != null && currentBuild is BuildControl)
            {
                currentBuild.Dispose();
            }

            currentBuild = build != null ? new BuildControl(build, logFilePath) : null;
            SetContent(currentBuild);

            if (currentBuild == null)
            {
                ProjectOrEvaluationHelper.ClearCache();
            }

            GC.Collect();
        }

        private void Reload()
        {
            OpenLogFile(logFilePath);
        }

        private async System.Threading.Tasks.Task RedactSecrets()
        {
            RedactInputControl redactInputControl = new RedactInputControl(GetSaveAsDestination);
            redactInputControl.Owner = this;

            if (redactInputControl.ShowDialog() != true)
            {
                return;
            }

            List<string> stringsToRedact =
                new(redactInputControl.SecretsBlock?
                        .Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.Trim())
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                    ?? new string[] { });

            if (
                !stringsToRedact.Any() &&
                !redactInputControl.RedactUsername &&
                !redactInputControl.RedactCommonCredentials)
            {
                MessageBox.Show("No secrets to redact - no action will be performed");
                return;
            }

            var progress = new BuildProgress();
            progress.Progress.Updated += update =>
            {
                Dispatcher.InvokeAsync(() =>
                {
                    progress.Value = update.Ratio;
                }, DispatcherPriority.Background);
            };
            progress.ProgressText = "Performing the log redaction ...";
            SetContent(progress);

            string error = await System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    BinlogRedactorOptions redactorOptions = new BinlogRedactorOptions(logFilePath)
                    {
                        OutputFileName = redactInputControl.DestinationFile,
                        ProcessEmbeddedFiles = redactInputControl.RedactEmbeddedFiles,
                        AutodetectUsername = redactInputControl.RedactUsername,
                        AutodetectCommonPatterns = redactInputControl.RedactCommonCredentials,
                        IdentifyReplacemenets = redactInputControl.DistinguishSecretsReplacements,
                        TokensToRedact = stringsToRedact.ToArray(),
                    };

                    BinlogRedactor.RedactSecrets(
                        redactorOptions,
                        progress.Progress);
                }
                catch (Exception e)
                {
                    return e.ToString();
                }

                return null;
            });

            if (!string.IsNullOrEmpty(error))
            {
                MessageBox.Show($"Redaction failed:{Environment.NewLine}{error}");
                SetContent(currentBuild);
            }
            else if (string.IsNullOrEmpty(redactInputControl.DestinationFile))
            {
                // Reload
                OpenLogFile(logFilePath);
            }
            else
            {
                SetContent(currentBuild);
                AnnounceFileSaved(redactInputControl.DestinationFile);
            }
        }

        private string GetSaveAsDestination()
        {
            string currentFilePath = currentBuild.LogFilePath;

            var saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = currentFilePath != null && currentFilePath.EndsWith(".binlog", StringComparison.OrdinalIgnoreCase) ? Serialization.BinlogFileDialogFilter : Serialization.FileDialogFilter;
            saveFileDialog.Title = "Save log file as";
            saveFileDialog.CheckFileExists = false;
            saveFileDialog.OverwritePrompt = true;
            saveFileDialog.ValidateNames = true;
            var result = saveFileDialog.ShowDialog(this);

            return result == true ? saveFileDialog.FileName : null;
        }

        private void AnnounceFileSaved(string filePath)
        {
            Dispatcher.InvokeAsync(() =>
            {
                currentBuild.UpdateBreadcrumb(new Message { Text = $"Saved {filePath}" });
            });
            SettingsService.AddRecentLogFile(filePath);
        }

        private void SaveAs()
        {
            if (currentBuild != null)
            {
                string currentFilePath = currentBuild.LogFilePath;
                string newFilePath = GetSaveAsDestination();
                if (string.IsNullOrEmpty(newFilePath) || string.Equals(currentFilePath, newFilePath, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                logFilePath = newFilePath;

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

                            AnnounceFileSaved(logFilePath);
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

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F && e.KeyboardDevice.Modifiers == ModifierKeys.Control)
            {
                FocusSearch();
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
            else if (e.Key == Key.R && e.KeyboardDevice.Modifiers == ModifierKeys.Control)
            {
                RedactSecrets().Ignore();
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

        private void RedactSecrets_Click(object sender, RoutedEventArgs e)
        {
            RedactSecrets().Ignore();
        }

        private void HelpLink_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://github.com/KirillOsenkov/MSBuildStructuredLog") { UseShellExecute = true });
        }

        private void HelpLink2_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://msbuildlog.com") { UseShellExecute = true });
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

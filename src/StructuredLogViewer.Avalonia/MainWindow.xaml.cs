using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Platform.Storage;
using Microsoft.Build.Logging.StructuredLogger;
using StructuredLogger.Utils;
using StructuredLogViewer.Avalonia.Controls;
using Task = System.Threading.Tasks.Task;

namespace StructuredLogViewer.Avalonia
{
    public class MainWindow : Window
    {
        private string logFilePath;
        private string projectFilePath;
        private BuildControl currentBuild;
        private string lastSearchText;
        private double scale = 1.0;

        public const string DefaultTitle = "MSBuild Structured Log Viewer";

        private ContentPresenter mainContent;
        private LayoutTransformControl layoutTransform;
        private MenuItem RecentProjectsMenu;
        private MenuItem RecentLogsMenu;
        private MenuItem ReloadMenu;
        private MenuItem SaveAsMenu;
        private MenuItem RedactSecretsMenu;
        private MenuItem StatsMenu;
        private Separator RecentItemsSeparator;
        private MenuItem startPage;
        private MenuItem Build;
        private MenuItem Rebuild;
        private MenuItem Open;
        private MenuItem SetMSBuild;
        private MenuItem SearchSyntaxLink;
        private MenuItem HelpLink;
        private MenuItem HelpLink2;
        private MenuItem AboutMenu;
        private MenuItem Exit;
        private StackPanel exceptionPanel;
        private TextBlock exceptionText;
        private Button closeExceptionButton;

        public MainWindow()
        {
            AppDomain.CurrentDomain.FirstChanceException += CurrentDomain_FirstChanceException;

            InitializeComponent();

            DialogService.ShowMessageBoxEvent += ShowMessageBox;

            RestoreWindowPosition();
            Closing += (s, e) => SaveWindowPosition();

            AddHandler(DragDrop.DropEvent, MainWindow_Drop);
            AddHandler(PointerWheelChangedEvent, MainWindow_PointerWheelChanged, global::Avalonia.Interactivity.RoutingStrategies.Tunnel);

            TemplateApplied += MainWindow_Loaded;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            this.RegisterControl(out mainContent, nameof(mainContent));
            this.RegisterControl(out layoutTransform, nameof(layoutTransform));
            this.RegisterControl(out startPage, nameof(startPage));
            this.RegisterControl(out RecentProjectsMenu, nameof(RecentProjectsMenu));
            this.RegisterControl(out RecentLogsMenu, nameof(RecentLogsMenu));
            this.RegisterControl(out ReloadMenu, nameof(ReloadMenu));
            this.RegisterControl(out SaveAsMenu, nameof(SaveAsMenu));
            this.RegisterControl(out RedactSecretsMenu, nameof(RedactSecretsMenu));
            this.RegisterControl(out StatsMenu, nameof(StatsMenu));
            this.RegisterControl(out RecentItemsSeparator, nameof(RecentItemsSeparator));
            this.RegisterControl(out Build, nameof(Build));
            this.RegisterControl(out Rebuild, nameof(Rebuild));
            this.RegisterControl(out Open, nameof(Open));
            this.RegisterControl(out SetMSBuild, nameof(SetMSBuild));
            this.RegisterControl(out SearchSyntaxLink, nameof(SearchSyntaxLink));
            this.RegisterControl(out HelpLink, nameof(HelpLink));
            this.RegisterControl(out HelpLink2, nameof(HelpLink2));
            this.RegisterControl(out AboutMenu, nameof(AboutMenu));
            this.RegisterControl(out Exit, nameof(Exit));
            this.RegisterControl(out exceptionPanel, nameof(exceptionPanel));
            this.RegisterControl(out exceptionText, nameof(exceptionText));
            this.RegisterControl(out closeExceptionButton, nameof(closeExceptionButton));

            exceptionText.PointerPressed += (s, e) => OpenExceptionLog();
            closeExceptionButton.Click += (s, e) => ExceptionText = null;

            this.KeyUp += Window_KeyUp;
            this.KeyDown += Window_KeyDown;

            startPage.Click += StartPage_Click;
            Build.Click += Build_Click;
            Rebuild.Click += Rebuild_Click;
            Open.Click += Open_Click;
            ReloadMenu.Click += Reload_Click;
            SaveAsMenu.Click += SaveAs_Click;
            RedactSecretsMenu.Click += RedactSecrets_Click;
            StatsMenu.Click += Stats_Click;
            SetMSBuild.Click += SetMSBuild_Click;
            SearchSyntaxLink.Click += SearchSyntaxLink_Click;
            HelpLink.Click += HelpLink_Click;
            HelpLink2.Click += HelpLink2_Click;
            AboutMenu.Click += HelpAbout_Click;
            Exit.Click += Exit_Click;
        }

        private bool exceptionReentrancyGuard;

        private void CurrentDomain_FirstChanceException(object sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e)
        {
            if (exceptionReentrancyGuard)
            {
                return;
            }

            try
            {
                exceptionReentrancyGuard = true;
                var exception = e.Exception;
                if (exception == null)
                {
                    return;
                }

                exception = ExceptionHandler.Unwrap(exception);

                if (ShouldIgnore(exception))
                {
                    return;
                }

                string message = exception.Message;
                ExceptionText = message;
                ErrorReporting.ReportException(exception);
            }
            catch
            {
            }
            finally
            {
                exceptionReentrancyGuard = false;
            }
        }

        private bool ShouldIgnore(Exception exception)
        {
            if (exception is OperationCanceledException
                or AggregateException
                or TargetInvocationException
                or FileNotFoundException
                or IOException
                or UnauthorizedAccessException)
            {
                return true;
            }

            return false;
        }

        private void UpdateZoomLevel(double zoom)
        {
            scale = zoom;
            layoutTransform.LayoutTransform = new ScaleTransform(zoom, zoom);
        }

        private void MainWindow_Drop(object sender, DragEventArgs e)
        {
            var files = e.DataTransfer.TryGetFiles();
            if (files is { Length: 1 } &&
                files[0].Path is { IsAbsoluteUri: true, Scheme: "file" } uri)
            {
                OpenFile(uri.LocalPath);
            }
        }

        private void MainWindow_PointerWheelChanged(object sender, PointerWheelEventArgs e)
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                const double min = 1;
                const double max = 4;
                const double step = 0.1;

                if (e.Delta.Y > 0 && scale < max)
                {
                    UpdateZoomLevel(Math.Min(max, scale + step));
                }
                else if (e.Delta.Y < 0 && scale > min)
                {
                    UpdateZoomLevel(Math.Max(min, scale - step));
                }

                e.Handled = true;
            }
        }

        private async Task<bool> TryOpenFromClipboard()
        {
            var text = await GetSingleFileFromClipboard();
            if (string.IsNullOrEmpty(text) || text.Length > 260)
            {
                return false;
            }

            text = text.TrimStart('"').TrimEnd('"');

            if (!text.EndsWith(".binlog", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

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

        private async Task<string> GetSingleFileFromClipboard()
        {
            try
            {
                var files = await Clipboard.TryGetFilesAsync();
                if (files is { Length: 1 } &&
                    files[0].Path is { IsAbsoluteUri: true, Scheme: "file" } uri)
                {
                    return uri.LocalPath;
                }
            }
            catch
            {
            }

            try
            {
                return await Clipboard.TryGetTextAsync();
            }
            catch
            {
                return null;
            }
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
            welcomeScreen.RecentLogSelected += log => Dispatcher.UIThread.Post(() => OpenLogFile(log));
            welcomeScreen.RecentProjectSelected += project => Dispatcher.UIThread.Post(() => BuildProject(project));
            welcomeScreen.OpenProjectRequested += async () => await OpenProjectOrSolution();
            welcomeScreen.OpenLogFileRequested += async () => await OpenLogFile();
            welcomeScreen.PropertyChanged += WelcomeScreen_PropertyChanged;
            UpdateRecentItemsMenu();
        }

        private void WelcomeScreen_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(WelcomeScreen.UseDarkTheme))
            {
                App.UpdateTheme();
            }
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

                if (await TryOpenFromClipboard())
                {
                    return;
                }

                DisplayWelcomeScreen();
            }
            catch (Exception ex)
            {
                var welcomeScreen = mainContent.Content as WelcomeScreen;
                if (welcomeScreen != null)
                {
                    welcomeScreen.Message = ex.ToString();
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
                return false;
            }

            var filePath = args[1];
            if (filePath.IndexOfAny(Path.GetInvalidPathChars()) == -1)
            {
                try
                {
                    filePath = Path.GetFullPath(filePath);
                }
                catch
                {
                }
            }

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

            RecentItemsSeparator.IsVisible = false;

            (RecentProjectsMenu.Items as IList)?.Clear();
            if (welcomeScreen.ShowRecentProjects)
            {
                RecentProjectsMenu.IsVisible = true;
                RecentItemsSeparator.IsVisible = true;
                foreach (var recentProjectFile in welcomeScreen.RecentProjects)
                {
                    var menuItem = new MenuItem { Header = recentProjectFile };
                    menuItem.Click += RecentProjectClick;
                    (RecentProjectsMenu.Items as IList)?.Add(menuItem);
                }

                var clearHistory = new MenuItem { Header = "Clear Recent Projects" };
                clearHistory.Click += ClearRecentProjects;
                (RecentProjectsMenu.Items as IList)?.Add(clearHistory);
            }
            else
            {
                RecentProjectsMenu.IsVisible = false;
            }

            (RecentLogsMenu.Items as IList)?.Clear();
            if (welcomeScreen.ShowRecentLogs)
            {
                RecentLogsMenu.IsVisible = true;
                RecentItemsSeparator.IsVisible = true;
                foreach (var recentLog in welcomeScreen.RecentLogs)
                {
                    var menuItem = new MenuItem { Header = recentLog };
                    menuItem.Click += RecentLogFileClick;
                    (RecentLogsMenu.Items as IList)?.Add(menuItem);
                }

                var clearHistory = new MenuItem { Header = "Clear Recent Logs" };
                clearHistory.Click += ClearRecentLogFiles;
                (RecentLogsMenu.Items as IList)?.Add(clearHistory);
            }
            else
            {
                RecentLogsMenu.IsVisible = false;
            }
        }

        private BuildControl CurrentBuildControl => mainContent.Content as BuildControl;

        private void SetContent(object content)
        {
            // remember the search text so we can restore it if a build is reloaded
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
                ReloadMenu.IsVisible = logFilePath != null;
                SaveAsMenu.IsVisible = true;
                RedactSecretsMenu.IsVisible = logFilePath != null;
            }
            else
            {
                ReloadMenu.IsVisible = false;
                SaveAsMenu.IsVisible = false;
                RedactSecretsMenu.IsVisible = false;
            }

            if (mainContent.Content is BuildControl currentContent && !string.IsNullOrEmpty(lastSearchText))
            {
                currentContent.searchLogControl.SearchText = lastSearchText;
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
            Microsoft.Build.Logging.StructuredLogger.Build.IgnoreEmbeddedFiles = SettingsService.IgnoreEmbeddedFiles;
            UpdateRecentItemsMenu();
            Title = filePath + " - " + DefaultTitle;

            var progress = new BuildProgress();
            progress.Progress.Updated += update =>
            {
                Dispatcher.UIThread.InvokeAsync(() =>
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

            Build build = await Task.Run(() =>
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
            progress.IsIndeterminate = true;
            stopwatch.Restart();
            await Task.Run(() => build.SearchIndex = new SearchIndex(build));
            var indexingTime = stopwatch.Elapsed;

            progress.ProgressText = "Reading embedded files...";
            TimeSpan embeddedFilesTime = TimeSpan.Zero;
            await Task.Run(() =>
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
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded); // let the progress message be rendered before we block the UI again

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
                currentBuild.Build.AddChild(new Message { Text = text });
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

        private async Task QueueAnalyzeBuild(Build build)
        {
            await Task.Run(() =>
            {
                try
                {
                    BuildAnalyzer.AnalyzeBuild(build);
                    build.SearchExtensions.Add(new SecretsSearch(build));
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
            IClipboard clipboardService = GetTopLevel(this)?.Clipboard;
            var parametersScreen = new BuildParametersScreen(clipboardService);
            parametersScreen.BrowseForMSBuildRequsted += BrowseForMSBuildExe;
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
            var progress = new BuildProgress() { IsIndeterminate = true };
            progress.ProgressText = $"Building {projectFilePath}...";
            SetContent(progress);
            var buildHost = new HostedBuild(projectFilePath, customArguments);
            Build result = await buildHost.BuildAndGetResult(progress);
            progress.ProgressText = "Analyzing build...";
            await QueueAnalyzeBuild(result);
            DisplayBuild(result);
            if (!string.IsNullOrWhiteSpace(searchText) && CurrentBuildControl != null)
            {
                CurrentBuildControl.InitialSearchText = searchText;
            }
        }

        private async void Open_Click(object sender, RoutedEventArgs e)
        {
            await OpenLogFile();
        }

        private async void Build_Click(object sender, RoutedEventArgs e)
        {
            await OpenProjectOrSolution();
        }

        private async Task OpenLogFile()
        {
            var files = await TopLevel.GetTopLevel(this)!.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Build Log",
                FileTypeFilter = new[] { FilePickerFileTypes.All, FileTypes.Binlog, FilePickerFileTypes.Xml }
            });

            var firstFile = files.FirstOrDefault();
            if (firstFile is null)
            {
                return;
            }

            if (firstFile.Path is { IsAbsoluteUri: true, Scheme: "file" } fileNameUri)
            {
                OpenLogFile(fileNameUri.LocalPath);
            }
        }

        private async Task OpenProjectOrSolution()
        {
            var files = await TopLevel.GetTopLevel(this)!.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
            {
                Title = "Open a solution or project",
                FileTypeFilter = new[] { FilePickerFileTypes.All, FileTypes.MsBuildProj, FileTypes.Sln }
            });
            var result = files.FirstOrDefault();

            if (result is not null
                && result.Path is { IsAbsoluteUri: true, Scheme: "file" } filePath)
            {
                BuildProject(filePath.LocalPath);
            }
        }

        private void RebuildProjectOrSolution()
        {
            if (!string.IsNullOrEmpty(projectFilePath))
            {
                var searchText = CurrentBuildControl?.SearchText;
                var args = SettingsService.GetCustomArguments(projectFilePath);
                BuildCore(projectFilePath, args, searchText);
            }
        }

        private void DisplayBuild(Build build)
        {
            currentBuild?.Dispose();

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
            if (logFilePath != null)
            {
                OpenLogFile(logFilePath);
            }
        }

        private async Task RedactSecrets()
        {
            if (currentBuild == null || logFilePath == null)
            {
                return;
            }

            var redactInputControl = new RedactInputControl(GetSaveAsDestination);
            var accepted = await redactInputControl.ShowDialog<bool>(this);
            if (!accepted)
            {
                return;
            }

            List<string> stringsToRedact =
                new(redactInputControl.SecretsBlock?
                        .Split(new[] { Environment.NewLine, "\n" }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.Trim())
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                    ?? new string[] { });

            if (
                !stringsToRedact.Any() &&
                !redactInputControl.RedactUsername &&
                !redactInputControl.RedactCommonCredentials)
            {
                ShowMessageBox("No secrets to redact - no action will be performed");
                return;
            }

            var progress = new BuildProgress();
            progress.Progress.Updated += update =>
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    progress.Value = update.Ratio;
                }, DispatcherPriority.Background);
            };
            progress.ProgressText = "Performing the log redaction ...";
            SetContent(progress);

            string error = await Task.Run(() =>
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
                ShowMessageBox($"Redaction failed:{Environment.NewLine}{error}");
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

        private async Task<string> GetSaveAsDestination()
        {
            string currentFilePath = currentBuild.LogFilePath;
            bool isBinlog = currentFilePath != null && currentFilePath.EndsWith(".binlog", StringComparison.OrdinalIgnoreCase);

            var result = await TopLevel.GetTopLevel(this)!.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions()
            {
                Title = "Save log file as",
                FileTypeChoices = isBinlog
                    ? new[] { FileTypes.Binlog }
                    : new[] { FileTypes.Binlog, FilePickerFileTypes.Xml },
                DefaultExtension = FileTypes.BinlogDefaultExtension,
            });

            if (result?.Path is { IsAbsoluteUri: true, Scheme: "file" } fileNameUri)
            {
                return fileNameUri.LocalPath;
            }

            return null;
        }

        private void AnnounceFileSaved(string filePath)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                currentBuild.UpdateBreadcrumb(new Message { Text = $"Saved {filePath}" });
            });
            SettingsService.AddRecentLogFile(filePath);
        }

        private async Task SaveAs()
        {
            if (currentBuild != null)
            {
                string currentFilePath = currentBuild.LogFilePath;
                string newFilePath = await GetSaveAsDestination();
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
                            if (logFilePath.EndsWith(".binlog", StringComparison.OrdinalIgnoreCase) &&
                                currentFilePath != null &&
                                File.Exists(currentFilePath))
                            {
                                // for binlogs a copy preserves the original bytes losslessly
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
        public Task InProgressTask = Task.CompletedTask;

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F && e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift))
            {
                CurrentBuildControl?.SelectFindInFilesTab();
                e.Handled = true;
            }
            else if (e.Key == Key.F && e.KeyModifiers == KeyModifiers.Control)
            {
                FocusSearch();
            }
        }

        private async void Window_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F5)
            {
                Reload();
            }
            else if (e.Key == Key.F6 && e.KeyModifiers == KeyModifiers.Shift)
            {
                RebuildProjectOrSolution();
            }
            else if (e.Key == Key.F6)
            {
                await OpenProjectOrSolution();
            }
            else if (e.Key == Key.O && e.KeyModifiers == KeyModifiers.Control)
            {
                await OpenLogFile();
            }
            else if (e.Key == Key.S && e.KeyModifiers == KeyModifiers.Control)
            {
                await SaveAs();
            }
            else if (e.Key == Key.R && e.KeyModifiers == KeyModifiers.Control)
            {
                await RedactSecrets();
            }
            else if (e.Key == Key.D0 && e.KeyModifiers == KeyModifiers.Control)
            {
                UpdateZoomLevel(1.0);
                e.Handled = true;
            }
            else if (e.Key == Key.C && e.KeyModifiers == KeyModifiers.Control)
            {
                var content = mainContent.Content as BuildProgress;
                if (content != null)
                {
                    await Clipboard.SetTextAsync(content.MSBuildCommandLine);
                }
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

        private async void SetMSBuild_Click(object sender, RoutedEventArgs e)
        {
            await BrowseForMSBuildExe();

            var buildParametersScreen = mainContent.Content as BuildParametersScreen;
            if (buildParametersScreen != null)
            {
                buildParametersScreen.UpdateMSBuildLocations();
            }
        }

        private async Task BrowseForMSBuildExe()
        {
            var files = await TopLevel.GetTopLevel(this)!.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
            {
                FileTypeFilter = new[] { FilePickerFileTypes.All, FileTypes.Exe },
                Title = "Select MSBuild file location",
            });

            var result = files.FirstOrDefault();
            if (result is null)
            {
                return;
            }

            if (result.Path is not { IsAbsoluteUri: true, Scheme: "file" } fileNameUri)
            {
                return;
            }

            var fileName = fileNameUri.LocalPath;
            var isMsBuild = fileName.EndsWith("MSBuild.dll", StringComparison.OrdinalIgnoreCase)
                         || fileName.EndsWith("MSBuild.exe", StringComparison.OrdinalIgnoreCase);
            if (!isMsBuild)
            {
                return;
            }

            SettingsService.AddRecentMSBuildLocation(fileName);
        }

        private void Stats_Click(object sender, RoutedEventArgs e)
        {
            if (currentBuild != null)
            {
                currentBuild.DisplayStats();
            }
        }

        private async void SaveAs_Click(object sender, RoutedEventArgs e)
        {
            await SaveAs();
        }

        private void OpenInBrowser(string url)
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }

        private void SearchSyntaxLink_Click(object sender, RoutedEventArgs e)
        {
            OpenInBrowser("https://github.com/KirillOsenkov/MSBuildStructuredLog/wiki/Search-Syntax");
        }

        private void RedactSecrets_Click(object sender, RoutedEventArgs e)
        {
            _ = RedactSecrets();
        }

        private void HelpLink_Click(object sender, RoutedEventArgs e)
        {
            OpenInBrowser("https://github.com/KirillOsenkov/MSBuildStructuredLog");
        }

        private void HelpLink2_Click(object sender, RoutedEventArgs e)
        {
            OpenInBrowser("https://msbuildlog.com");
        }

        private void HelpAbout_Click(object sender, RoutedEventArgs e)
        {
            var version = typeof(MainWindow).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            var message = string.IsNullOrEmpty(version)
                ? "Locally built version"
                : $"Version {version}";
            ShowMessageBox(message);
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            ((IClassicDesktopStyleApplicationLifetime)Application.Current.ApplicationLifetime).Shutdown();
        }

        private void StartPage_Click(object sender, RoutedEventArgs e)
        {
            DisplayWelcomeScreen();
        }

        private string currentExceptionText;
        public string ExceptionText
        {
            get => currentExceptionText;
            set
            {
                if (currentExceptionText == value)
                {
                    return;
                }

                currentExceptionText = value;
                Dispatcher.UIThread.Post(UpdateExceptionVisibility);
            }
        }

        private void UpdateExceptionVisibility()
        {
            if (!string.IsNullOrEmpty(currentExceptionText))
            {
                exceptionText.Text = currentExceptionText;
                exceptionPanel.IsVisible = true;
            }
            else
            {
                exceptionPanel.IsVisible = false;
            }
        }

        private void OpenExceptionLog()
        {
            try
            {
                Process.Start(new ProcessStartInfo(ErrorReporting.LogFilePath) { UseShellExecute = true });
            }
            catch
            {
            }
        }

        public static void ShowMessageBox(string message)
        {
            Dispatcher.UIThread.Post(() =>
            {
                var owner = (Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

                var text = new TextBox
                {
                    Text = message,
                    IsReadOnly = true,
                    TextWrapping = TextWrapping.Wrap,
                    AcceptsReturn = true,
                    BorderThickness = new Thickness(0),
                    Background = Brushes.Transparent
                };
                var okButton = new Button
                {
                    Content = "OK",
                    MinWidth = 75,
                    Margin = new Thickness(0, 12, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    IsDefault = true
                };
                var panel = new DockPanel { Margin = new Thickness(12) };
                DockPanel.SetDock(okButton, global::Avalonia.Controls.Dock.Bottom);
                panel.Children.Add(okButton);
                panel.Children.Add(new ScrollViewer { Content = text });

                var window = new Window
                {
                    Title = DefaultTitle,
                    Content = panel,
                    SizeToContent = SizeToContent.WidthAndHeight,
                    MaxWidth = 800,
                    MaxHeight = 600,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    ShowInTaskbar = false,
                    FontSize = 12
                };
                okButton.Click += (s, e) => window.Close();

                if (owner != null)
                {
                    window.ShowDialog(owner);
                }
                else
                {
                    window.Show();
                }
            });
        }

        private void RestoreWindowPosition()
        {
            try
            {
                var saved = SettingsService.AvaloniaWindowPosition;
                if (string.IsNullOrEmpty(saved))
                {
                    return;
                }

                var parts = saved.Split(',');
                if (parts.Length != 5)
                {
                    return;
                }

                var x = int.Parse(parts[0]);
                var y = int.Parse(parts[1]);
                var width = double.Parse(parts[2]);
                var height = double.Parse(parts[3]);
                var maximized = parts[4] == "1";

                if (width < 200 || height < 200)
                {
                    return;
                }

                WindowStartupLocation = WindowStartupLocation.Manual;
                Position = new PixelPoint(x, y);
                if (maximized)
                {
                    WindowState = WindowState.Maximized;
                }
                else
                {
                    WindowState = WindowState.Normal;
                    Width = width;
                    Height = height;
                }
            }
            catch
            {
                // fall back to the defaults from XAML (centered, maximized)
            }
        }

        private void SaveWindowPosition()
        {
            try
            {
                var maximized = WindowState == WindowState.Maximized;
                SettingsService.AvaloniaWindowPosition = $"{Position.X},{Position.Y},{(int)Width},{(int)Height},{(maximized ? 1 : 0)}";
            }
            catch
            {
            }
        }
    }
}

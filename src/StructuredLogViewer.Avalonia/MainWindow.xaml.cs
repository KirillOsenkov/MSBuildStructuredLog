using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.Build.Logging.StructuredLogger;
using StructuredLogViewer.Avalonia.Controls;
using Task = System.Threading.Tasks.Task;

namespace StructuredLogViewer.Avalonia
{
    public class MainWindow : Window
    {
        private string logFilePath;
        private string projectFilePath;
        private BuildControl currentBuild;

        private const string ClipboardFileFormat = "FileDrop";
        public const string DefaultTitle = "MSBuild Structured Log Viewer";

        private ContentPresenter mainContent;
        private MenuItem RecentProjectsMenu;
        private MenuItem RecentLogsMenu;
        private MenuItem ReloadMenu;
        private MenuItem SaveAsMenu;
        private Separator RecentItemsSeparator;
        private MenuItem startPage;
        private MenuItem Build;
        private MenuItem Rebuild;
        private MenuItem Open;
        private MenuItem SetMSBuild;
        private MenuItem HelpLink;
        private MenuItem Exit;

        public MainWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif

            TemplateApplied += MainWindow_Loaded;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            this.RegisterControl(out mainContent, nameof(mainContent));
            this.RegisterControl(out startPage, nameof(startPage));
            this.RegisterControl(out RecentProjectsMenu, nameof(RecentProjectsMenu));
            this.RegisterControl(out RecentLogsMenu, nameof(RecentLogsMenu));
            this.RegisterControl(out ReloadMenu, nameof(ReloadMenu));
            this.RegisterControl(out SaveAsMenu, nameof(SaveAsMenu));
            this.RegisterControl(out RecentItemsSeparator, nameof(RecentItemsSeparator));
            this.RegisterControl(out Build, nameof(Build));
            this.RegisterControl(out Rebuild, nameof(Rebuild));
            this.RegisterControl(out Open, nameof(Open));
            this.RegisterControl(out SetMSBuild, nameof(SetMSBuild));
            this.RegisterControl(out HelpLink, nameof(HelpLink));
            this.RegisterControl(out Exit, nameof(Exit));

            this.KeyUp += Window_KeyUp;

            startPage.Click += StartPage_Click;
            Build.Click += Build_Click;
            Rebuild.Click += Rebuild_Click;
            Open.Click += Open_Click;
            ReloadMenu.Click += Reload_Click;
            SaveAsMenu.Click += SaveAs_Click;
            SetMSBuild.Click += SetMSBuild_Click;
            HelpLink.Click += HelpLink_Click;
            Exit.Click += Exit_Click;
        }

        private async Task<bool> TryOpenFromClipboard()
        {
            var text = await Application.Current.Clipboard.GetTextAsync();
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

        private void DisplayWelcomeScreen(string message = "")
        {
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

                if (await TryOpenFromClipboard())
                {
                    return;
                }

                DisplayWelcomeScreen();

                // only check for updates if there were no command-line arguments and debugger not attached
                if (Debugger.IsAttached || SettingsService.DisableUpdates)
                {
                    return;
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
                (RecentProjectsMenu.Items as IList)?.Clear();
                RecentProjectsMenu.IsVisible = true;
                RecentItemsSeparator.IsVisible = true;
                foreach (var recentProjectFile in welcomeScreen.RecentProjects)
                {
                    var menuItem = new MenuItem { Header = recentProjectFile };
                    menuItem.Click += RecentProjectClick;
                    (RecentProjectsMenu.Items as IList)?.Add(menuItem);
                }
            }

            if (welcomeScreen.ShowRecentLogs)
            {
                (RecentLogsMenu.Items as IList)?.Clear();
                RecentLogsMenu.IsVisible = true;
                RecentItemsSeparator.IsVisible = true;
                foreach (var recentLog in welcomeScreen.RecentLogs)
                {
                    var menuItem = new MenuItem { Header = recentLog };
                    menuItem.Click += RecentLogFileClick;
                    (RecentLogsMenu.Items as IList)?.Add(menuItem);
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
                ReloadMenu.IsVisible = logFilePath != null;
                SaveAsMenu.IsVisible = true;
            }
            else
            {
                ReloadMenu.IsVisible = false;
                SaveAsMenu.IsVisible = false;
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

            var progress = new BuildProgress() { IsIndeterminate = true };
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
                await QueueAnalyzeBuild(build);
            }

            progress.ProgressText = "Reading embedded files...";
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded); // let the progress message be rendered before we block the UI again
            _ = build.SourceFiles;

            progress.ProgressText = "Rendering tree...";
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded); // let the progress message be rendered before we block the UI again

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

        private async void BuildCore(string projectFilePath, string customArguments)
        {
            var progress = new BuildProgress() { IsIndeterminate = true };
            progress.ProgressText = $"Building {projectFilePath}...";
            SetContent(progress);
            var buildHost = new HostedBuild(projectFilePath, customArguments);
            Build result = await buildHost.BuildAndGetResult(progress);
            progress.ProgressText = "Analyzing build...";
            await QueueAnalyzeBuild(result);
            DisplayBuild(result);
        }

        private async Task QueueAnalyzeBuild(Build build)
        {
            await System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    BuildAnalyzer.AnalyzeBuild(build);
                }
                catch (Exception ex)
                {
                    DialogService.ShowMessageBox(
                    "Error while analyzing build. Sorry about that. Please Ctrl+C to copy this text and file an issue on https://github.com/KirillOsenkov/MSBuildStructuredLog/issues/new \r\n" + ex.ToString());
                }
            });
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
            var openFileDialog = new OpenFileDialog();
            openFileDialog.Filters.Add(new FileDialogFilter { Name = "Build Log (*.binlog;*.buildlog;*.xml)", Extensions = { "binlog", "buildlog", "xml" } });
            openFileDialog.Title = "Open a build log file";
            var result = await openFileDialog.ShowAndGetFileAsync(this);
            if (!File.Exists(result))
            {
                return;
            }

            OpenLogFile(result);
        }

        private async Task OpenProjectOrSolution()
        {
            var openFileDialog = new OpenFileDialog();
            openFileDialog.Filters.Add(new FileDialogFilter { Name = "MSBuild projects and solutions (*.sln;*.*proj)", Extensions = { "sln", "*proj" } });
            openFileDialog.Title = "Open a solution or project";
            var result = await openFileDialog.ShowAndGetFileAsync(this);
            if (!File.Exists(result))
            {
                return;
            }

            BuildProject(result);
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

        private async Task SaveAs()
        {
            if (currentBuild != null)
            {
                var saveFileDialog = new SaveFileDialog();
                saveFileDialog.Filters.Add(new FileDialogFilter { Name = "Binary (compact) Structured Build Log (*.buildlog)", Extensions = { "buildlog" } });
                saveFileDialog.Filters.Add(new FileDialogFilter { Name = "Readable (large) XML Log (*.xml)", Extensions = { "xml" } });
                saveFileDialog.Title = "Save log file as";
                var result = await saveFileDialog.ShowAsync(this);
                if (result == null)
                {
                    return;
                }

                logFilePath = result;
                await System.Threading.Tasks.Task.Run(() =>
                {
                    Serialization.Write(currentBuild.Build, logFilePath);
                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        currentBuild.UpdateBreadcrumb(new Message { Text = $"Saved {logFilePath}" });
                    });
                    SettingsService.AddRecentLogFile(logFilePath);
                });
            }
        }

        private async void Window_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F5)
            {
                Reload();
            }
            else if (e.Key == Key.F6 && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                RebuildProjectOrSolution();
            }
            else if (e.Key == Key.F6)
            {
                await OpenProjectOrSolution();
            }
            else if (e.Key == Key.O && e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                await OpenLogFile();
            }
            else if (e.Key == Key.F && e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                FocusSearch();
            }
            else if (e.Key == Key.C && e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                var content = mainContent.Content as BuildProgress;
                if (content != null)
                {
                    await Application.Current.Clipboard.SetTextAsync(content.MSBuildCommandLine);
                }
            }
            else if (e.Key == Key.S && e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                _ = SaveAs();
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
            var openFileDialog = new OpenFileDialog
            {
                Filters = { new FileDialogFilter { Name = "MSBuild (.dll;.exe)", Extensions = { "dll", "exe" } } },
                Title = "Select MSBuild file location",
            };

            var fileName = await openFileDialog.ShowAndGetFileAsync(this);
            if (!File.Exists(fileName))
            {
                return;
            }

            var isMsBuild = fileName.EndsWith("MSBuild.dll", StringComparison.OrdinalIgnoreCase)
                         || fileName.EndsWith("MSBuild.exe", StringComparison.OrdinalIgnoreCase);
            if (!isMsBuild)
            {
                return;
            }

            SettingsService.AddRecentMSBuildLocation(fileName);
        }

        private void SaveAs_Click(object sender, RoutedEventArgs e)
        {
            _ = SaveAs();
        }

        private void HelpLink_Click(object sender, RoutedEventArgs e)
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "https://github.com/KirillOsenkov/MSBuildStructuredLog",
                UseShellExecute = true
            };
            Process.Start(psi);
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            ((IClassicDesktopStyleApplicationLifetime)Application.Current.ApplicationLifetime).Shutdown();
        }

        private void StartPage_Click(object sender, RoutedEventArgs e)
        {
            DisplayWelcomeScreen();
        }
    }
}

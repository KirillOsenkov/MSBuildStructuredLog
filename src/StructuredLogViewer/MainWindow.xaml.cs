using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Build.Logging.StructuredLogger;
using Microsoft.Win32;
using StructuredLogViewer.Controls;

namespace StructuredLogViewer
{
    public partial class MainWindow : Window
    {
        private string xmlLogFilePath;
        private string projectFilePath;
        private BuildControl currentBuild;

        public const string DefaultTitle = "MSBuild Structured Log Viewer";

        public MainWindow()
        {
            InitializeComponent();
            var uri = new Uri("StructuredLogViewer;component/themes/Generic.xaml", UriKind.Relative);
            var generic = (ResourceDictionary)Application.LoadComponent(uri);
            Application.Current.Resources.MergedDictionaries.Add(generic);
            var welcomeScreen = new WelcomeScreen();
            SetContent(welcomeScreen);
            welcomeScreen.RecentLogSelected += log => OpenLogFile(log);
            welcomeScreen.RecentProjectSelected += project => BuildProject(project);
            welcomeScreen.OpenProjectRequested += () => OpenProjectOrSolution();
            welcomeScreen.OpenLogFileRequested += () => OpenLogFile();

            UpdateRecentItemsMenu();
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

        private void SetContent(object content)
        {
            mainContent.Content = content;
            if (content == null)
            {
                xmlLogFilePath = null;
                projectFilePath = null;
                currentBuild = null;
            }

            if (content is BuildControl)
            {
                ReloadMenu.Visibility = xmlLogFilePath != null ? Visibility.Visible : Visibility.Collapsed;
                SaveAsMenu.Visibility = Visibility.Visible;
                EditMenu.Visibility = Visibility.Visible;
            }
            else
            {
                ReloadMenu.Visibility = Visibility.Collapsed;
                SaveAsMenu.Visibility = Visibility.Collapsed;
                EditMenu.Visibility = Visibility.Collapsed;
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
            this.xmlLogFilePath = filePath;
            SettingsService.AddRecentLogFile(filePath);
            UpdateRecentItemsMenu();
            Title = DefaultTitle + " - " + filePath;
            var progress = new BuildProgress();
            progress.ProgressText = "Opening " + filePath + "...";
            SetContent(progress);
            Build build = await System.Threading.Tasks.Task.Run(() => XmlLogReader.ReadFromXml(filePath));
            progress.ProgressText = "Analyzing " + filePath + "...";
            await System.Threading.Tasks.Task.Run(() => BuildAnalyzer.AnalyzeBuild(build));
            DisplayBuild(build);
        }

        private async void BuildProject(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return;
            }

            DisplayBuild(null);
            this.projectFilePath = filePath;
            SettingsService.AddRecentProject(projectFilePath);
            UpdateRecentItemsMenu();
            Title = DefaultTitle + " - " + projectFilePath;
            var progress = new BuildProgress();
            progress.ProgressText = $"Building {projectFilePath}...";
            SetContent(progress);
            var buildHost = new HostedBuild(projectFilePath);
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
            openFileDialog.Filter = "Structured XML Log Files (*.xml)|*.xml";
            openFileDialog.Title = "Open .xml structured log file";
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

        private void DisplayBuild(Build build)
        {
            currentBuild = build != null ? new BuildControl(build) : null;
            SetContent(currentBuild);
        }

        private void Reload()
        {
            OpenLogFile(xmlLogFilePath);
        }

        private void SaveAs()
        {
            if (currentBuild != null)
            {
                var saveFileDialog = new SaveFileDialog();
                saveFileDialog.Filter = "Structured XML Log Files (*.xml)|*.xml";
                saveFileDialog.Title = "Save log file as";
                saveFileDialog.CheckFileExists = false;
                saveFileDialog.OverwritePrompt = true;
                saveFileDialog.ValidateNames = true;
                var result = saveFileDialog.ShowDialog(this);
                if (result != true)
                {
                    return;
                }

                xmlLogFilePath = saveFileDialog.FileName;
                System.Threading.Tasks.Task.Run(() =>
                {
                    XmlLogWriter.WriteToXml(currentBuild.Build, xmlLogFilePath);
                    Dispatcher.InvokeAsync(() =>
                    {
                        currentBuild.UpdateBreadcrumb(new Message { Text = $"Saved {xmlLogFilePath}" });
                    });
                });
            }
        }

        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F5)
            {
                Reload();
            }
            else if (e.Key == Key.F6)
            {
                OpenProjectOrSolution();
            }
            else if (e.Key == Key.O && e.KeyboardDevice.Modifiers == ModifierKeys.Control)
            {
                OpenLogFile();
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

        private void Reload_Click(object sender, RoutedEventArgs e)
        {
            Reload();
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            if (currentBuild != null)
            {
                currentBuild.Copy();
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (currentBuild != null)
            {
                currentBuild.Delete();
            }
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
    }
}

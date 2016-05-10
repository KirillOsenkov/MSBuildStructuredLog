using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
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
            mainContent.Content = new WelcomeScreen();
        }

        private void OpenLogFile(string filePath)
        {
            this.xmlLogFilePath = filePath;
            Title = DefaultTitle + " - " + filePath;
            var build = XmlLogReader.ReadFromXml(filePath);
            BuildAnalyzer.AnalyzeBuild(build);
            OpenBuild(build);
        }

        private void OpenBuild(Build build)
        {
            currentBuild = build != null ? new BuildControl(build) : null;
            mainContent.Content = currentBuild;
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            OpenFile();
        }

        private void OpenFile()
        {
            var openFileDialog = new OpenFileDialog();
            openFileDialog.DefaultExt = ".xml";
            openFileDialog.Title = "Open .xml structured log file";
            openFileDialog.CheckFileExists = true;
            var result = openFileDialog.ShowDialog(this);
            if (result != true)
            {
                return;
            }

            xmlLogFilePath = openFileDialog.FileName;
            OpenLogFile(xmlLogFilePath);
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void Reload_Click(object sender, RoutedEventArgs e)
        {
            Reload();
        }

        private void Reload()
        {
            if (File.Exists(xmlLogFilePath))
            {
                OpenLogFile(xmlLogFilePath);
            }
        }

        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F5)
            {
                Reload();
            }
            else if (e.Key == Key.O && e.KeyboardDevice.Modifiers == ModifierKeys.Control)
            {
                OpenFile();
            }
        }

        private void HelpLink_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://github.com/KirillOsenkov/MSBuildStructuredLog");
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

        private void Build_Click(object sender, RoutedEventArgs e)
        {
            OpenProjectOrSolution();
        }

        private void OpenProjectOrSolution()
        {
            var openFileDialog = new OpenFileDialog();
            openFileDialog.DefaultExt = "*.sln;*.*proj";
            openFileDialog.Title = "Open a solution or project";
            openFileDialog.CheckFileExists = true;
            var result = openFileDialog.ShowDialog(this);
            if (result != true)
            {
                return;
            }

            projectFilePath = openFileDialog.FileName;
            BuildProject();
        }

        private async void BuildProject()
        {
            OpenBuild(null);
            Title = DefaultTitle + " - " + projectFilePath;
            var progress = new BuildProgress();
            progress.ProgressText = $"Building {projectFilePath}...";
            mainContent.Content = progress;
            var buildHost = new HostedBuild(projectFilePath);
            Build result = await buildHost.BuildAndGetResult();
            progress.ProgressText = "Analyzing build...";
            await System.Threading.Tasks.Task.Run(() => { BuildAnalyzer.AnalyzeBuild(result); });
            OpenBuild(result);
        }
    }
}

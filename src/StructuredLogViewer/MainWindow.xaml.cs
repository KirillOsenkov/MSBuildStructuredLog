using System;
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
        private string filePath;

        public MainWindow()
        {
            InitializeComponent();
            var uri = new Uri("StructuredLogViewer;component/themes/Generic.xaml", UriKind.Relative);
            var generic = (ResourceDictionary)Application.LoadComponent(uri);
            Application.Current.Resources.MergedDictionaries.Add(generic);
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var file = @"D:\1.xml";
            if (File.Exists(file))
            {
                OpenFile(file);
            }
        }

        private void OpenFile(string filePath)
        {
            this.filePath = filePath;
            Title = "Structured Log Viewer - " + filePath;
            var build = XmlLogReader.ReadFromXml(filePath);
            BuildAnalyzer.AnalyzeBuild(build);
            mainContent.Content = new BuildControl(build);
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            OpenFile();
        }

        private void OpenFile()
        {
            var openFileDialog = new OpenFileDialog();
            openFileDialog.DefaultExt = ".xml";
            openFileDialog.Title = "Open .xml structured log file...";
            var result = openFileDialog.ShowDialog(this);
            if (result != true)
            {
                return;
            }

            filePath = openFileDialog.FileName;
            OpenFile(filePath);
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
            if (File.Exists(filePath))
            {
                OpenFile(filePath);
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
    }
}

using System;
using System.IO;
using System.Windows;
using Microsoft.Build.Logging.StructuredLogger;
using Microsoft.Win32;
using StructuredLogViewer.Controls;

namespace StructuredLogViewer
{
    public partial class MainWindow : Window
    {
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

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog();
            openFileDialog.DefaultExt = ".xml";
            openFileDialog.Title = "Open .xml structured log file...";
            var result = openFileDialog.ShowDialog(this);
            if (result != true)
            {
                return;
            }

            var filePath = openFileDialog.FileName;
            OpenFile(filePath);
        }

        private void OpenFile(string filePath)
        {
            Title = "Structured Log Viewer - " + filePath;
            var build = XmlLogReader.ReadFromXml(filePath);
            mainContent.Content = new BuildControl(build);
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}

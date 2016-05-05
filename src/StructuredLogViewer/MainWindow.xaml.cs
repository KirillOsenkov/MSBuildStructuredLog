using System;
using System.Windows;
using Microsoft.Build.Logging.StructuredLogger;
using Microsoft.Win32;
using StructuredLogViewer.Controls;

namespace StructuredLogViewer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            var uri = new Uri("StructuredLogViewer;component/themes/Generic.xaml", UriKind.Relative);
            var generic = (ResourceDictionary)Application.LoadComponent(uri);
            Application.Current.Resources.MergedDictionaries.Add(generic);
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
            var build = LogReader.ReadLog(filePath);
            mainContent.Content = new BuildControl(build);
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}

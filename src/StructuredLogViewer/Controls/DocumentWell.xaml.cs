using System;
using System.Windows;
using System.Windows.Controls;

namespace StructuredLogViewer.Controls
{
    public partial class DocumentWell : UserControl
    {
        public DocumentWell()
        {
            InitializeComponent();
        }

        public void DisplaySource(string sourceFilePath, string text)
        {
            Visibility = Visibility.Visible;
            textViewerControl.DisplaySource(sourceFilePath, text);
        }

        private void closeButton_Click(object sender, RoutedEventArgs e)
        {
            Visibility = Visibility.Collapsed;
        }
    }
}

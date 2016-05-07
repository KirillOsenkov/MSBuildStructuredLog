using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Build.Logging.StructuredLogger;

namespace StructuredLogViewer.Controls
{
    public partial class BuildControl : UserControl
    {
        public BuildControl(Build build)
        {
            InitializeComponent();
            DataContext = build;
            Build = build;
        }

        public Build Build { get; set; }

        private void searchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = searchTextBox.Text;
            if (string.IsNullOrWhiteSpace(searchText))
            {
                return;
            }

            Search(searchText);
        }

        private void Search(string searchText)
        {
            var tree = treeView;
        }

        private void TreeViewItem_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs args)
        {
            // prevent the annoying horizontal scrolling
            args.Handled = true;
        }
    }
}

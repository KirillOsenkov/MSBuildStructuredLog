using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace StructuredLogViewer.Controls
{
    public partial class DocumentWell : UserControl
    {
        private ICollectionView tabsView;

        public DocumentWell()
        {
            InitializeComponent();
            tabControl.ItemsSource = Tabs;
            tabsView = CollectionViewSource.GetDefaultView(Tabs);
            Tabs.CollectionChanged += Tabs_CollectionChanged;
        }

        private void Tabs_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            Visibility = Tabs.Any() ? Visibility.Visible : Visibility.Collapsed;
        }

        public ObservableCollection<SourceFileTab> Tabs { get; } = new ObservableCollection<SourceFileTab>();

        public SourceFileTab Find(string filePath)
        {
            return Tabs.FirstOrDefault(t => string.Equals(t.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
        }

        public void DisplaySource(string sourceFilePath, string text)
        {
            var existing = Find(sourceFilePath);
            if (existing != null)
            {
                tabControl.SelectedItem = existing;
            }

            var textViewerControl = new TextViewerControl();
            textViewerControl.DisplaySource(sourceFilePath, text);
            var tab = new SourceFileTab()
            {
                FilePath = sourceFilePath,
                Text = text,
                Content = textViewerControl,
            };
            var header = new SourceFileTabHeader(tab);
            tab.Header = header;
            header.CloseRequested += t => Tabs.Remove(t);
            tab.HeaderTemplate = (DataTemplate)Application.Current.Resources["SourceFileTabHeaderTemplate"];

            Tabs.Add(tab);
            tabControl.SelectedItem = tab;
        }

        private void closeButton_Click(object sender, RoutedEventArgs e)
        {
            Tabs.Clear();
        }
    }
}

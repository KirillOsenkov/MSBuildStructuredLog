using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

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

            var existingStyle = Application.Current.FindResource(typeof(TabItem));
            var style = new Style(typeof(TabItem), (Style)existingStyle);
            style.Setters.Add(new EventSetter(MouseDownEvent, (MouseButtonEventHandler)OnMouseDownEvent));

            tabControl.ItemContainerStyle = style;
        }

        private void OnMouseDownEvent(object sender, MouseButtonEventArgs args)
        {
            if (args.MiddleButton == MouseButtonState.Pressed && sender is TabItem sourceFileTab)
            {
                Tabs.Remove(sourceFileTab);
            }
        }

        private void Tabs_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            Visibility = Tabs.Any() ? Visibility.Visible : Visibility.Collapsed;
        }

        public ObservableCollection<TabItem> Tabs { get; } = new ObservableCollection<TabItem>();

        public TabItem Find(string filePath)
        {
            return Tabs.FirstOrDefault(t => t.Tag is SourceFileTab s && string.Equals(s.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
        }

        public void CloseAllTabs()
        {
            Tabs.Clear();
        }

        public void Hide()
        {
            Visibility = Visibility.Collapsed;
        }

        public void DisplaySource(
            string sourceFilePath,
            string text,
            int lineNumber = 0,
            int column = 0,
            Action preprocess = null,
            bool displayPath = true)
        {
            var existing = Find(sourceFilePath);
            if (existing != null)
            {
                Visibility = Visibility.Visible;
                tabControl.SelectedItem = existing;
                var textViewer = existing.Content as TextViewerControl;
                if (textViewer != null)
                {
                    textViewer.SetPathDisplay(displayPath);

                    if (textViewer.Text != text)
                    {
                        textViewer.SetText(text);
                    }

                    textViewer.DisplaySource(lineNumber, column);
                }

                return;
            }

            var textViewerControl = new TextViewerControl();
            textViewerControl.DisplaySource(sourceFilePath, text, lineNumber, column, preprocess);
            var tab = new SourceFileTab()
            {
                FilePath = sourceFilePath,
                Text = text
            };
            var tabItem = new TabItem()
            {
                Tag = tab,
                Content = textViewerControl
            };
            var header = new SourceFileTabHeader(tab);
            tabItem.Header = header;
            header.CloseRequested += t =>
            {
                var tabItem = Tabs.FirstOrDefault(tabItem => tabItem.Tag == t);
                if (tabItem != null)
                {
                    Tabs.Remove(tabItem);
                }
            };
            tabItem.HeaderTemplate = (DataTemplate)Application.Current.Resources["SourceFileTabHeaderTemplate"];
            textViewerControl.SetPathDisplay(displayPath);

            Tabs.Add(tabItem);
            tabControl.SelectedItem = tabItem;
        }

        private void closeButton_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }
    }
}

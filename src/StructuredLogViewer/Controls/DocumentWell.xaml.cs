using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace StructuredLogViewer.Controls
{
    public partial class DocumentWell : UserControl
    {
        public DocumentWell()
        {
            InitializeComponent();
            tabControl.ItemsSource = Tabs;
            Tabs.CollectionChanged += Tabs_CollectionChanged;

            ContextMenu tabContextMenu = new ContextMenu();
            var closeMenuItem = new MenuItem() { Header = "Close" };
            closeMenuItem.Click += (s, e) =>
            {
                if (tabContextMenu.PlacementTarget is TabItem tabItem)
                {
                    Tabs.Remove(tabItem);
                }
            };
            tabContextMenu.AddItem(closeMenuItem);

            var closeAllButThisMenuItem = new MenuItem() { Header = "Close all but this" };
            closeAllButThisMenuItem.Click += (s, e) =>
            {
                if (tabContextMenu.PlacementTarget is TabItem tabItem)
                {
                    CloseAllButThis(tabItem.Tag as SourceFileTab);
                }
            };
            tabContextMenu.AddItem(closeAllButThisMenuItem);

            var closeAllMenuItem = new MenuItem() { Header = "Close all" };
            closeAllMenuItem.Click += (s, e) =>
            {
                CloseAllTabs();
            };
            tabContextMenu.AddItem(closeAllMenuItem);

            var existingStyle = Application.Current.FindResource(typeof(TabItem));
            var style = new Style(typeof(TabItem), (Style)existingStyle);
            style.Setters.Add(new EventSetter(MouseDownEvent, (MouseButtonEventHandler)OnMouseDownEvent));
            style.Setters.Add(new Setter(ContextMenuProperty, tabContextMenu));

            tabControl.ItemContainerStyle = style;
        }

        public void Dispose()
        {
            tabControl.ItemContainerStyle = null;
            Tabs.CollectionChanged -= Tabs_CollectionChanged;
            tabControl.ItemsSource = null;
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
            NavigationHelper navigationHelper = null,
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
            textViewerControl.DisplaySource(sourceFilePath, text, lineNumber, column, preprocess, navigationHelper);
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
                CloseTab(t);
            };
            tabItem.HeaderTemplate = (DataTemplate)Application.Current.Resources["SourceFileTabHeaderTemplate"];
            textViewerControl.SetPathDisplay(displayPath);

            Tabs.Add(tabItem);
            tabControl.SelectedItem = tabItem;
        }

        private void CloseTab(SourceFileTab sourceFileTab)
        {
            var tabItem = Tabs.FirstOrDefault(tabItem => tabItem.Tag == sourceFileTab);
            if (tabItem != null)
            {
                Tabs.Remove(tabItem);
            }
        }

        private void CloseAllButThis(SourceFileTab sourceFileTab)
        {
            foreach (var tab in Tabs.ToArray())
            {
                if (tab.Tag != sourceFileTab)
                {
                    Tabs.Remove(tab);
                }
            }
        }

        private void closeButton_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }
    }
}

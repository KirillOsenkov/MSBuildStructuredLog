﻿using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Build.Logging.StructuredLogger;

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

        public TabItem Find(string filePath, int hash)
        {
            return Tabs.FirstOrDefault(t => t.Tag is SourceFileTab s && string.Equals(s.FilePath, filePath, StringComparison.OrdinalIgnoreCase) && (hash == 0 || hash == s.HashCode));
        }

        public void CloseAllTabs()
        {
            Tabs.Clear();
        }

        public void Hide()
        {
            Visibility = Visibility.Collapsed;
        }

        public void DisplayCommandLineDiffer(
            string sourceFilePath,
            string commandLineText)
        {
            var cmdDiff = new CommandLineDiffControl();
            cmdDiff.Initialize(commandLineText);

            var tab = new SourceFileTab()
            {
                FilePath = sourceFilePath,
                Text = commandLineText,
                HashCode = TextUtilities.GetHashCode(sourceFilePath, commandLineText),
            };

            AttachToTab(cmdDiff, tab);
        }

        public void DisplaySource(
            string sourceFilePath,
            string text,
            int lineNumber = 0,
            int column = 0,
            string? actionName = null,
            string? actionToolTip = null,
            Action action = null,
            NavigationHelper navigationHelper = null,
            EditorExtension editorExtension = null,
            bool displayPath = true,
            int tabHash = 0)
        {
            var existing = Find(sourceFilePath, tabHash);
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
            textViewerControl.DisplaySource(sourceFilePath, text, lineNumber, column, actionName, actionToolTip, action, navigationHelper);
            textViewerControl.SetPathDisplay(displayPath);

            if (editorExtension != null)
            {
                editorExtension.Install(textViewerControl);
            }

            var tab = new SourceFileTab()
            {
                FilePath = sourceFilePath,
                Text = text,
                HashCode = tabHash,
            };

            AttachToTab(textViewerControl, tab);
        }

        private void AttachToTab(UserControl textViewerControl, SourceFileTab tab)
        {
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

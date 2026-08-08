using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace StructuredLogViewer.Avalonia.Controls
{
    public class DocumentWell : UserControl
    {
        private TabControl tabControl;
        private Button closeButton;
        private SourceFileTab contextMenuTab;

        public DocumentWell()
        {
            InitializeComponent();

            Tabs.CollectionChanged += Tabs_CollectionChanged;

            var tabContextMenu = new ContextMenu();
            var closeMenuItem = new MenuItem() { Header = "Close" };
            closeMenuItem.Click += (s, e) =>
            {
                if (contextMenuTab != null)
                {
                    Tabs.Remove(contextMenuTab);
                }
            };
            tabContextMenu.AddItem(closeMenuItem);

            var closeAllButThisMenuItem = new MenuItem() { Header = "Close all but this" };
            closeAllButThisMenuItem.Click += (s, e) =>
            {
                if (contextMenuTab != null)
                {
                    CloseAllButThis(contextMenuTab);
                }
            };
            tabContextMenu.AddItem(closeAllButThisMenuItem);

            var closeAllMenuItem = new MenuItem() { Header = "Close all" };
            closeAllMenuItem.Click += (s, e) => CloseAllTabs();
            tabContextMenu.AddItem(closeAllMenuItem);

            var tabItemStyle = new global::Avalonia.Styling.Style(x => x.OfType<TabItem>());
            tabItemStyle.Setters.Add(new global::Avalonia.Styling.Setter(ContextMenuProperty, tabContextMenu));
            tabControl.Styles.Add(tabItemStyle);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            this.RegisterControl(out tabControl, nameof(tabControl));
            this.RegisterControl(out closeButton, nameof(closeButton));

            closeButton.Click += closeButton_Click;
            tabControl.PointerPressed += TabControlOnPointerPressed;
            tabControl.AddHandler(ContextRequestedEvent, TabControlOnContextRequested, RoutingStrategies.Tunnel);
        }

        public void Dispose()
        {
            Tabs.CollectionChanged -= Tabs_CollectionChanged;
            CloseAllTabs();
        }

        private void Tabs_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            IsVisible = Tabs.Any();
        }

        public ObservableCollection<SourceFileTab> Tabs { get; } = new ObservableCollection<SourceFileTab>();

        public SourceFileTab Find(string filePath)
        {
            return Tabs.FirstOrDefault(t => string.Equals(t.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
        }

        public void CloseAllTabs()
        {
            Tabs.Clear();
        }

        public void Hide()
        {
            IsVisible = false;
        }

        public TextViewerControl SelectedTextViewer => (tabControl.SelectedItem as SourceFileTab)?.Content;

        public void DisplaySource(
            string sourceFilePath,
            string text,
            int lineNumber = 0,
            int column = 0,
            Action preprocess = null,
            NavigationHelper navigationHelper = null,
            EditorExtension editorExtension = null,
            bool displayPath = true)
        {
            var existing = Find(sourceFilePath);
            if (existing != null)
            {
                IsVisible = true;
                tabControl.SelectedItem = existing;
                var textViewer = existing.Content;
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

            if (editorExtension != null)
            {
                editorExtension.Install(textViewerControl);
            }

            var tab = new SourceFileTab
            {
                FilePath = sourceFilePath,
                Content = textViewerControl,
            };
            tab.CloseRequested += t => Tabs.Remove(t);
            textViewerControl.SetPathDisplay(displayPath);

            Tabs.Add(tab);
            tabControl.SelectedItem = tab;
        }

        private void CloseAllButThis(SourceFileTab sourceFileTab)
        {
            foreach (var tab in Tabs.ToArray())
            {
                if (tab != sourceFileTab)
                {
                    Tabs.Remove(tab);
                }
            }
        }

        private void closeButton_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        private void TabControlOnPointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (e.Handled)
                return;

            if (!e.GetCurrentPoint(tabControl).Properties.IsMiddleButtonPressed)
                return;

            if (FindTab(e.Source) is { } sourceFileTab)
            {
                sourceFileTab.Close.Execute(null);
            }
        }

        private void TabControlOnContextRequested(object sender, ContextRequestedEventArgs e)
        {
            // resolve the tab under the cursor right before the context menu opens,
            // so the menu always acts on the tab it was invoked on (or on none)
            contextMenuTab = FindTab(e.Source);
        }

        private static SourceFileTab FindTab(object source)
        {
            var current = source as Visual;

            while (current != null)
            {
                if (current is TabItem { DataContext: SourceFileTab sourceFileTab })
                {
                    return sourceFileTab;
                }

                current = current.GetVisualParent();
            }

            return null;
        }
    }
}

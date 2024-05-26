using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace StructuredLogViewer.Avalonia.Controls
{
    public class DocumentWell : UserControl
    {
        private TabControl tabControl;
        private Button closeButton;

        public DocumentWell()
        {
            InitializeComponent();

            Tabs.CollectionChanged += Tabs_CollectionChanged;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            this.RegisterControl(out tabControl, nameof(tabControl));
            this.RegisterControl(out closeButton, nameof(closeButton));

            closeButton.Click += closeButton_Click;
            tabControl.PointerPressed += TabControlOnPointerPressed;
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

        private void closeButton_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        private void TabControlOnPointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (e.Handled)
                return;

            var current = e.Source as Visual;

            while (current != null)
            {
                if (current is TabItem { DataContext: SourceFileTab sourceFileTab })
                {
                    sourceFileTab.Close.Execute(null);
                    break;
                }

                current = current.GetVisualParent();
            }
        }
    }
}

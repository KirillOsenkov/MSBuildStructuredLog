using Avalonia.Controls;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Avalonia.Markup.Xaml;
using Avalonia.Interactivity;

namespace StructuredLogViewer.Avalonia.Controls
{
    public partial class DocumentWell : UserControl
    {
        private TabItemsControl tabControl;
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
        }

        private void Tabs_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            IsVisible = Tabs.Any();

            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                tabControl.Tabs.Clear();
                return;
            }

            if (e.OldItems != null)
            {
                foreach (TabItem item in e.OldItems)
                {
                    tabControl.Tabs.Remove(item);
                }
            }

            if (e.NewItems != null)
            {
                foreach (TabItem item in e.NewItems)
                {
                    tabControl.Tabs.Add(item);
                }
            }
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

        public void DisplaySource(string sourceFilePath, string text, int lineNumber = 0, int column = 0, Action preprocess = null, bool displayPath = true)
        {
            var existing = Find(sourceFilePath);
            if (existing != null)
            {
                IsVisible = true;
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
                Text = text,
                Content = textViewerControl,
            };
            var header = new SourceFileTabHeader(tab);
            tab.Header = header;
            header.CloseRequested += t => Tabs.Remove(t);
            // TODO: template
            //tab.HeaderTemplate = (DataTemplate)Application.Current.Resources["SourceFileTabHeaderTemplate"];
            textViewerControl.SetPathDisplay(displayPath);

            Tabs.Add(tab);
            tabControl.SelectedItem = tab;
        }

        private void closeButton_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }
    }
}

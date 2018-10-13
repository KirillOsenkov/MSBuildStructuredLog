using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using System;
using System.Collections;
using Avalonia;
using Avalonia.Controls.Presenters;

namespace StructuredLogViewer.Avalonia.Controls
{
    public partial class SearchAndResultsControl : UserControl
    {
        private TypingConcurrentOperation typingConcurrentOperation = new TypingConcurrentOperation();
        internal TextBox searchTextBox;
        private TreeView resultsList;
        private ContentPresenter watermark;
        private Button clearSearchButton;

        public SearchAndResultsControl()
        {
            InitializeComponent();
            typingConcurrentOperation.DisplayResults += (r, more) => Dispatcher.UIThread.InvokeAsync(() => DisplaySearchResults(r));
            typingConcurrentOperation.SearchComplete += (text, arg, elapsed) => Dispatcher.UIThread.InvokeAsync(() => TypingConcurrentOperation_SearchComplete(text, arg));
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            this.RegisterControl(out searchTextBox, nameof(searchTextBox));
            this.RegisterControl(out resultsList, nameof(resultsList));
            this.RegisterControl(out watermark, nameof(watermark));
            this.RegisterControl(out clearSearchButton, nameof(clearSearchButton));

            searchTextBox.PropertyChanged += searchTextBox_TextChanged;
            clearSearchButton.Click += clearSearchButton_Click;
        }

        private void TypingConcurrentOperation_SearchComplete(string searchText, object arg2)
        {
            SettingsService.AddRecentSearchText(searchText, discardPrefixes: true);
        }

        public TreeView ResultsList => resultsList;
        public Func<object, IEnumerable> ResultsTreeBuilder { get; set; }
        public event Action WatermarkDisplayed;

        public Func<string, int, object> ExecuteSearch
        {
            get => typingConcurrentOperation.ExecuteSearch;
            set => typingConcurrentOperation.ExecuteSearch = value;
        }

        private void searchTextBox_TextChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property != TextBox.TextProperty) return;

            var searchText = searchTextBox.Text;
            if (string.IsNullOrWhiteSpace(searchText) || searchText.Length < 3)
            {
                typingConcurrentOperation.Reset();
                DisplaySearchResults(null);
                return;
            }

            typingConcurrentOperation.TextChanged(searchText);
        }

        private void DisplaySearchResults(object results)
        {
            if (results == null)
            {
                watermark.IsVisible = true;
                WatermarkDisplayed?.Invoke();
            }
            else
            {
                watermark.IsVisible = false;
            }

            resultsList.Items = ResultsTreeBuilder(results);
        }

        public object WatermarkContent
        {
            get => watermark.Content;
            set { watermark.Content = value; }
        }

        public string SearchText
        {
            get => searchTextBox.Text;

            set
            {
                searchTextBox.Text = value;
                searchTextBox.CaretIndex = value.Length;
                searchTextBox.Focus();
            }
        }

        private void clearSearchButton_Click(object sender, RoutedEventArgs e)
        {
            SearchText = "";
        }
    }
}

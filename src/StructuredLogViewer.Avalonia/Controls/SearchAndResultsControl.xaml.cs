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
            typingConcurrentOperation.DisplayResults += (r, moreAvailable) => Dispatcher.UIThread.InvokeAsync(() => DisplaySearchResults(r, moreAvailable));
            typingConcurrentOperation.SearchComplete += (text, arg, elapsed) => Dispatcher.UIThread.InvokeAsync(() => TypingConcurrentOperation_SearchComplete(text, arg, elapsed));
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

        private void TypingConcurrentOperation_SearchComplete(string searchText, object arg2, TimeSpan elapsed)
        {
            BuildControl.Elapsed = elapsed;
            SettingsService.AddRecentSearchText(searchText, discardPrefixes: true);
        }

        public TreeView ResultsList => resultsList;
        public Func<object, bool, IEnumerable> ResultsTreeBuilder { get; set; }
        public event Action WatermarkDisplayed;

        public ExecuteSearchFunc ExecuteSearch
        {
            get => typingConcurrentOperation.ExecuteSearch;
            set => typingConcurrentOperation.ExecuteSearch = value;
        }
        
        public void TriggerSearch(string text, int maxResults)
        {
            typingConcurrentOperation.TriggerSearch(text, maxResults);
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

            typingConcurrentOperation.TextChanged(searchText, Search.DefaultMaxResults);
        }

        private void DisplaySearchResults(object results, bool moreAvailable = false)
        {
            if (results == null)
            {
                if (!watermark.IsVisible)
                {
                    watermark.IsVisible = true;
                    WatermarkDisplayed?.Invoke();
                }
            }
            else
            {
                watermark.IsVisible = false;
            }

            resultsList.Items = ResultsTreeBuilder(results, moreAvailable);
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

using System;
using System.Collections;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace StructuredLogViewer.Controls
{
    public partial class SearchAndResultsControl : UserControl
    {
        private readonly TypingConcurrentOperation typingConcurrentOperation = new TypingConcurrentOperation();

        public SearchAndResultsControl()
        {
            InitializeComponent();
            typingConcurrentOperation.DisplayResults += DisplaySearchResults;
            typingConcurrentOperation.SearchComplete += TypingConcurrentOperation_SearchComplete;

            VirtualizingPanel.SetIsVirtualizing(resultsList, SettingsService.EnableTreeViewVirtualization);

            this.Unloaded += SearchAndResultsControl_Unloaded;
        }

        public void Dispose()
        {
            this.Unloaded -= SearchAndResultsControl_Unloaded;
            typingConcurrentOperation.DisplayResults -= DisplaySearchResults;
            typingConcurrentOperation.SearchComplete -= TypingConcurrentOperation_SearchComplete;
            this.clearSearchButton.Click -= clearSearchButton_Click;
            this.resultsList.ItemsSource = null;
        }

        private void SearchAndResultsControl_Unloaded(object sender, RoutedEventArgs e)
        {
            typingConcurrentOperation.ReleaseTimer();
        }

        private void TypingConcurrentOperation_SearchComplete(string searchText, object arg2, TimeSpan elapsed)
        {
            BuildControl.Elapsed = elapsed;

            // text copied from another tab is not added to the recent searches of this tab
            if (searchText != copiedTextInSearchBox)
            {
                SettingsService.AddRecentSearchText(searchText, discardPrefixes: true, RecentItemsCategory);
            }
        }

        public string RecentItemsCategory { get; set; }

        // the text most recently copied into the search box by CopySearchText
        private string lastCopiedText;

        // same as lastCopiedText, until the text in the search box is changed
        private string copiedTextInSearchBox;

        public bool IsSearchBoxVisible => topPanel.Visibility == Visibility.Visible;

        /// <summary>
        /// Sets the search text to the text of the search tab that was selected before this one.
        /// Nothing is copied if the search box is hidden or already has text. The same text is
        /// copied only once, so a search box that the user cleared is not filled again.
        /// </summary>
        public void CopySearchText(string text)
        {
            var trimmed = text?.Trim();
            if (string.IsNullOrEmpty(trimmed) ||
                trimmed == lastCopiedText ||
                !IsSearchBoxVisible ||
                !string.IsNullOrWhiteSpace(SearchText))
            {
                return;
            }

            lastCopiedText = trimmed;
            copiedTextInSearchBox = trimmed;
            SearchText = text;
        }

        public TreeView ResultsList => resultsList;
        public Func<object, bool, IEnumerable> ResultsTreeBuilder { get; set; }
        public event Action WatermarkDisplayed;
        public event Action<string> TextChanged;

        public Grid TopPanel => topPanel;

        public ExecuteSearchFunc ExecuteSearch
        {
            get => typingConcurrentOperation.ExecuteSearch;
            set => typingConcurrentOperation.ExecuteSearch = value;
        }

        public void RetriggerSearch()
        {
            typingConcurrentOperation.TriggerSearch(SearchText, Search.DefaultMaxResults);
        }

        public void TriggerSearch(string text, int maxResults)
        {
            typingConcurrentOperation.TriggerSearch(text, maxResults);
        }

        private void searchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = searchTextBox.Text.Trim();
            if (searchText != copiedTextInSearchBox)
            {
                copiedTextInSearchBox = null;
            }

            TextChanged?.Invoke(searchText);

            if (string.IsNullOrWhiteSpace(searchText) || searchText.Length < 3)
            {
                typingConcurrentOperation.Reset();

                // only clear the contents when we have a search function defined.
                // if the text input is handled externally, don't mess with the 
                // content
                if (ExecuteSearch != null)
                {
                    DisplaySearchResults(null);
                }

                return;
            }

            typingConcurrentOperation.TextChanged(searchText, Search.DefaultMaxResults);
        }

        private void DisplaySearchResults(object results, bool moreAvailable = false, CancellationToken cancellationToken = default)
        {
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                var tree = ResultsTreeBuilder(results, moreAvailable);
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                DisplayItems(tree);
            }, System.Windows.Threading.DispatcherPriority.Background);
        }

        public void DisplayItems(IEnumerable content)
        {
            if ((content == null || !content.OfType<object>().Any()) && WatermarkContent != null)
            {
                if (watermarkScrollViewer.Visibility != Visibility.Visible)
                {
                    watermarkScrollViewer.Visibility = Visibility.Visible;
                    WatermarkDisplayed?.Invoke();
                }
            }
            else
            {
                watermarkScrollViewer.Visibility = Visibility.Collapsed;
            }

            resultsList.ItemsSource = content;
        }

        public object WatermarkContent
        {
            get => watermark.Content;

            set
            {
                watermark.Content = value;
            }
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

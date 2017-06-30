using System;
using System.Collections;
using System.Windows;
using System.Windows.Controls;

namespace StructuredLogViewer.Controls
{
    public partial class SearchAndResultsControl : UserControl
    {
        private TypingConcurrentOperation typingConcurrentOperation = new TypingConcurrentOperation();

        public SearchAndResultsControl()
        {
            InitializeComponent();
            typingConcurrentOperation.DisplayResults += r => DisplaySearchResults(r);
            typingConcurrentOperation.SearchComplete += TypingConcurrentOperation_SearchComplete;
        }

        private void TypingConcurrentOperation_SearchComplete(string searchText, object arg2)
        {
            SettingsService.AddRecentSearchText(searchText, discardPrefixes: true);
        }

        public TreeView ResultsList => resultsList;
        public Func<object, IEnumerable> ResultsTreeBuilder { get; set; }
        public event Action WatermarkDisplayed;

        public Func<string, object> ExecuteSearch
        {
            get => typingConcurrentOperation.ExecuteSearch;
            set => typingConcurrentOperation.ExecuteSearch = value;
        }

        private void searchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
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
                watermark.Visibility = Visibility.Visible;
                WatermarkDisplayed?.Invoke();
            }
            else
            {
                watermark.Visibility = Visibility.Collapsed;
            }

            resultsList.ItemsSource = ResultsTreeBuilder(results);
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

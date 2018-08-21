﻿using System;
using System.Collections;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using StructuredLogViewer.Core;

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
        }

        private void TypingConcurrentOperation_SearchComplete(string searchText, object arg2)
        {
            SettingsService.AddRecentSearchText(searchText, discardPrefixes: true);
        }

        public TreeView ResultsList => resultsList;
        public Func<object, bool, IEnumerable> ResultsTreeBuilder { get; set; }
        public event Action WatermarkDisplayed;
        public event Action<string> TextChanged;

        public Func<string, int, object> ExecuteSearch
        {
            get => typingConcurrentOperation.ExecuteSearch;
            set => typingConcurrentOperation.ExecuteSearch = value;
        }

        public void TriggerSearch(string text, int maxResults)
        {
            typingConcurrentOperation.TextChanged(text, maxResults);
        }

        private void searchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = searchTextBox.Text;
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

            typingConcurrentOperation.TextChanged(searchText);
        }

        private void DisplaySearchResults(object results, bool moreAvailable = false)
        {
            DisplayItems(ResultsTreeBuilder(results, moreAvailable));
        }

        public void DisplayItems(IEnumerable content)
        {
            if ((content == null || !content.OfType<object>().Any()) && WatermarkContent != null)
            {
                watermark.Visibility = Visibility.Visible;
                WatermarkDisplayed?.Invoke();
            }
            else
            {
                watermark.Visibility = Visibility.Collapsed;
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

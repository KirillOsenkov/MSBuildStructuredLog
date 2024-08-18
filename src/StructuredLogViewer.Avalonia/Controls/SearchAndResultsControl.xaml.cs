using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using System;
using System.Collections;
using System.Threading;
using Avalonia;
using Avalonia.Controls.Presenters;
using System.Linq;

namespace StructuredLogViewer.Avalonia.Controls
{
    public partial class SearchAndResultsControl : UserControl
    {
        private readonly TypingConcurrentOperation typingConcurrentOperation = new TypingConcurrentOperation();
        internal TextBox searchTextBox;
        private TreeView resultsList;
        private ContentPresenter watermark;
        private ScrollViewer watermarkScrollViewer;
        private Button clearSearchButton;
        private Grid topPanel;

        public SearchAndResultsControl()
        {
            InitializeComponent();
            typingConcurrentOperation.DisplayResults += (r, moreAvailable, cancellationToken) => Dispatcher.UIThread.InvokeAsync(() => DisplaySearchResults(r, moreAvailable, cancellationToken));
            typingConcurrentOperation.SearchComplete += (text, arg, elapsed) => Dispatcher.UIThread.InvokeAsync(() => TypingConcurrentOperation_SearchComplete(text, arg, elapsed));
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            typingConcurrentOperation.ReleaseTimer();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            this.RegisterControl(out searchTextBox, nameof(searchTextBox));
            this.RegisterControl(out resultsList, nameof(resultsList));
            this.RegisterControl(out watermark, nameof(watermark));
            this.RegisterControl(out clearSearchButton, nameof(clearSearchButton));
            this.RegisterControl(out watermarkScrollViewer, nameof(watermarkScrollViewer));
            this.RegisterControl(out topPanel, nameof(topPanel));

            searchTextBox.PropertyChanged += searchTextBox_TextChanged;
            clearSearchButton.Click += clearSearchButton_Click;
        }

        private void TypingConcurrentOperation_SearchComplete(string searchText, object arg2, TimeSpan elapsed)
        {
            BuildControl.Elapsed = elapsed;
            SettingsService.AddRecentSearchText(searchText, discardPrefixes: true, RecentItemsCategory);
        }

        public string RecentItemsCategory { get; set; }

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

        public void TriggerSearch(string text, int maxResults)
        {
            typingConcurrentOperation.TriggerSearch(text, maxResults);
        }
        
        private void searchTextBox_TextChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property != TextBox.TextProperty) return;

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

            typingConcurrentOperation.TextChanged(searchText, Search.DefaultMaxResults);
        }

        private void DisplaySearchResults(object results, bool moreAvailable = false, CancellationToken cancellationToken = default)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
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
            }, DispatcherPriority.Background);
        }

        public void DisplayItems(IEnumerable content)
        {
            if ((content == null || !content.OfType<object>().Any()) && WatermarkContent != null)
            {
                if (!watermarkScrollViewer.IsVisible)
                {
                    watermarkScrollViewer.IsVisible = true;
                    WatermarkDisplayed?.Invoke();
                }
            }
            else
            {
                watermarkScrollViewer.IsVisible = false;
            }

            resultsList.ItemsSource = content;
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

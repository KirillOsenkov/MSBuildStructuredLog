using System;
using System.Diagnostics;
using System.Windows;
using StructuredLogViewer.Controls;

using StructuredLogViewer.Core;

using TPLTask = System.Threading.Tasks.Task;

namespace StructuredLogViewer
{
    public class TypingConcurrentOperation
    {
        public Func<string, int, object> ExecuteSearch;
        public event Action<object, bool> DisplayResults;
        public event Action<string, object> SearchComplete;

        public const int ThrottlingDelayMilliseconds = 300;

        public void Reset()
        {
            latestSearch = null;
        }

        private string latestSearch;

        public void TextChanged(string searchText, int maxResults = Search.DefaultMaxResults)
        {
            if (ExecuteSearch == null)
            {
                return;
            }

            latestSearch = searchText;
            TPLTask.Delay(ThrottlingDelayMilliseconds).ContinueWith(_ =>
            {
                if (latestSearch == searchText)
                {
                    StartOperation(searchText, maxResults);
                }
            });
        }

        private void StartOperation(string searchText, int maxResults = Search.DefaultMaxResults)
        {
            Stopwatch sw = Stopwatch.StartNew();
            var results = ExecuteSearch(searchText, maxResults);
            bool moreAvailable = results is System.Collections.ICollection collection && collection.Count >= maxResults;
            var elapsed = sw.Elapsed;
            BuildControl.Elapsed = elapsed;
            if (latestSearch == searchText)
            {
                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    DisplayResults?.Invoke(results, moreAvailable);
                    SearchComplete?.Invoke(searchText, results);
                });
            }
        }
    }
}

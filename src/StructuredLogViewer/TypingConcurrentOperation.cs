using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using Microsoft.Build.Logging.StructuredLogger;
using StructuredLogViewer.Controls;
using TPLTask = System.Threading.Tasks.Task;

namespace StructuredLogViewer
{
    public class TypingConcurrentOperation
    {
        public Build Build { get; internal set; }

        public event Action<IEnumerable<SearchResult>> DisplayResults;

        public const int ThrottlingDelayMilliseconds = 300;

        public void Reset()
        {
            latestSearch = null;
        }

        private string latestSearch;

        public void TextChanged(string searchText)
        {
            latestSearch = searchText;
            TPLTask.Delay(ThrottlingDelayMilliseconds).ContinueWith(_ =>
            {
                if (latestSearch == searchText)
                {
                    StartOperation(searchText);
                }
            });
        }

        private void StartOperation(string searchText)
        {
            Stopwatch sw = Stopwatch.StartNew();
            var search = new Search(Build);
            var results = search.FindNodes(searchText);
            var elapsed = sw.Elapsed;
            BuildControl.Elapsed = elapsed;
            if (latestSearch == searchText)
            {
                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    DisplayResults?.Invoke(results);
                });
            }
        }
    }
}

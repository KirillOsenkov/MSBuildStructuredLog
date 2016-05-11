using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using Microsoft.Build.Logging.StructuredLogger;
using TPLTask = System.Threading.Tasks.Task;

namespace StructuredLogViewer
{
    public class TypingConcurrentOperation
    {
        public Build Build { get; internal set; }

        public event Action<IEnumerable<object>> DisplayResults;

        public const int ThrottlingDelayMilliseconds = 200;

        public void Reset()
        {
            latestSearch = null;
        }

        private string latestSearch;

        public void TextChanged(string searchText)
        {
            latestSearch = searchText;
            TPLTask.Delay(300).ContinueWith(_ =>
            {
                if (latestSearch == searchText)
                {
                    StartOperation(searchText);
                }
            });
        }

        private void StartOperation(string searchText)
        {
            var search = new Search(Build);
            Stopwatch sw = Stopwatch.StartNew();
            var results = search.FindNodes(searchText);
            results = new TreeNode[] { new Message { Text = "Search took " + sw.Elapsed.ToString() } }.Concat(results);
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

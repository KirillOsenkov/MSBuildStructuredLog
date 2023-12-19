using System;
using System.Diagnostics;
using System.Threading;
using TPLTask = System.Threading.Tasks.Task;

namespace StructuredLogViewer
{
    public delegate object ExecuteSearchFunc(string query, int maxResults, CancellationToken cancellationToken);

    public class TypingConcurrentOperation
    {
        public ExecuteSearchFunc ExecuteSearch;
        public event Action<object, bool, CancellationToken> DisplayResults;
        public event Action<string, object, TimeSpan> SearchComplete;

        public const int ThrottlingDelayMilliseconds = 300;

        private Timer timer;
        private string searchText;
        private int maxResults;

        private CancellationTokenSource currentCancellationTokenSource;

        public void ReleaseTimer()
        {
            if (timer != null)
            {
                timer.Change(Timeout.Infinite, Timeout.Infinite);
                timer.Dispose();
                timer = null;
            }
        }

        public void Reset()
        {
            Interlocked.Exchange(ref currentCancellationTokenSource, null)?.Cancel();
            timer?.Change(Timeout.Infinite, Timeout.Infinite);
        }

        public void TextChanged(string searchText, int maxResults)
        {
            if (searchText.Equals(this.searchText))
            {
                return;
            }

            if (ExecuteSearch == null)
            {
                Reset();
                return;
            }

            Interlocked.Exchange(ref currentCancellationTokenSource, null)?.Cancel();

            this.searchText = searchText;
            this.maxResults = maxResults;

            SetTimer();
        }

        private void SetTimer()
        {
            if (timer == null)
            {
                timer = new Timer(OnTimer);
            }

            timer.Change(ThrottlingDelayMilliseconds, Timeout.Infinite);
        }

        public void TriggerSearch(string searchText, int maxResults)
        {
            Reset();

            this.searchText = searchText;
            this.maxResults = maxResults;

            TPLTask.Run(StartOperation);
        }

        private void OnTimer(object state)
        {
            TPLTask.Run(StartOperation);
        }

        private void StartOperation()
        {
            var cts = new CancellationTokenSource();
            Interlocked.Exchange(ref currentCancellationTokenSource, cts)?.Cancel();

            var localSearchText = searchText;
            var localMaxResults = maxResults;

            var sw = Stopwatch.StartNew();
            var results = ExecuteSearch(localSearchText, localMaxResults, cts.Token);
            var elapsed = sw.Elapsed;

            var moreAvailable = results is System.Collections.ICollection collection && collection.Count >= localMaxResults;

            if (!cts.Token.IsCancellationRequested)
            {
                SearchComplete?.Invoke(localSearchText, results, elapsed);
                DisplayResults?.Invoke(results, moreAvailable, cts.Token);
            }
        }
    }
}

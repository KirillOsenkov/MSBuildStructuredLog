using System;
using System.Linq;
using System.Threading;
using Microsoft.Build.Logging.StructuredLogger;
using StructuredLogViewer;
using Xunit;

namespace StructuredLogger.Tests
{
    public class SearchTests
    {
        [Fact]
        public void Search_UsesDefaultResultLimitUnlessShowAllIsEnabled()
        {
            var build = new Build();
            for (int i = 0; i < Search.DefaultMaxResults + 5; i++)
            {
                build.AddChild(new Message { Text = "needle" });
            }

            int defaultCount = new Search(
                new[] { build },
                new[] { "needle" },
                Search.GetMaxResults(Search.DefaultMaxResults, alwaysShowAllResults: false),
                markResultsInTree: false)
                .FindNodes("needle", CancellationToken.None)
                .Count();

            int showAllCount = new Search(
                new[] { build },
                new[] { "needle" },
                Search.GetMaxResults(Search.DefaultMaxResults, alwaysShowAllResults: true),
                markResultsInTree: false)
                .FindNodes("needle", CancellationToken.None)
                .Count();

            Assert.Equal(Search.DefaultMaxResults, defaultCount);
            Assert.Equal(Search.DefaultMaxResults + 5, showAllCount);
        }

        [Fact]
        public void Search_StopsWhenCancellationIsRequested()
        {
            var build = new Build();
            build.AddChild(new Message { Text = "needle" });

            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();

                Assert.Throws<OperationCanceledException>(() => new Search(
                    new[] { build },
                    new[] { "needle" },
                    int.MaxValue,
                    markResultsInTree: false)
                    .FindNodes("needle", cancellation.Token));
            }
        }
    }
}

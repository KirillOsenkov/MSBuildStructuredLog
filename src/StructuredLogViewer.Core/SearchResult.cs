using System;
using System.Collections.Generic;
using Microsoft.Build.Logging.StructuredLogger;

namespace StructuredLogViewer
{
    public class SearchResult
    {
        public object Node { get; set; }
        public Dictionary<string, List<string>> WordsInFields = new Dictionary<string, List<string>>();
        public bool MatchedByType { get; private set; }
        public TimeSpan Duration { get; set; }

        public SearchResult(object node, bool includeDuration = false)
        {
            Node = node;
            if (includeDuration && node is TimedNode timedNode)
            {
                Duration = timedNode.Duration;
            }
        }

        public void AddMatch(string field, string word)
        {
            if (!WordsInFields.TryGetValue(field, out var bucket))
            {
                bucket = new List<string>();
                WordsInFields[field] = bucket;
            }

            bucket.Add(word);
        }

        public void AddMatchByNodeType()
        {
            MatchedByType = true;
        }
    }
}

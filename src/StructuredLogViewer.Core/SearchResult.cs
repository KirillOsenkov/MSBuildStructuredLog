using System;
using System.Collections.Generic;
using Microsoft.Build.Logging.StructuredLogger;

namespace StructuredLogViewer
{
    public class SearchResult
    {
        public BaseNode Node { get; }
        public List<(string field, string match)> WordsInFields = new List<(string, string)>();

        public bool MatchedByType { get; private set; }
        public TimeSpan Duration { get; set; }

        public SearchResult(BaseNode node, bool includeDuration = false)
        {
            Node = node;
            if (includeDuration && node is TimedNode timedNode)
            {
                Duration = timedNode.Duration;
            }
        }

        public void AddMatch(string field, string word, bool addAtBeginning = false)
        {
            if (addAtBeginning)
            {
                WordsInFields.Insert(0, (field, word));
            }
            else
            {
                WordsInFields.Add((field, word));
            }
        }

        public void AddMatchByNodeType()
        {
            MatchedByType = true;
        }
    }
}

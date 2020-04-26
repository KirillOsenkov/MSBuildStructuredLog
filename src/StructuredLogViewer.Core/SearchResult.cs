using System;
using System.Collections.Generic;
using Microsoft.Build.Logging.StructuredLogger;

namespace StructuredLogViewer
{
    public class SearchResult
    {
        public BaseNode Node { get; }
        public List<(string field, string match)> WordsInFields = new List<(string, string)>();
        public List<(string highlighted, string notHighlighted)> SearchResultPair = new List<(string, string)>();
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

        public void AddMatch(string field, string word, string notHighlightedText = "", bool addAtBeginning = false)
        {
            if (addAtBeginning)
            {
                WordsInFields.Insert(0, (field, word));
            }
            else
            {
                WordsInFields.Add((field, word));
            }

            if (!string.IsNullOrEmpty(notHighlightedText))
            {
                SearchResultPair.Add((field, notHighlightedText));
            }
        }

        public void AddMatchByNodeType()
        {
            MatchedByType = true;
        }
    }
}

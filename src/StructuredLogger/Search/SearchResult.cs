using System;
using System.Collections.Generic;
using Microsoft.Build.Logging.StructuredLogger;

namespace StructuredLogViewer
{
    public class SearchResult
    {
        public BaseNode Node { get; }
        public List<(string field, string match)> WordsInFields = new List<(string, string)>();

        public IList<string> FieldsToDisplay { get; set; }

        public bool MatchedByType { get; private set; }
        public TimeSpan Duration { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        public string RootFolder { get; set; }

        public static SearchResult EmptyQueryMatch { get; } = new SearchResult();

        public SearchResult()
        {
        }

        public SearchResult(BaseNode node, bool includeDuration = false, bool includeStart = false, bool includeEnd = false)
        {
            Node = node;

            if (node is TimedNode timedNode)
            {
                if (includeDuration)
                {
                    Duration = timedNode.Duration;
                }

                if (includeStart)
                {
                    StartTime = timedNode.StartTime;
                }

                if (includeEnd)
                {
                    EndTime = timedNode.EndTime;
                }
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

        public override string ToString()
        {
            return Node?.ToString();
        }
    }
}

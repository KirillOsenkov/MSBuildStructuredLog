using System;
using System.Collections.Generic;
using Microsoft.Build.Logging.StructuredLogger;

namespace StructuredLogViewer
{
    public class Search
    {
        public const int MaxResults = 5000;

        private Build build;
        private string query;
        private List<LogProcessNode> resultSet;

        public Search(Build build)
        {
            this.build = build;
        }

        public IEnumerable<LogProcessNode> FindNodes(string query)
        {
            this.query = query;
            resultSet = new List<LogProcessNode>();
            build.VisitAllChildren<LogProcessNode>(Visit);
            return resultSet;
        }

        private void Visit(LogProcessNode node)
        {
            if (resultSet.Count > MaxResults)
            {
                return;
            }

            if (node.Name != null && node.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) != -1)
            {
                resultSet.Add(node);
            }
        }
    }
}

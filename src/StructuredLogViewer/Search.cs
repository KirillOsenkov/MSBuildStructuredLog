using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Logging.StructuredLogger;

namespace StructuredLogViewer
{
    public class Search
    {
        public const int MaxResults = 5000;

        private Build build;
        private string query;
        private List<TreeNode> resultSet;

        public Search(Build build)
        {
            this.build = build;
        }

        private static readonly char[] space = { ' ' };
        private string[] words;

        public IEnumerable<TreeNode> FindNodes(string query)
        {
            this.query = query;
            this.words = query.Split(space, StringSplitOptions.RemoveEmptyEntries);
            resultSet = new List<TreeNode>();
            build.VisitAllChildren<TreeNode>(Visit);
            return resultSet;
        }

        private void Visit(TreeNode node)
        {
            if (resultSet.Count > MaxResults)
            {
                return;
            }

            var named = node as NamedNode;
            if (named != null && named.Name != null)
            {
                if (IsMatch(named.Name))
                {
                    resultSet.Add(node);
                    return;
                }
            }
        }

        private bool IsMatch(string text)
        {
            for (int i = 0; i < words.Length; i++)
            {
                if (text.IndexOf(words[i], StringComparison.OrdinalIgnoreCase) == -1)
                {
                    return false;
                }
            }

            return true;
        }
    }
}

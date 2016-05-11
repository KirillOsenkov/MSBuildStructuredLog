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
        private List<object> resultSet;

        public Search(Build build)
        {
            this.build = build;
        }

        private static readonly char[] space = { ' ' };
        private string[] words;

        public IEnumerable<object> FindNodes(string query)
        {
            this.query = query;
            this.words = query.Split(space, StringSplitOptions.RemoveEmptyEntries);
            resultSet = new List<object>();
            build.VisitAllChildren<object>(Visit);
            return resultSet;
        }

        // avoid allocating this for every node
        private readonly List<string> searchFields = new List<string>(4);

        private void Visit(object node)
        {
            if (resultSet.Count > MaxResults)
            {
                return;
            }

            searchFields.Clear();

            var named = node as NamedNode;
            if (named != null && named.Name != null)
            {
                searchFields.Add(named.Name);
            }

            var textNode = node as TextNode;
            if (textNode != null && textNode.Text != null)
            {
                searchFields.Add(textNode.Text);
            }

            var nameValueNode = node as NameValueNode;
            if (nameValueNode != null)
            {
                searchFields.Add(nameValueNode.Name);
                searchFields.Add(nameValueNode.Value);
            }

            // in case they want to narrow down the search such as "Build target" or "Copy task"
            var typeName = node.GetType().Name;
            searchFields.Add(typeName);

            if (IsMatch(searchFields))
            {
                resultSet.Add(node);
            }
        }

        /// <summary>
        ///  Each of the query words must be found in at least one field ∀w ∃f
        /// </summary>
        private bool IsMatch(List<string> fields)
        {
            for (int i = 0; i < words.Length; i++)
            {
                bool anyFieldMatched = false;
                var word = words[i];
                for (int j = 0; j < fields.Count; j++)
                {
                    if (fields[j].IndexOf(word, StringComparison.OrdinalIgnoreCase) != -1)
                    {
                        anyFieldMatched = true;
                        break;
                    }
                }

                if (!anyFieldMatched)
                {
                    return false;
                }
            }

            return true;
        }
    }
}

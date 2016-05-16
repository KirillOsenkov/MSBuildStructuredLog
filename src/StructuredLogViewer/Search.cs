using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Logging.StructuredLogger;

namespace StructuredLogViewer
{
    public class Search
    {
        public const int MaxResults = 500;

        private Build build;
        private string query;
        private List<SearchResult> resultSet;

        public Search(Build build)
        {
            this.build = build;
        }

        private static readonly char[] space = { ' ' };
        private string[] words;

        public IEnumerable<SearchResult> FindNodes(string query)
        {
            this.query = query;
            this.words = query.Split(space, StringSplitOptions.RemoveEmptyEntries);
            resultSet = new List<SearchResult>();
            build.VisitAllChildren<object>(Visit);
            return resultSet;
        }

        // avoid allocating this for every node
        private readonly List<string> searchFields = new List<string>(5);

        private void Visit(object node)
        {
            if (resultSet.Count > MaxResults)
            {
                return;
            }

            searchFields.Clear();

            PopulateSearchFields(node, searchFields.Add);

            var result = IsMatch(searchFields);
            if (result != null)
            {
                result.Node = node;
                resultSet.Add(result);
            }
        }

        public static void PopulateSearchFields(object node, Action<string> searchFields)
        {
            var named = node as NamedNode;
            if (named != null && named.Name != null)
            {
                searchFields(named.Name);
            }

            var textNode = node as TextNode;
            if (textNode != null && textNode.Text != null)
            {
                searchFields(textNode.Text);
            }

            var nameValueNode = node as NameValueNode;
            if (nameValueNode != null)
            {
                searchFields(nameValueNode.Name);
                searchFields(nameValueNode.Value);
            }

            // in case they want to narrow down the search such as "Build target" or "Copy task"
            var typeName = node.GetType().Name;
            searchFields(typeName);
        }

        /// <summary>
        ///  Each of the query words must be found in at least one field ∀w ∃f
        /// </summary>
        private SearchResult IsMatch(List<string> fields)
        {
            SearchResult result = null;

            for (int i = 0; i < words.Length; i++)
            {
                bool anyFieldMatched = false;
                var word = words[i];
                for (int j = 0; j < fields.Count; j++)
                {
                    var field = fields[j];
                    var index = field.IndexOf(word, StringComparison.OrdinalIgnoreCase);
                    if (index != -1)
                    {
                        if (result == null)
                        {
                            result = new SearchResult();
                        }

                        // if matched on the type of the node, special case it
                        if (j == fields.Count - 1)
                        {
                            result.AddMatchByNodeType();
                        }
                        else
                        {
                            result.AddMatch(field, word, index);
                        }
                        
                        anyFieldMatched = true;
                        break;
                    }
                }

                if (!anyFieldMatched)
                {
                    return null;
                }
            }

            return result;
        }
    }
}

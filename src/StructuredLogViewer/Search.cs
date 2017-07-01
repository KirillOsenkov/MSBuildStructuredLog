using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Build.Logging.StructuredLogger;

namespace StructuredLogViewer
{
    public class Search
    {
        public const int MaxResults = 1000;

        private Build build;
        private string query;
        private List<SearchResult> resultSet;

        public Search(Build build)
        {
            this.build = build;
        }

        private static readonly char[] space = { ' ' };
        private List<string> words;
        private string typeKeyword;

        public IEnumerable<SearchResult> FindNodes(string query)
        {
            this.query = query;
            this.words = query.Split(space, StringSplitOptions.RemoveEmptyEntries).ToList();

            for (int i = words.Count - 1; i >= 0; i--)
            {
                var word = words[i];
                if (word.Length > 2 && word[0] == '$' && word[1] != '(')
                {
                    words.RemoveAt(i);
                    typeKeyword = word.Substring(1).ToLowerInvariant();
                    break;
                }
            }

            resultSet = new List<SearchResult>();

            var matchesInStrings = new HashSet<string>();

            foreach (var stringInstance in build.StringTable.Instances)
            {
                foreach (var word in words)
                {
                    if (stringInstance.IndexOf(word, StringComparison.OrdinalIgnoreCase) != -1)
                    {
                        matchesInStrings.Add(stringInstance);
                    }
                }
            }

            var cts = new CancellationTokenSource();
            build.VisitAllChildren<object>(node => Visit(node, matchesInStrings, cts), cts.Token);
            return resultSet;
        }

        // avoid allocating this for every node
        private readonly List<string> searchFields = new List<string>(6);

        private void Visit(object node, HashSet<string> stringsThatMatch, CancellationTokenSource cancellationTokenSource)
        {
            if (cancellationTokenSource.IsCancellationRequested)
            {
                return;
            }

            if (resultSet.Count > MaxResults)
            {
                cancellationTokenSource.Cancel();
                return;
            }

            searchFields.Clear();

            PopulateSearchFields(node);

            var result = IsMatch(searchFields, stringsThatMatch);
            if (result != null)
            {
                result.Node = node;
                resultSet.Add(result);
            }
        }

        public void PopulateSearchFields(object node)
        {
            // in case they want to narrow down the search such as "Build target" or "Copy task"
            var typeName = node.GetType().Name;
            searchFields.Add(typeName);

            // for tasks derived from Task $task should still work
            if (node is Task && typeName != "Task")
            {
                searchFields.Add("Task");
            }

            var named = node as NamedNode;
            if (named != null && !string.IsNullOrEmpty(named.Name))
            {
                searchFields.Add(named.Name);
            }

            var textNode = node as TextNode;
            if (textNode != null && !string.IsNullOrEmpty(textNode.Text))
            {
                searchFields.Add(textNode.Text);
            }

            var nameValueNode = node as NameValueNode;
            if (nameValueNode != null)
            {
                if (!string.IsNullOrEmpty(nameValueNode.Name))
                {
                    searchFields.Add(nameValueNode.Name);
                }

                if (!string.IsNullOrEmpty(nameValueNode.Value))
                {
                    searchFields.Add(nameValueNode.Value);
                }
            }

            var diagnostic = node as AbstractDiagnostic;
            if (diagnostic != null)
            {
                if (!string.IsNullOrEmpty(diagnostic.Code))
                {
                    searchFields.Add(diagnostic.Code);
                }

                if (!string.IsNullOrEmpty(diagnostic.File))
                {
                    searchFields.Add(diagnostic.File);
                }

                if (!string.IsNullOrEmpty(diagnostic.ProjectFile))
                {
                    searchFields.Add(diagnostic.ProjectFile);
                }
            }
        }

        /// <summary>
        ///  Each of the query words must be found in at least one field ∀w ∃f
        /// </summary>
        private SearchResult IsMatch(List<string> fields, HashSet<string> stringsThatMatch)
        {
            SearchResult result = null;

            if (typeKeyword != null)
            {
                // zeroth field is always the type
                if (string.Equals(typeKeyword, fields[0], StringComparison.OrdinalIgnoreCase) ||
                    // special case for types derived from Task, $task should still work
                    (typeKeyword == "task" && fields.Count > 1 && fields[1] == "Task"))
                {
                    // this node is of the type that we need, search other fields
                    if (result == null)
                    {
                        result = new SearchResult();
                    }

                    result.AddMatchByNodeType();
                }
                else
                {
                    return null;
                }
            }

            for (int i = 0; i < words.Count; i++)
            {
                bool anyFieldMatched = false;
                var word = words[i];

                for (int j = 0; j < fields.Count; j++)
                {
                    var field = fields[j];

                    if (!stringsThatMatch.Contains(field))
                    {
                        // no point looking here, we know this string doesn't match anything
                        continue;
                    }

                    var index = field.IndexOf(word, StringComparison.OrdinalIgnoreCase);
                    if (index != -1)
                    {
                        if (result == null)
                        {
                            result = new SearchResult();
                        }

                        // if matched on the type of the node (always field 0), special case it
                        if (j == 0)
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

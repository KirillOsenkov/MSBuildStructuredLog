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
        private string[] words;

        public IEnumerable<SearchResult> FindNodes(string query)
        {
            this.query = query;
            this.words = query.Split(space, StringSplitOptions.RemoveEmptyEntries);
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

            PopulateSearchFields(node, searchFields.Add);

            var result = IsMatch(searchFields, stringsThatMatch);
            if (result != null)
            {
                result.Node = node;
                resultSet.Add(result);
            }
        }

        public static void PopulateSearchFields(object node, Action<string> addSearchField)
        {
            // in case they want to narrow down the search such as "Build target" or "Copy task"
            var typeName = node.GetType().Name;
            addSearchField(typeName);

            // for tasks derived from Task $task should still work
            if (node is Task && typeName != "Task")
            {
                addSearchField("Task");
            }

            var named = node as NamedNode;
            if (named != null && named.Name != null)
            {
                addSearchField(named.Name);
            }

            var textNode = node as TextNode;
            if (textNode != null && textNode.Text != null)
            {
                addSearchField(textNode.Text);
            }

            var nameValueNode = node as NameValueNode;
            if (nameValueNode != null)
            {
                addSearchField(nameValueNode.Name);
                addSearchField(nameValueNode.Value);
            }

            var diagnostic = node as AbstractDiagnostic;
            if (diagnostic != null)
            {
                addSearchField(diagnostic.Code);
                addSearchField(diagnostic.File);
                addSearchField(diagnostic.ProjectFile);
            }
        }

        /// <summary>
        ///  Each of the query words must be found in at least one field ∀w ∃f
        /// </summary>
        private SearchResult IsMatch(List<string> fields, HashSet<string> stringsThatMatch)
        {
            SearchResult result = null;

            for (int i = 0; i < words.Length; i++)
            {
                bool anyFieldMatched = false;
                var word = words[i];

                // enable strict search for node type like "$property Foo" to search for properties only
                if (word.Length > 2 && word[0] == '$' && word[1] != '(')
                {
                    word = word.Substring(1);

                    // zeroth field is always the type
                    var type = fields[0];
                    if (string.Equals(word, type, StringComparison.OrdinalIgnoreCase) ||
                        // special case for types derived from Task, $task should still work
                        (string.Equals("task", word, StringComparison.OrdinalIgnoreCase) && fields.Count > 1 && fields[1] == "Task"))
                    {
                        // this node is of the type that we need, search other fields
                        if (result == null)
                        {
                            result = new SearchResult();
                        }

                        result.AddMatchByNodeType();
                        continue;
                    }
                    else
                    {
                        return null;
                    }
                }

                for (int j = 0; j < fields.Count; j++)
                {
                    var field = fields[j];
                    if (string.IsNullOrEmpty(field))
                    {
                        continue;
                    }

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

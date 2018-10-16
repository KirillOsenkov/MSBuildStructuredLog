using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Build.Logging.StructuredLogger;

namespace StructuredLogViewer
{
    public class NodeQueryMatcher
    {
        public string Query { get; private set; }
        public List<string> Words { get; private set; }
        public string TypeKeyword { get; private set; }
        public int NodeIndex { get; private set; } = -1;
        public HashSet<string> MatchesInStrings { get; private set; }
        private NodeQueryMatcher UnderMatcher { get; set; }

        // avoid allocating this for every node
        private readonly List<string> searchFields = new List<string>(6);

        private static readonly char[] space = { ' ' };

        public NodeQueryMatcher(string query, IEnumerable<string> stringTable)
        {
            this.Query = query;

            this.Words = ParseIntoWords(query);

            if (Words.Count == 1 &&
                Words[0] is string potentialNodeIndex &&
                potentialNodeIndex.Length > 1 &&
                potentialNodeIndex[0] == '$')
            {
                var nodeIndexText = potentialNodeIndex.Substring(1);
                if (int.TryParse(nodeIndexText, out var nodeIndex))
                {
                    NodeIndex = nodeIndex;
                    Words.RemoveAt(0);
                    return;
                }
            }

            for (int i = Words.Count - 1; i >= 0; i--)
            {
                var word = Words[i];
                if (word.Length > 2 && word[0] == '$' && word[1] != '(' && TypeKeyword == null)
                {
                    Words.RemoveAt(i);
                    TypeKeyword = word.Substring(1).ToLowerInvariant();
                    continue;
                }

                if (word.StartsWith("under(", StringComparison.OrdinalIgnoreCase) && word.EndsWith(")"))
                {
                    word = word.Substring(6, word.Length - 7);
                    Words.RemoveAt(i);
                    UnderMatcher = new NodeQueryMatcher(word, stringTable);
                    continue;
                }
            }

            PrecomputeMatchesInStrings(stringTable);
        }

        private void PrecomputeMatchesInStrings(IEnumerable<string> stringTable)
        {
            MatchesInStrings = new HashSet<string>();

            foreach (var stringInstance in stringTable)
            {
                foreach (var word in Words)
                {
                    if (stringInstance.IndexOf(word, StringComparison.OrdinalIgnoreCase) != -1)
                    {
                        MatchesInStrings.Add(stringInstance);
                    }
                }
            }
        }

        private static List<string> ParseIntoWords(string query)
        {
            var result = new List<string>();

            StringBuilder currentWord = new StringBuilder();
            bool isInParentheses = false;
            bool isInQuotes = false;
            for (int i = 0; i < query.Length; i++)
            {
                char c = query[i];
                switch (c)
                {
                    case ' ' when !isInParentheses && !isInQuotes:
                        result.Add(TrimQuotes(currentWord.ToString()));
                        currentWord.Clear();
                        break;
                    case '(' when !isInParentheses && !isInQuotes:
                        isInParentheses = true;
                        currentWord.Append(c);
                        break;
                    case ')' when isInParentheses && !isInQuotes:
                        isInParentheses = false;
                        currentWord.Append(c);
                        break;
                    case '"' when !isInParentheses:
                        isInQuotes = !isInQuotes;
                        currentWord.Append(c);
                        break;
                    default:
                        currentWord.Append(c);
                        break;
                }
            }

            result.Add(TrimQuotes(currentWord.ToString()));

            return result;
        }

        private static string TrimQuotes(string word)
        {
            if (word.Length > 2 && word[0] == '"' && word[word.Length - 1] == '"')
            {
                word = word.Substring(1, word.Length - 2);
            }

            return word;
        }

        private void PopulateSearchFields(object node)
        {
            searchFields.Clear();

            // in case they want to narrow down the search such as "Build target" or "Copy task"
            var typeName = node.GetType().Name;
            searchFields.Add(typeName);

            // for tasks derived from Task $task should still work
            if (node is Task && typeName != "Task")
            {
                searchFields.Add("Task");
            }

            if (node is NamedNode named && !string.IsNullOrEmpty(named.Name))
            {
                searchFields.Add(named.Name);
            }

            if (node is TextNode textNode && !string.IsNullOrEmpty(textNode.Text))
            {
                searchFields.Add(textNode.Text);
            }

            if (node is NameValueNode nameValueNode)
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

            if (node is AbstractDiagnostic diagnostic)
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
        public SearchResult IsMatch(object node)
        {
            SearchResult result = null;

            if (NodeIndex > -1)
            {
                if (node is TimedNode timedNode && timedNode.Index == NodeIndex)
                {
                    result = new SearchResult(node);
                    var prefix = "Node id: ";
                    result.AddMatch(prefix + NodeIndex.ToString(), NodeIndex.ToString(), prefix.Length);
                    return result;
                }
            }

            PopulateSearchFields(node);

            if (TypeKeyword != null)
            {
                // zeroth field is always the type
                if (string.Equals(TypeKeyword, searchFields[0], StringComparison.OrdinalIgnoreCase) ||
                    // special case for types derived from Task, $task should still work
                    (TypeKeyword == "task" && searchFields.Count > 1 && searchFields[1] == "Task"))
                {
                    // this node is of the type that we need, search other fields
                    if (result == null)
                    {
                        result = new SearchResult(node);
                    }

                    result.AddMatchByNodeType();
                }
                else
                {
                    return null;
                }
            }

            for (int i = 0; i < Words.Count; i++)
            {
                bool anyFieldMatched = false;
                var word = Words[i];

                for (int j = 0; j < searchFields.Count; j++)
                {
                    var field = searchFields[j];

                    if (!MatchesInStrings.Contains(field))
                    {
                        // no point looking here, we know this string doesn't match anything
                        continue;
                    }

                    var index = field.IndexOf(word, StringComparison.OrdinalIgnoreCase);
                    if (index != -1)
                    {
                        if (result == null)
                        {
                            result = new SearchResult(node);
                        }

                        // if matched on the type of the node (always field 0), special case it
                        if (j == 0)
                        {
                            result.AddMatchByNodeType();
                        }
                        else
                        {
                            string fullText = field;
                            if (node is NameValueNode named && fullText == named.Name)
                            {
                                fullText = named.ToString();
                            }

                            result.AddMatch(fullText, word, index);
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

            if (UnderMatcher != null && !IsUnder(UnderMatcher, result))
            {
                return null;
            }

            return result;
        }

        private static bool IsUnder(NodeQueryMatcher matcher, SearchResult result)
        {
            if (!(result.Node is ParentedNode parented))
            {
                return true;
            }

            foreach (var parent in parented.GetParentChainExcludingThis())
            {
                if (matcher.IsMatch(parent) != null)
                {
                    return true;
                }
            }

            return false;
        }
    }
}

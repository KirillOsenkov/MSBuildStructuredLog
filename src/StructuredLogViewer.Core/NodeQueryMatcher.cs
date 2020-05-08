using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private HashSet<string>[] MatchesInStrings { get; set; }
        public bool IncludeDuration { get; set; }
        public TimeSpan PrecalculationDuration { get; set; }

        private string nameToSearch { get; set; }
        private string valueToSearch { get; set; }
        private List<NodeQueryMatcher> IncludeMatchers { get; set; } = new List<NodeQueryMatcher>();
        private List<NodeQueryMatcher> ExcludeMatchers { get; set; } = new List<NodeQueryMatcher>();

        // avoid allocating this for every node
        [ThreadStatic]
        private static List<string> searchFieldsThreadStatic;

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

                if (word == "$time")
                {
                    Words.RemoveAt(i);
                    IncludeDuration = true;
                    continue;
                }

                if (word.Length > 2 && word[0] == '$' && word[1] != '(' && (TypeKeyword == null || !TypeKeyword.Contains(word.Substring(1).ToLowerInvariant())))
                {
                    Words.RemoveAt(i);
                    TypeKeyword = word.Substring(1).ToLowerInvariant();
                    continue;
                }

                if (word.StartsWith("under(", StringComparison.OrdinalIgnoreCase) && word.EndsWith(")"))
                {
                    word = word.Substring(6, word.Length - 7);
                    Words.RemoveAt(i);
                    var underMatcher = new NodeQueryMatcher(word, stringTable);
                    IncludeMatchers.Add(underMatcher);
                    continue;
                }

                if (word.StartsWith("notunder(", StringComparison.OrdinalIgnoreCase) && word.EndsWith(")"))
                {
                    word = word.Substring(9, word.Length - 10);
                    Words.RemoveAt(i);
                    var underMatcher = new NodeQueryMatcher(word, stringTable);
                    ExcludeMatchers.Add(underMatcher);
                    continue;
                }

                if (word.StartsWith("name=", StringComparison.OrdinalIgnoreCase) && word.Length > 5)
                {
                    word = word.Substring(5, word.Length - 5);
                    Words.RemoveAt(i);
                    Words.Insert(i, word);
                    nameToSearch = word;
                    continue;
                }

                if (word.StartsWith("value=", StringComparison.OrdinalIgnoreCase) && word.Length > 6)
                {
                    word = word.Substring(6, word.Length - 6);
                    Words.RemoveAt(i);
                    Words.Insert(i, word);
                    valueToSearch = word;
                    continue;
                }
            }

            PrecomputeMatchesInStrings(stringTable);
        }

        private void PrecomputeMatchesInStrings(IEnumerable<string> stringTable)
        {
            int wordCount = Words.Count;
            MatchesInStrings = new HashSet<string>[wordCount];
            var wordTasks = new System.Threading.Tasks.Task[wordCount];

            var sw = Stopwatch.StartNew();

            for (int i = 0; i < Words.Count; i++)
            {
                MatchesInStrings[i] = new HashSet<string>();
            }

#if false
            for (int i = 0; i < Words.Count; i++)
            {
                var word = Words[i];
                var matches = MatchesInStrings[i];

                wordTasks[i] = System.Threading.Tasks.Task.Run(() =>
                {
                    System.Threading.Tasks.Parallel.ForEach(stringTable, stringInstance =>
                    {
                        if (stringInstance.IndexOf(word, StringComparison.OrdinalIgnoreCase) != -1)
                        {
                            lock (matches)
                            {
                                matches.Add(stringInstance);
                            }
                        }
                    });
                });
            }

            System.Threading.Tasks.Task.WaitAll(wordTasks);

#else
            System.Threading.Tasks.Parallel.ForEach(stringTable, stringInstance =>
            {
                for (int i = 0; i < Words.Count; i++)
                {
                    var word = Words[i];
                    if (stringInstance.IndexOf(word, StringComparison.OrdinalIgnoreCase) != -1)
                    {
                        var matches = MatchesInStrings[i];
                        lock (matches)
                        {
                            matches.Add(stringInstance);
                        }
                    }
                }
            });
#endif

            var elapsed = sw.Elapsed;
            PrecalculationDuration = elapsed;
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
                        var wordToAdd = TrimQuotes(currentWord.ToString());
                        if (!string.IsNullOrWhiteSpace(wordToAdd))
                        {
                            result.Add(wordToAdd);
                        }

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

            var word = TrimQuotes(currentWord.ToString());
            if (!string.IsNullOrWhiteSpace(word))
            {
                result.Add(word);
            }

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

        public static List<string> PopulateSearchFields(BaseNode node)
        {
            var searchFields = searchFieldsThreadStatic;

            if (searchFields == null)
            {
                searchFields = new List<string>(6);
                searchFieldsThreadStatic = searchFields;
            }
            else
            {
                searchFields.Clear();
            }

            // in case they want to narrow down the search such as "Build target" or "Copy task"
            var typeName = node.TypeName;
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

            return searchFields;
        }

        /// <summary>
        ///  Each of the query words must be found in at least one field ∀w ∃f
        /// </summary>
        public SearchResult IsMatch(BaseNode node)
        {
            SearchResult result = null;

            if (node == null)
            {
                return null;
            }

            if (NodeIndex > -1)
            {
                if (node is TimedNode timedNode && timedNode.Index == NodeIndex)
                {
                    result = new SearchResult(node);
                    var prefix = "Node id: ";
                    result.AddMatch(prefix + NodeIndex.ToString(), NodeIndex.ToString());
                    return result;
                }
            }

            var searchFields = PopulateSearchFields(node);

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
                        result = new SearchResult(node, IncludeDuration);
                    }

                    result.AddMatchByNodeType();
                }
                else
                {
                    return null;
                }
            }

            bool nameMatched = false;
            bool valueMatched = false;
            for (int i = 0; i < Words.Count; i++)
            {
                bool anyFieldMatched = false;
                var word = Words[i];

                for (int j = 0; j < searchFields.Count; j++)
                {
                    var field = searchFields[j];

                    if (!MatchesInStrings[i].Contains(field))
                    {
                        // no point looking here, we know this string doesn't match anything
                        continue;
                    }

                    if (result == null)
                    {
                        result = new SearchResult(node, IncludeDuration);
                    }

                    // if matched on the type of the node (always field 0), special case it
                    if (j == 0)
                    {
                        result.AddMatchByNodeType();
                    }
                    else
                    {
                        string fullText = field;
                        var named = node as NameValueNode;

                        // NameValueNode is special case have to check in which field to search
                        if (named != null && (nameToSearch != null || valueToSearch != null))
                        {
                            if (j == 1 && word == nameToSearch)
                            {
                                result.AddMatch(fullText, word, addAtBeginning: true);
                                nameMatched = true;
                                anyFieldMatched = true;
                                break;
                            }

                            if (j == 2 && word == valueToSearch)
                            {
                                result.AddMatch(fullText, word);
                                valueMatched = true;
                                anyFieldMatched = true;
                                break;
                            }
                        }
                        else
                        {
                            result.AddMatch(fullText, word);
                            anyFieldMatched = true;
                            break;
                        }
                    }
                }

                if (!anyFieldMatched)
                {
                    return null;
                }
            }

            if (result == null)
            {
                return null;
            }

            if (nameToSearch != null && valueToSearch != null && (!nameMatched || !valueMatched))
            {
                return null;
            }

            bool showResult = IncludeMatchers.Count == 0;
            foreach (NodeQueryMatcher matcher in IncludeMatchers)
            {
                if (!showResult)
                {
                    showResult = IsUnder(matcher, result);
                }
            }

            if (!showResult)
            {
                return null;
            }

            foreach (NodeQueryMatcher matcher in ExcludeMatchers)
            {
                if (IsUnder(matcher, result))
                {
                    return null;
                }
            }

            return result;
        }

        private static bool IsUnder(NodeQueryMatcher matcher, SearchResult result)
        {
            foreach (var parent in result.Node.GetParentChainExcludingThis())
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

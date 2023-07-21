using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Logging.StructuredLogger;
using StructuredLogger;

namespace StructuredLogViewer
{
    public struct Term : IEquatable<Term>
    {
        public string Word;
        public bool Quotes;

        public Term(string word, bool quotes = false)
        {
            Word = word;
            Quotes = quotes;
        }

        public static Term Get(string input)
        {
            var trimmed = input.TrimQuotes();
            if (trimmed == input)
            {
                return new Term(input);
            }
            else if (!string.IsNullOrWhiteSpace(trimmed))
            {
                if (trimmed.Contains(" "))
                {
                    // multi-word
                    return new Term(trimmed);
                }
                else
                {
                    // exact match
                    return new Term(trimmed, quotes: true);
                }
            }

            return default;
        }

        public bool IsMatch(string field, HashSet<string> superstrings)
        {
            if (Quotes)
            {
                return string.Equals(field, Word, StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                return superstrings.Contains(field);
            }
        }

        public override bool Equals(object obj)
        {
            if (obj is Term other)
            {
                return Equals(other);
            }

            return false;
        }

        public override int GetHashCode()
        {
            return (Word, Quotes).GetHashCode();
        }

        public bool Equals(Term other)
        {
            return other.Word == Word && other.Quotes == Quotes;
        }

        public static bool operator ==(Term left, Term right) => left.Equals(right);
        public static bool operator !=(Term left, Term right) => !left.Equals(right);

        public override string ToString()
        {
            return $"'{Word}' Quotes={Quotes}";
        }
    }

    public class NodeQueryMatcher
    {
        public string Query { get; private set; }
        public List<Term> Words { get; private set; }
        public string TypeKeyword { get; private set; }
        public int NodeIndex { get; private set; } = -1;
        private HashSet<string>[] MatchesInStrings { get; set; }
        public bool IncludeDuration { get; set; }
        public bool IncludeStart { get; set; }
        public bool IncludeEnd { get; set; }
        public TimeSpan PrecalculationDuration { get; set; }
        public bool UnderProject { get; set; } = false;

        private Term nameToSearch { get; set; }
        private Term valueToSearch { get; set; }
        private List<NodeQueryMatcher> IncludeMatchers { get; set; } = new List<NodeQueryMatcher>();
        private List<NodeQueryMatcher> ExcludeMatchers { get; set; } = new List<NodeQueryMatcher>();

        // avoid allocating this for every node
        [ThreadStatic]
        private static string[] searchFieldsThreadStatic;

        private readonly StringCache stringCache; // only used for validation that all strings are interned (disabled)

        public NodeQueryMatcher(
            string query,
            IEnumerable<string> stringTable,
            CancellationToken cancellationToken = default,
            StringCache stringCache = null // validation disabled in production
            )
        {
            this.stringCache = stringCache;

            query = PreprocessQuery(query);

            this.Query = query;

            var rawWords = TextUtilities.Tokenize(query);
            this.Words = new List<Term>(rawWords.Count);
            foreach (var rawWord in rawWords)
            {
                var term = Term.Get(rawWord);
                if (term != default)
                {
                    Words.Add(term);
                }
            }

            if (Words.Count == 1 &&
                Words[0].Word is string potentialNodeIndex &&
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
                var word = Words[i].Word;

                if (string.Equals(word, "$time", StringComparison.OrdinalIgnoreCase) || string.Equals(word, "$duration", StringComparison.OrdinalIgnoreCase))
                {
                    Words.RemoveAt(i);
                    IncludeDuration = true;
                    continue;
                }
                else if (string.Equals(word, "$start", StringComparison.OrdinalIgnoreCase) || string.Equals(word, "$starttime", StringComparison.OrdinalIgnoreCase))
                {
                    Words.RemoveAt(i);
                    IncludeStart = true;
                    continue;
                }
                else if (string.Equals(word, "$end", StringComparison.OrdinalIgnoreCase) || string.Equals(word, "$endtime", StringComparison.OrdinalIgnoreCase))
                {
                    Words.RemoveAt(i);
                    IncludeEnd = true;
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

                if (word.StartsWith("project(", StringComparison.OrdinalIgnoreCase) && word.EndsWith(")"))
                {
                    word = word.Substring(8, word.Length - 9);
                    Words.RemoveAt(i);

                    var underMatcher = new NodeQueryMatcher(word, stringTable);
                    underMatcher.UnderProject = true;
                    IncludeMatchers.Add(underMatcher);
                    continue;
                }

                if (word.StartsWith("name=", StringComparison.OrdinalIgnoreCase) && word.Length > 5)
                {
                    word = word.Substring(5, word.Length - 5);
                    Words.RemoveAt(i);
                    var term = Term.Get(word);
                    if (term != default)
                    {
                        Words.Insert(i, term);
                        nameToSearch = term;
                    }

                    continue;
                }

                if (word.StartsWith("value=", StringComparison.OrdinalIgnoreCase) && word.Length > 6)
                {
                    word = word.Substring(6, word.Length - 6);
                    Words.RemoveAt(i);
                    var term = Term.Get(word);
                    if (term != default)
                    {
                        Words.Insert(i, term);
                        valueToSearch = term;
                    }

                    continue;
                }
            }

            PrecomputeMatchesInStrings(stringTable, cancellationToken);
        }

        private string PreprocessQuery(string query)
        {
            if (string.IsNullOrEmpty(query))
            {
                return query;
            }

            query = query.Replace("$csc", "$task csc");
            query = query.Replace("$rar", "$task ResolveAssemblyReference");

            return query;
        }

        private void PrecomputeMatchesInStrings(IEnumerable<string> stringTable, CancellationToken cancellationToken = default)
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
            var options = new ParallelOptions
            {
                CancellationToken = cancellationToken,
            };
            if (PlatformUtilities.HasThreads)
            {
                Parallel.ForEach(stringTable, options, ProcessString);
            }
            else
            {
                foreach (var stringInstance in stringTable)
                {
                    ProcessString(stringInstance);
                }
            }
#endif

            var elapsed = sw.Elapsed;
            PrecalculationDuration = elapsed;

            void ProcessString(string stringInstance)
            {
                for (int i = 0; i < Words.Count; i++)
                {
                    var term = Words[i];
                    if (term.Quotes)
                    {
                        continue;
                    }

                    var word = term.Word;
                    if (stringInstance.IndexOf(word, StringComparison.OrdinalIgnoreCase) != -1)
                    {
                        var matches = MatchesInStrings[i];
                        lock (matches)
                        {
                            matches.Add(stringInstance);
                        }
                    }
                }
            }
        }

        private const int MaxArraySize = 6;

        public static (string[] array, int count) PopulateSearchFields(BaseNode node)
        {
            var searchFields = searchFieldsThreadStatic;
            int count = 0;

            if (searchFields == null)
            {
                searchFields = new string[MaxArraySize];
                searchFieldsThreadStatic = searchFields;
            }
            else
            {
                Array.Clear(searchFields, 0, MaxArraySize);
            }

            // in case they want to narrow down the search such as "Build target" or "Copy task"
            var typeName = node.TypeName;

            // for tasks derived from Task $task should still work
            if (node is Microsoft.Build.Logging.StructuredLogger.Task t && t.IsDerivedTask)
            {
                searchFields[count++] = "Task";
            }

            searchFields[count++] = typeName;

            if (node is NameValueNode nameValueNode)
            {
                if (!string.IsNullOrEmpty(nameValueNode.Name))
                {
                    searchFields[count++] = nameValueNode.Name;
                }

                if (!string.IsNullOrEmpty(nameValueNode.Value))
                {
                    searchFields[count++] = nameValueNode.Value;
                }
            }
            else if (node is NamedNode named)
            {
                if (!string.IsNullOrEmpty(named.Name))
                {
                    searchFields[count++] = named.Name;
                }

                if (node is TextNode textNode)
                {
                    if (!string.IsNullOrEmpty(textNode.Text))
                    {
                        searchFields[count++] = textNode.Text;
                    }

                    if (node is AbstractDiagnostic diagnostic)
                    {
                        if (!string.IsNullOrEmpty(diagnostic.Code))
                        {
                            searchFields[count++] = diagnostic.Code;
                        }

                        if (!string.IsNullOrEmpty(diagnostic.File))
                        {
                            searchFields[count++] = diagnostic.File;
                        }

                        if (!string.IsNullOrEmpty(diagnostic.ProjectFile))
                        {
                            searchFields[count++] = diagnostic.ProjectFile;
                        }
                    }
                }
                else if (node is TimedNode)
                {
                    if (node is Project project)
                    {
                        if (!string.IsNullOrEmpty(project.TargetFramework))
                        {
                            searchFields[count++] = project.TargetFramework;
                        }

                        if (!string.IsNullOrEmpty(project.TargetsText))
                        {
                            searchFields[count++] = project.TargetsText;
                        }

                        if (!string.IsNullOrEmpty(project.EvaluationText))
                        {
                            searchFields[count++] = project.EvaluationText;
                        }
                    }
                    else if (node is ProjectEvaluation evaluation)
                    {
                        if (!string.IsNullOrEmpty(evaluation.EvaluationText))
                        {
                            searchFields[count++] = evaluation.EvaluationText;
                        }
                    }
                    else if (node is Target target)
                    {
                        if (!string.IsNullOrEmpty(target.ParentTarget))
                        {
                            searchFields[count++] = target.ParentTarget;
                        }
                    }
                }
            }

            return (searchFields, count);
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
                if (string.Equals(TypeKeyword, searchFields.array[0], StringComparison.OrdinalIgnoreCase) ||
                    // special case for types derived from Task, $task should still work
                    (TypeKeyword == "task" && searchFields.count > 1 && searchFields.array[1] == "Task"))
                {
                    // this node is of the type that we need, search other fields
                    if (result == null)
                    {
                        result = new SearchResult(node, IncludeDuration, IncludeStart, IncludeEnd);
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
                var term = Words[i];
                var word = term.Word;

                for (int j = 0; j < searchFields.count; j++)
                {
                    var field = searchFields.array[j];

                    //if (!stringCache.Contains(field))
                    //{
                    //}

                    if (!term.IsMatch(field, MatchesInStrings[i]))
                    {
                        continue;
                    }

                    if (result == null)
                    {
                        result = new SearchResult(node, IncludeDuration, IncludeStart, IncludeEnd);
                    }

                    // if matched on the type of the node (always field 0), special case it
                    if (j == 0)
                    {
                        result.AddMatchByNodeType();
                    }
                    else
                    {
                        string fullText = field;
                        var nameValueNode = node as NameValueNode;

                        // NameValueNode is a special case: have to check in which field to search
                        if (nameValueNode != null && (nameToSearch != default || valueToSearch != default))
                        {
                            if (j == 1 && term == nameToSearch)
                            {
                                result.AddMatch(fullText, word, addAtBeginning: true);
                                nameMatched = true;
                                anyFieldMatched = true;
                                break;
                            }

                            if (j == 2 && term == valueToSearch)
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

            if (nameToSearch != default && valueToSearch != default && (!nameMatched || !valueMatched))
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
            if (matcher.UnderProject)
            {
                var project = result.Node.GetNearestParent<Project>();
                if (project != null && matcher.IsMatch(project) != null)
                {
                    return true;
                }

                var projectEvaluation = result.Node.GetNearestParent<ProjectEvaluation>();
                if (projectEvaluation != null && matcher.IsMatch(projectEvaluation) != null)
                {
                    return true;
                }

                return false;
            }

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

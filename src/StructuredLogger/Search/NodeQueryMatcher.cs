using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Logging.StructuredLogger;

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

        public bool IsMatch(string field)
        {
            if (Quotes)
            {
                return string.Equals(field, Word, StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                return field.IndexOf(Word, StringComparison.OrdinalIgnoreCase) != -1;
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
        public List<Term> Terms { get; private set; }
        public string TypeKeyword { get; private set; }
        public int NodeIndex { get; private set; } = -1;
        private HashSet<string>[] MatchesInStrings { get; set; }
        public bool IncludeDuration { get; set; }
        public bool IncludeStart { get; set; }
        public bool IncludeEnd { get; set; }
        public TimeSpan PrecalculationDuration { get; set; }
        public bool IsProjectMatcher { get; set; } = false;

        public int NameTermIndex { get; set; } = -1;
        public int ValueTermIndex { get; set; } = -1;
        public bool? Skipped { get; set; }
        public int Height { get; set; } = -1;

        public DateTime StartBefore { get; set; }
        public DateTime StartAfter { get; set; }
        public DateTime EndBefore { get; set; }
        public DateTime EndAfter { get; set; }
        public bool HasTimeIntervalConstraints { get; set; }

        public IList<NodeQueryMatcher> IncludeMatchers { get; } = new List<NodeQueryMatcher>();
        public IList<NodeQueryMatcher> ExcludeMatchers { get; } = new List<NodeQueryMatcher>();
        public IList<NodeQueryMatcher> NotMatchers { get; } = new List<NodeQueryMatcher>();
        public IList<NodeQueryMatcher> ProjectMatchers { get; } = new List<NodeQueryMatcher>();

        // avoid allocating this for every node
        [ThreadStatic]
        private static string[] searchFieldsThreadStatic;

        public const int MaxArraySize = 6;

        public NodeQueryMatcher(string query)
        {
            query = PreprocessQuery(query);

            this.Query = query;

            var rawTerms = TextUtilities.Tokenize(query);
            this.Terms = new List<Term>(rawTerms.Count);

            foreach (var rawTerm in rawTerms)
            {
                var term = Term.Get(rawTerm);
                if (term != default)
                {
                    Terms.Add(term);
                }
            }

            if (Terms.Count == 1 &&
                Terms[0].Word is string potentialNodeIndex &&
                potentialNodeIndex.Length > 1 &&
                potentialNodeIndex[0] == '$')
            {
                var nodeIndexText = potentialNodeIndex.Substring(1);
                if (int.TryParse(nodeIndexText, out var nodeIndex))
                {
                    NodeIndex = nodeIndex;
                    Terms.RemoveAt(0);
                    return;
                }
            }

            ParseTerms();
        }

        public void Initialize(IEnumerable<string> stringTable, CancellationToken cancellationToken = default)
        {
            if (stringTable == null)
            {
                return;
            }

            var sw = Stopwatch.StartNew();
            MatchesInStrings = PrecomputeMatchesInStrings(stringTable, Terms, cancellationToken);
            var elapsed = sw.Elapsed;
            PrecalculationDuration = elapsed;
        }

        private void ParseTerms()
        {
            for (int termIndex = Terms.Count - 1; termIndex >= 0; termIndex--)
            {
                var word = Terms[termIndex].Word;

                if (string.Equals(word, "$time", StringComparison.OrdinalIgnoreCase) || string.Equals(word, "$duration", StringComparison.OrdinalIgnoreCase))
                {
                    Terms.RemoveAt(termIndex);
                    IncludeDuration = true;
                    continue;
                }
                else if (string.Equals(word, "$start", StringComparison.OrdinalIgnoreCase) || string.Equals(word, "$starttime", StringComparison.OrdinalIgnoreCase))
                {
                    Terms.RemoveAt(termIndex);
                    IncludeStart = true;
                    continue;
                }
                else if (string.Equals(word, "$end", StringComparison.OrdinalIgnoreCase) || string.Equals(word, "$endtime", StringComparison.OrdinalIgnoreCase))
                {
                    Terms.RemoveAt(termIndex);
                    IncludeEnd = true;
                    continue;
                }

                if (word.Length > 2 && word[0] == '$' && word[1] != '(' && (TypeKeyword == null || !TypeKeyword.Contains(word.Substring(1).ToLowerInvariant())))
                {
                    Terms.RemoveAt(termIndex);
                    TypeKeyword = word.Substring(1).ToLowerInvariant();
                    continue;
                }

                if (word.StartsWith("under(", StringComparison.OrdinalIgnoreCase) && word.EndsWith(")"))
                {
                    word = word.Substring(6, word.Length - 7);
                    Terms.RemoveAt(termIndex);
                    var underMatcher = new NodeQueryMatcher(word);
                    IncludeMatchers.Add(underMatcher);
                    continue;
                }

                if (word.StartsWith("notunder(", StringComparison.OrdinalIgnoreCase) && word.EndsWith(")"))
                {
                    word = word.Substring(9, word.Length - 10);
                    Terms.RemoveAt(termIndex);
                    var underMatcher = new NodeQueryMatcher(word);
                    ExcludeMatchers.Add(underMatcher);
                    continue;
                }

                if (word.StartsWith("not(", StringComparison.OrdinalIgnoreCase) && word.EndsWith(")"))
                {
                    word = word.Substring(4, word.Length - 5);
                    Terms.RemoveAt(termIndex);
                    var notMatcher = new NodeQueryMatcher(word);
                    NotMatchers.Add(notMatcher);
                }

                if (word.StartsWith("project(", StringComparison.OrdinalIgnoreCase) && word.EndsWith(")"))
                {
                    word = word.Substring(8, word.Length - 9);
                    Terms.RemoveAt(termIndex);

                    var underMatcher = new NodeQueryMatcher(word);
                    underMatcher.IsProjectMatcher = true;
                    IncludeMatchers.Add(underMatcher);
                    ProjectMatchers.Add(underMatcher);
                    continue;
                }

                if (word.StartsWith("start<\"", StringComparison.OrdinalIgnoreCase) && word.Length > 8 && word.EndsWith("\""))
                {
                    word = word.Substring(7, word.Length - 8);
                    Terms.RemoveAt(termIndex);

                    if (DateTime.TryParse(word, out var startBefore))
                    {
                        StartBefore = startBefore;
                        HasTimeIntervalConstraints = true;
                        continue;
                    }
                }

                if (word.StartsWith("start>\"", StringComparison.OrdinalIgnoreCase) && word.Length > 8 && word.EndsWith("\""))
                {
                    word = word.Substring(7, word.Length - 8);
                    Terms.RemoveAt(termIndex);

                    if (DateTime.TryParse(word, out var startAfter))
                    {
                        StartAfter = startAfter;
                        HasTimeIntervalConstraints = true;
                        continue;
                    }
                }

                if (word.StartsWith("end<\"", StringComparison.OrdinalIgnoreCase) && word.Length > 6 && word.EndsWith("\""))
                {
                    word = word.Substring(5, word.Length - 6);
                    Terms.RemoveAt(termIndex);

                    if (DateTime.TryParse(word, out var endBefore))
                    {
                        EndBefore = endBefore;
                        HasTimeIntervalConstraints = true;
                        continue;
                    }
                }

                if (word.StartsWith("end>\"", StringComparison.OrdinalIgnoreCase) && word.Length > 6 && word.EndsWith("\""))
                {
                    word = word.Substring(5, word.Length - 6);
                    Terms.RemoveAt(termIndex);

                    if (DateTime.TryParse(word, out var endAfter))
                    {
                        EndAfter = endAfter;
                        HasTimeIntervalConstraints = true;
                        continue;
                    }
                }
            }

            // need to do a second pass because previous loop might shift term indices by removing terms
            for (int termIndex = Terms.Count - 1; termIndex >= 0; termIndex--)
            {
                var word = Terms[termIndex].Word;

                if (word.StartsWith("name=", StringComparison.OrdinalIgnoreCase) && word.Length > 5)
                {
                    word = word.Substring(5, word.Length - 5);
                    Terms.RemoveAt(termIndex);
                    var term = Term.Get(word);
                    if (term != default)
                    {
                        Terms.Insert(termIndex, term);
                        NameTermIndex = termIndex;
                    }

                    continue;
                }

                if (word.StartsWith("value=", StringComparison.OrdinalIgnoreCase) && word.Length > 6)
                {
                    word = word.Substring(6, word.Length - 6);
                    Terms.RemoveAt(termIndex);
                    var term = Term.Get(word);
                    if (term != default)
                    {
                        Terms.Insert(termIndex, term);
                        ValueTermIndex = termIndex;
                    }

                    continue;
                }

                if (word.StartsWith("skipped=", StringComparison.OrdinalIgnoreCase) && word.Length > 8)
                {
                    word = word.Substring(8, word.Length - 8);
                    Terms.RemoveAt(termIndex);
                    if (string.Equals("true", word, StringComparison.OrdinalIgnoreCase))
                    {
                        Skipped = true;
                    }
                    else if (string.Equals("false", word, StringComparison.OrdinalIgnoreCase))
                    {
                        Skipped = false;
                    }

                    continue;
                }

                if (word.StartsWith("height=", StringComparison.OrdinalIgnoreCase) && word.Length > 7)
                {
                    word = word.Substring(7, word.Length - 7);
                    Terms.RemoveAt(termIndex);
                    if (string.Equals("max", word, StringComparison.OrdinalIgnoreCase))
                    {
                        Height = int.MaxValue;
                    }
                    else if (int.TryParse(word, out int height))
                    {
                        Height = height;
                    }
                }
            }
        }

        private string PreprocessQuery(string query)
        {
            if (string.IsNullOrEmpty(query))
            {
                return query;
            }

            query = query.Replace("$csc", "$task csc");
            query = query.Replace("$rar", "$task ResolveAssemblyReference");
            query = query.Replace("project (", "project(");
            query = query.Replace("under (", "under(");
            query = query.Replace("notunder (", "notunder(");
            query = query.Replace("start < ", "start<");
            query = query.Replace("start <", "start<");
            query = query.Replace("start< ", "start<");
            query = query.Replace("start > ", "start>");
            query = query.Replace("start >", "start>");
            query = query.Replace("start> ", "start>");
            query = query.Replace("end < ", "end<");
            query = query.Replace("end <", "end<");
            query = query.Replace("end< ", "end<");
            query = query.Replace("end > ", "end>");
            query = query.Replace("end >", "end>");
            query = query.Replace("end> ", "end>");
            query = query.Replace("name =", "name=");
            query = query.Replace("value =", "value=");
            query = query.Replace("skipped =", "skipped=");

            return query;
        }

        public static HashSet<string>[] PrecomputeMatchesInStrings(
            IEnumerable<string> stringTable,
            IList<Term> terms,
            CancellationToken cancellationToken = default)
        {
            int termCount = terms.Count;
            var matchesInStrings = new HashSet<string>[termCount];
            var wordTasks = new System.Threading.Tasks.Task[termCount];

            for (int i = 0; i < termCount; i++)
            {
                matchesInStrings[i] = new HashSet<string>();
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

            return matchesInStrings;

            void ProcessString(string stringInstance)
            {
                for (int i = 0; i < terms.Count; i++)
                {
                    var term = terms[i];
                    if (term.Quotes)
                    {
                        continue;
                    }

                    var word = term.Word;
                    if (stringInstance.IndexOf(word, StringComparison.OrdinalIgnoreCase) != -1)
                    {
                        var matches = matchesInStrings[i];
                        lock (matches)
                        {
                            matches.Add(stringInstance);
                        }
                    }
                }
            }
        }

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
            //if (node is Microsoft.Build.Logging.StructuredLogger.Task t && t.IsDerivedTask)
            //{
            //    //searchFields[count++] = Strings.Task;
            //    typeName = Strings.Task;
            //}

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

                if (node is TimedNode)
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
                }
            }
            else if (node is TextNode textNode)
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
                else if (node is Import import)
                {
                    //if (!string.IsNullOrEmpty(import.ProjectFilePath))
                    //{
                    //    searchFields[count++] = import.ProjectFilePath;
                    //}

                    if (!string.IsNullOrEmpty(import.ImportedProjectFilePath))
                    {
                        searchFields[count++] = import.ImportedProjectFilePath;
                    }
                }
                else if (node is NoImport noImport)
                {
                    //if (!string.IsNullOrEmpty(noImport.ProjectFilePath))
                    //{
                    //    searchFields[count++] = noImport.ProjectFilePath;
                    //}

                    if (!string.IsNullOrEmpty(noImport.ImportedFileSpec))
                    {
                        searchFields[count++] = noImport.ImportedFileSpec;
                    }

                    if (!string.IsNullOrEmpty(noImport.Reason))
                    {
                        searchFields[count++] = noImport.Reason;
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

            if (NodeIndex > -1)
            {
                if (node is TimedNode timedNode && timedNode.Index == NodeIndex)
                {
                    result = new SearchResult(node);
                    var prefix = "Node id: ";
                    result.AddMatch(prefix + NodeIndex.ToString(), NodeIndex.ToString());
                    return result;
                }

                return null;
            }

            var searchFields = PopulateSearchFields(node);

            if (TypeKeyword != null)
            {
                // zeroth field is always the type
                if (string.Equals(searchFields.array[0], TypeKeyword, StringComparison.OrdinalIgnoreCase) ||
                    // special case for types derived from Task, $task should still work
                    (searchFields.count > 1 &&
                    string.Equals(searchFields.array[0], Strings.Task, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(searchFields.array[1], TypeKeyword, StringComparison.OrdinalIgnoreCase)))
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
            for (int termIndex = 0; termIndex < Terms.Count; termIndex++)
            {
                bool anyFieldMatched = false;
                Term term = Terms[termIndex];
                string word = term.Word;

                for (int fieldIndex = 0; fieldIndex < searchFields.count; fieldIndex++)
                {
                    string field = searchFields.array[fieldIndex];

                    if (MatchesInStrings != null)
                    {
                        if (!term.IsMatch(field, MatchesInStrings[termIndex]))
                        {
                            continue;
                        }
                    }
                    else
                    {
                        if (!term.IsMatch(field))
                        {
                            continue;
                        }
                    }

                    if (result == null)
                    {
                        result = new SearchResult(node, IncludeDuration, IncludeStart, IncludeEnd);
                    }

                    // if matched on the type of the node (always field 0), special case it
                    if (fieldIndex == 0)
                    {
                        result.AddMatchByNodeType();
                    }
                    else
                    {
                        string fullText = field;

                        // NameValueNode is a special case: have to check in which field to search
                        if (node is NameValueNode && (NameTermIndex != -1 || ValueTermIndex != -1))
                        {
                            if (fieldIndex == 1 && termIndex == NameTermIndex)
                            {
                                result.AddMatch(fullText, word, addAtBeginning: true);
                                nameMatched = true;
                                anyFieldMatched = true;
                                break;
                            }

                            if (fieldIndex == 2 && termIndex == ValueTermIndex)
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

            // if both name and value are specified, they both have to match
            if (NameTermIndex != -1 && ValueTermIndex != -1 && (!nameMatched || !valueMatched))
            {
                return null;
            }

            bool showResult = IncludeMatchers.Count == 0;
            foreach (NodeQueryMatcher matcher in IncludeMatchers)
            {
                if (!showResult)
                {
                    showResult = IsUnder(matcher, result.Node);
                }
            }

            if (!showResult)
            {
                return null;
            }

            foreach (NodeQueryMatcher matcher in ExcludeMatchers)
            {
                if (IsUnder(matcher, result.Node))
                {
                    return null;
                }
            }

            foreach (NodeQueryMatcher matcher in NotMatchers)
            {
                var notMatch = matcher.IsMatch(node);
                if (notMatch != null)
                {
                    return null;
                }
            }

            if (Skipped != null && node is Target target)
            {
                if (target.Skipped != Skipped)
                {
                    return null;
                }
            }

            return result;
        }

        public SearchResult IsMatch(string field)
        {
            SearchResult result = null;

            foreach (var term in Terms)
            {
                if (!term.IsMatch(field))
                {
                    return null;
                }

                result ??= new();
                result.AddMatch(field, term.Word);
            }

            return result ?? SearchResult.EmptyQueryMatch;
        }

        /// <summary>
        /// Matches Terms against the given set of fields
        /// </summary>
        /// <param name="fields">Fields to match against</param>
        /// <returns>
        /// EmptyQueryMatch if there are no terms.
        /// null if there was a term that didn't match any fields.
        /// SearchResult if all terms matched at least one field.
        /// </returns>
        public SearchResult IsMatch(params string[] fields)
        {
            SearchResult result = null;

            foreach (var term in Terms)
            {
                bool matched = false;
                foreach (var field in fields)
                {
                    if (term.IsMatch(field))
                    {
                        matched = true;
                        result ??= new();
                        result.AddMatch(field, term.Word);
                        continue;
                    }
                }

                if (!matched)
                {
                    return null;
                }
            }

            return result ?? SearchResult.EmptyQueryMatch;
        }

        public bool IsTimeIntervalMatch(BaseNode node)
        {
            if (!HasTimeIntervalConstraints)
            {
                return true;
            }

            // Messages and Folders are not timed nodes, use the parent instead.
            if (node is not TimedNode timedNode)
            {
                var parentNode = node.GetNearestParent<TimedNode>();
                if (parentNode is null)
                {
                    return true;
                }

                timedNode = parentNode;
            }

            if (StartBefore != default && timedNode.StartTime > StartBefore)
            {
                return false;
            }

            if (StartAfter != default && timedNode.StartTime < StartAfter)
            {
                return false;
            }

            if (EndBefore != default && timedNode.EndTime > EndBefore)
            {
                return false;
            }

            if (EndAfter != default && timedNode.EndTime < EndAfter)
            {
                return false;
            }

            return true;
        }

        public static bool IsUnder(NodeQueryMatcher matcher, BaseNode node)
        {
            if (matcher.IsProjectMatcher)
            {
                var project = node.GetNearestParent<TimedNode>(p => p is Project or ProjectEvaluation);
                if (project != null &&
                    matcher.IsMatch(project) != null &&
                    matcher.IsTimeIntervalMatch(project))
                {
                    return true;
                }

                return false;
            }

            foreach (var parent in node.GetParentChainExcludingThis())
            {
                if (matcher.IsMatch(parent) != null && matcher.IsTimeIntervalMatch(parent))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

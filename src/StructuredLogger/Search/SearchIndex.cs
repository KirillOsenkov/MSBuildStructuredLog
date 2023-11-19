using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StructuredLogViewer;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class SearchIndex
    {
        private int stringCount;

        private readonly string[] strings;
        private byte[] bitVector;
        private Dictionary<string, int> stringToIndexMap = new Dictionary<string, int>();
        private ChunkedList<NodeEntry> nodeEntries;

        private int typeKeyword;
        private int taskString;

        public int MaxResults { get; set; }
        public bool MarkResultsInTree { get; set; }

        public SearchIndex(Build build)
        {
            int chunkSize = 1048576;

            nodeEntries = new(chunkSize);

            var stringInstances = (ICollection<string>)build.StringTable.Instances;
            stringCount = stringInstances.Count + 1;
            strings = new string[stringCount];
            bitVector = new byte[stringCount];

            strings[0] = "";

            stringInstances.CopyTo(strings, 1);

            for (int i = 0; i < stringCount; i++)
            {
                var stringInstance = strings[i];
                stringToIndexMap[stringInstance] = i;
            }

            Visit(build);
        }

        private int GetStringIndex(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            stringToIndexMap.TryGetValue(text, out int index);
            return index;
        }

        private void Visit(BaseNode node)
        {
            var entry = GetEntry(node);
            nodeEntries.Add(entry);

            if (node is TreeNode treeNode && treeNode.HasChildren)
            {
                var children = treeNode.Children;
                for (int i = 0; i < children.Count; i++)
                {
                    var child = children[i];
                    Visit(child);
                }
            }
        }

        private NodeEntry GetEntry(BaseNode node)
        {
            var entry = new NodeEntry(node);

            var fields = NodeQueryMatcher.PopulateSearchFields(node);
            int count = fields.count;

            entry.Field1 = GetStringIndex(fields.array[0]);
            if (count > 1)
            {
                entry.Field2 = GetStringIndex(fields.array[1]);
                if (count > 2)
                {
                    entry.Field3 = GetStringIndex(fields.array[2]);
                    if (count > 3)
                    {
                        entry.Field4 = GetStringIndex(fields.array[3]);
                        if (count > 4)
                        {
                            entry.Field5 = GetStringIndex(fields.array[4]);
                            if (count > 5)
                            {
                                entry.Field6 = GetStringIndex(fields.array[5]);
                            }
                        }
                    }
                }
            }

            return entry;
        }

        public IEnumerable<SearchResult> FindNodes(string query, CancellationToken cancellationToken)
        {
            List<SearchResult> results = new();

            var matcher = new NodeQueryMatcher(query, strings, cancellationToken, precomputeMatchesInStrings: false);

            // we assume there are 8 words or less in the query, so we can use 1 byte per string instance
            var terms = matcher.Words.Take(8).ToArray();

            if (PlatformUtilities.HasThreads)
            {
                Parallel.For(0, stringCount, stringIndex =>
                {
                    ComputeBits(terms, stringIndex);
                });
            }
            else
            {
                for (int stringIndex = 0; stringIndex < stringCount; stringIndex++)
                {
                    ComputeBits(terms, stringIndex);
                }
            }

            typeKeyword = GetStringIndex(matcher.TypeKeyword);
            taskString = GetStringIndex(Strings.Task);

            bool searching = true;

            if (PlatformUtilities.HasThreads)
            {
                Dictionary<List<NodeEntry>, List<SearchResult>> resultsByChunk = new();

                Parallel.ForEach(nodeEntries.Chunks, chunk =>
                {
                    List<SearchResult> chunkResults = new();
                    SearchChunk(chunkResults, matcher, terms, searching: true, chunk);
                    lock (resultsByChunk)
                    {
                        resultsByChunk[chunk] = chunkResults;
                    }
                });

                foreach (var chunk in nodeEntries.Chunks)
                {
                    if (resultsByChunk.TryGetValue(chunk, out var chunkResults))
                    {
                        for (int i = 0; i < chunkResults.Count; i++)
                        {
                            var result = chunkResults[i];
                            results.Add(result);
                            if (results.Count >= MaxResults)
                            {
                                break;
                            }
                        }
                    }

                    if (results.Count >= MaxResults)
                    {
                        break;
                    }
                }
            }
            else
            {
                foreach (var chunk in nodeEntries.Chunks)
                {
                    searching = SearchChunk(results, matcher, terms, searching, chunk);
                }
            }

            if (MarkResultsInTree)
            {
                foreach (var result in results)
                {
                    foreach (var parent in result.Node.GetParentChainExcludingThis())
                    {
                        parent.ContainsSearchResult = true;
                    }
                }
            }

            return results;
        }

        private bool SearchChunk(
            List<SearchResult> results,
            NodeQueryMatcher matcher,
            Term[] terms,
            bool searching,
            List<NodeEntry> chunk)
        {
            for (int i = 0; i < chunk.Count; i++)
            {
                var entry = chunk[i];
                bool match = false;

                if (searching && IsMatch(matcher, entry, terms) is { } searchResult)
                {
                    match = true;
                    results.Add(searchResult);
                    if (results.Count >= MaxResults)
                    {
                        searching = false;
                        if (!MarkResultsInTree)
                        {
                            break;
                        }
                    }
                }

                if (MarkResultsInTree)
                {
                    entry.Node.IsSearchResult = match;
                    entry.Node.ContainsSearchResult = false;
                }
            }

            return searching;
        }

        private void ComputeBits(Term[] terms, int stringIndex)
        {
            var stringInstance = strings[stringIndex];
            byte bits = 0;

            for (int termIndex = 0; termIndex < terms.Length; termIndex++)
            {
                var term = terms[termIndex];
                var word = term.Word;

                bool stringMatchesWord = false;

                if (term.Quotes)
                {
                    if (stringInstance.Equals(word, StringComparison.OrdinalIgnoreCase))
                    {
                        stringMatchesWord = true;
                    }
                }
                else
                {
                    if (stringInstance.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        stringMatchesWord = true;
                    }
                }

                if (stringMatchesWord)
                {
                    bits |= (byte)(1 << (termIndex));
                }
            }

            bitVector[stringIndex] = bits;
        }

        public SearchResult IsMatch(NodeQueryMatcher matcher, NodeEntry entry, Term[] terms)
        {
            SearchResult result = null;

            var node = entry.Node;
            int nodeIndex = matcher.NodeIndex;

            if (nodeIndex > -1)
            {
                if (node is TimedNode timedNode && timedNode.Index == nodeIndex)
                {
                    result = new SearchResult(node);
                    var indexString = nodeIndex.ToString();
                    result.AddMatch("Node id: " + indexString, indexString);
                    return result;
                }
            }

            if (typeKeyword != 0)
            {
                // zeroth field is always the type
                if (typeKeyword == entry.Field1 ||
                    // special case for types derived from Task, $task should still work
                    (typeKeyword == taskString && entry.Field2 == taskString))
                {
                    // this node is of the type that we need, search other fields
                    if (result == null)
                    {
                        result = new SearchResult(node, matcher.IncludeDuration, matcher.IncludeStart, matcher.IncludeEnd);
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
            int termCount = terms.Length;

            var nameToSearch = matcher.NameToSearch;
            var valueToSearch = matcher.ValueToSearch;

            for (int i = 0; i < termCount; i++)
            {
                bool anyFieldMatched = false;
                Term term = terms[i];
                string word = term.Word;

                for (int j = 0; j < 6; j++)
                {
                    int field;
                    if (j == 0)
                    {
                        field = entry.Field1;
                    }
                    else if (j == 1)
                    {
                        field = entry.Field2;
                    }
                    else if (j == 2)
                    {
                        field = entry.Field3;
                    }
                    else if (j == 3)
                    {
                        field = entry.Field4;
                    }
                    else if (j == 4)
                    {
                        field = entry.Field5;
                    }
                    else
                    {
                        field = entry.Field6;
                    }

                    if (field == 0)
                    {
                        break;
                    }

                    byte bits = bitVector[field];
                    if ((bits & (1 << i)) == 0)
                    {
                        continue;
                    }

                    if (result == null)
                    {
                        result = new SearchResult(node, matcher.IncludeDuration, matcher.IncludeStart, matcher.IncludeEnd);
                    }

                    // if matched on the type of the node (always field 0), special case it
                    if (j == 0)
                    {
                        result.AddMatchByNodeType();
                    }
                    else
                    {
                        string fullText = strings[field];
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

            // if both name and value are specified, they both have to match
            if (nameToSearch != default && valueToSearch != default && (!nameMatched || !valueMatched))
            {
                return null;
            }

            bool showResult = matcher.IncludeMatchers.Count == 0;
            foreach (NodeQueryMatcher includeMatcher in matcher.IncludeMatchers)
            {
                if (!showResult)
                {
                    showResult = NodeQueryMatcher.IsUnder(includeMatcher, result);
                }
            }

            if (!showResult)
            {
                return null;
            }

            foreach (NodeQueryMatcher excludeMatcher in matcher.ExcludeMatchers)
            {
                if (NodeQueryMatcher.IsUnder(excludeMatcher, result))
                {
                    return null;
                }
            }

            return result;
        }
    }

    public class NodeEntry
    {
        public NodeEntry(BaseNode node)
        {
            Node = node;
        }

        public BaseNode Node { get; }

        public int Field1;
        public int Field2;
        public int Field3;
        public int Field4;
        public int Field5;
        public int Field6;

        public int this[int index]
        {
            get => GetField(index);
        }

        public int GetField(int index)
        {
            return index switch
            {
                0 => Field1,
                1 => Field2,
                2 => Field3,
                3 => Field4,
                4 => Field5,
                5 => Field6,
                _ => 0
            };
        }
    }

    public class ChunkedList<T>
    {
        public int ChunkSize { get; }

        private List<List<T>> chunks = new List<List<T>>();

        public ChunkedList() : this(1048576)
        {
        }

        public ChunkedList(int chunkSize)
        {
            ChunkSize = chunkSize;
        }

        public void Add(T item)
        {
            AddChunk();
            List<T> chunk = chunks[chunks.Count - 1];
            chunk.Add(item);
        }

        private void AddChunk()
        {
            if (chunks.Count == 0 || chunks[chunks.Count - 1].Count >= ChunkSize)
            {
                chunks.Add(new List<T>(ChunkSize));
            }
        }

        public IList<List<T>> Chunks => chunks;
    }
}

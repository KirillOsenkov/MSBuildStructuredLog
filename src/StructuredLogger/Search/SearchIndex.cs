using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using StructuredLogViewer;
using TPLTask = System.Threading.Tasks.Task;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class SearchIndex
    {
        private readonly int stringCount;

        private readonly string[] strings;
        private readonly byte[] bitVector;
        private readonly ChunkedList<NodeEntry> nodeEntries;
        private Dictionary<string, int> stringToIndexMap = new(StringComparer.Ordinal);

        private readonly int taskString;
        private int typeKeyword;

        public int MaxResults { get; set; }
        public bool MarkResultsInTree { get; set; }
        public string[] Strings => strings;
        public int NodeCount => nodeEntries.Count;

        public TimeSpan PrecalculationDuration;

        private readonly bool hasThreads = PlatformUtilities.HasThreads;
        private readonly Build build;

        public SearchIndex(Build build)
        {
            int chunkSize = 1048576;

            nodeEntries = new(chunkSize);

            build.StringTable.Seal();

            strings = (string[])build.StringTable.Instances;
            stringCount = strings.Length;

            bitVector = new byte[stringCount];

            for (int i = 0; i < stringCount; i++)
            {
                var stringInstance = strings[i];
                stringToIndexMap[stringInstance] = i;
            }

            taskString = GetStringIndex(Logging.StructuredLogger.Strings.Task);

            if (hasThreads)
            {
                PopulateEntriesInParallel(build);
            }
            else
            {
                Visit(build);
            }

            stringToIndexMap = null;
            this.build = build;
        }

        public const int BufferSize = 8192;

        private void PopulateEntriesInParallel(Build build)
        {
            var workQueue = new BlockingCollection<Work>();
            var bufferQueue = new Rental<Work>(() => new() { SearchIndex = this });

            var bucket = bufferQueue.Get();

            void AddNode(BaseNode node)
            {
                var nodes = bucket.Nodes;
                if (nodes.Count < BufferSize)
                {
                    nodes.Add(node);
                }
                else
                {
                    bucket.StartProcessing();
                    workQueue.Add(bucket);
                    bucket = bufferQueue.Get();
                    bucket.Nodes.Add(node);
                }
            }

            var producerTask = TPLTask.Run(() =>
            {
                AddNodes(build, AddNode);

                // flush the last (incomplete) bucket
                bucket.StartProcessing();
                workQueue.Add(bucket);
                workQueue.CompleteAdding();
            });

            var enumerable = workQueue.GetConsumingEnumerable();

            foreach (var work in enumerable)
            {
                work.Wait();

                foreach (var entry in work.Entries)
                {
                    nodeEntries.Add(entry);
                }

                work.Clear();
                bufferQueue.Return(work);
            }
        }

        class Work
        {
            public readonly List<BaseNode> Nodes = new List<BaseNode>(BufferSize);
            public readonly List<NodeEntry> Entries = new List<NodeEntry>(BufferSize);
            public SearchIndex SearchIndex;

            private TPLTask task;

            public void StartProcessing()
            {
                task = TPLTask.Run(() => Process());
            }

            private void Process()
            {
                var nodes = Nodes;
                var entries = Entries;
                int nodesCount = Nodes.Count;
                for (int i = 0; i < nodesCount; i++)
                {
                    var node = nodes[i];
                    var entry = SearchIndex.GetEntry(node);
                    entries.Add(entry);
                }
            }

            public void Wait()
            {
                task.Wait();
            }

            internal void Clear()
            {
                Entries.Clear();
                Nodes.Clear();
                task = null;
            }
        }

        private void AddNodes(BaseNode node, Action<BaseNode> collector)
        {
            collector(node);

            if (node is TreeNode treeNode && treeNode.HasChildren)
            {
                var children = treeNode.Children;
                for (int i = 0; i < children.Count; i++)
                {
                    var child = children[i];
                    AddNodes(child, collector);
                }
            }
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
            var fields = NodeQueryMatcher.PopulateSearchFields(node);
            int count = fields.count;

            NodeEntry entry = null;

            // most nodes have 3 or 4 entries, so put these cases first
            if (count == 3)
            {
                entry = new NodeEntry3()
                {
                    Field1 = GetStringIndex(fields.array[0]),
                    Field2 = GetStringIndex(fields.array[1]),
                    Field3 = GetStringIndex(fields.array[2])
                };
            }
            else if (count == 4)
            {
                entry = new NodeEntry4()
                {
                    Field1 = GetStringIndex(fields.array[0]),
                    Field2 = GetStringIndex(fields.array[1]),
                    Field3 = GetStringIndex(fields.array[2]),
                    Field4 = GetStringIndex(fields.array[3])
                };
            }
            else if (count == 1)
            {
                entry = new NodeEntry()
                {
                    Field1 = GetStringIndex(fields.array[0])
                };
            }
            else if (count == 2)
            {
                entry = new NodeEntry2()
                {
                    Field1 = GetStringIndex(fields.array[0]),
                    Field2 = GetStringIndex(fields.array[1])
                };
            }
            else if (count == 5)
            {
                entry = new NodeEntry5()
                {
                    Field1 = GetStringIndex(fields.array[0]),
                    Field2 = GetStringIndex(fields.array[1]),
                    Field3 = GetStringIndex(fields.array[2]),
                    Field4 = GetStringIndex(fields.array[3]),
                    Field5 = GetStringIndex(fields.array[4])
                };
            }
            else if (count == 6)
            {
                entry = new NodeEntry6()
                {
                    Field1 = GetStringIndex(fields.array[0]),
                    Field2 = GetStringIndex(fields.array[1]),
                    Field3 = GetStringIndex(fields.array[2]),
                    Field4 = GetStringIndex(fields.array[3]),
                    Field5 = GetStringIndex(fields.array[4]),
                    Field6 = GetStringIndex(fields.array[5])
                };
            }

            entry.Node = node;

            return entry;
        }

        public IEnumerable<SearchResult> FindNodes(string query, CancellationToken cancellationToken)
        {
            List<SearchResult> results = new();
            typeKeyword = 0;

            var matcher = new NodeQueryMatcher(query);

            foreach (var searchExtension in build.SearchExtensions)
            {
                if (searchExtension.TryGetResults(matcher, results, MaxResults))
                {
                    return results;
                }
            }

            // we assume there are 8 words or less in the query, so we can use 1 byte per string instance
            var terms = matcher.Terms.Take(8).ToArray();
            string typeString = matcher.TypeKeyword;

            var stopwatch = Stopwatch.StartNew();

            if (PlatformUtilities.HasThreads)
            {
                Parallel.For(0, stringCount, stringIndex =>
                {
                    ComputeBits(terms, stringIndex, typeString);
                });
            }
            else
            {
                for (int stringIndex = 0; stringIndex < stringCount; stringIndex++)
                {
                    ComputeBits(terms, stringIndex, typeString);
                }
            }

            PrecalculationDuration = stopwatch.Elapsed;

            if (cancellationToken.IsCancellationRequested)
            {
                return results;
            }

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

                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
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
                    if (cancellationToken.IsCancellationRequested)
                    {
                        if (MarkResultsInTree)
                        {
                            searching = false;
                        }
                        else
                        {
                            return results;
                        }
                    }

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

                if (searching &&
                    IsMatch(matcher, entry, terms) is { } searchResult &&
                    matcher.IsTimeIntervalMatch(entry.Node))
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

        private void ComputeBits(Term[] terms, int stringIndex, string typeString)
        {
            var stringInstance = strings[stringIndex];
            byte bits = 0;

            if (typeKeyword == 0 && typeString is not null && string.Equals(typeString, stringInstance, StringComparison.OrdinalIgnoreCase))
            {
                typeKeyword = stringIndex;
            }

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

                return null;
            }

            if (typeKeyword != 0)
            {
                // zeroth field is always the type
                if (entry.Field1 == typeKeyword
                    // special case for types derived from Task, $task should still work
                    // || (entry.Field1 == taskString && entry.GetField(1) == typeKeyword)
                    )
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

            int nameTermIndex = matcher.NameTermIndex;
            int valueTermIndex = matcher.ValueTermIndex;

            for (int termIndex = 0; termIndex < termCount; termIndex++)
            {
                bool anyFieldMatched = false;
                Term term = terms[termIndex];
                string word = term.Word;
                bool quotes = term.Quotes;

                for (int fieldIndex = 0; fieldIndex < NodeQueryMatcher.MaxArraySize; fieldIndex++)
                {
                    int field = entry.GetField(fieldIndex);
                    if (field == 0)
                    {
                        break;
                    }

                    byte bits = bitVector[field];
                    if ((bits & (1 << termIndex)) == 0)
                    {
                        continue;
                    }

                    string fullText = strings[field];
                    if (quotes && !string.Equals(word, fullText, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (result == null)
                    {
                        result = new SearchResult(node, matcher.IncludeDuration, matcher.IncludeStart, matcher.IncludeEnd);
                    }

                    // if matched on the type of the node (always field 0), special case it
                    if (fieldIndex == 0)
                    {
                        result.AddMatchByNodeType();
                    }
                    else
                    {
                        var nameValueNode = node as NameValueNode;

                        // NameValueNode is a special case: have to check in which field to search
                        if (nameValueNode != null && (nameTermIndex != -1 || valueTermIndex != -1))
                        {
                            if (fieldIndex == 1 && termIndex == nameTermIndex)
                            {
                                result.AddMatch(fullText, word, addAtBeginning: true);
                                nameMatched = true;
                                anyFieldMatched = true;
                                break;
                            }

                            if (fieldIndex == 2 && termIndex == valueTermIndex)
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
            if (nameTermIndex != -1 && valueTermIndex != -1 && (!nameMatched || !valueMatched))
            {
                return null;
            }

            bool showResult = matcher.IncludeMatchers.Count == 0;
            foreach (NodeQueryMatcher includeMatcher in matcher.IncludeMatchers)
            {
                if (!showResult)
                {
                    showResult = NodeQueryMatcher.IsUnder(includeMatcher, result.Node);
                }
            }

            if (!showResult)
            {
                return null;
            }

            foreach (NodeQueryMatcher excludeMatcher in matcher.ExcludeMatchers)
            {
                if (NodeQueryMatcher.IsUnder(excludeMatcher, result.Node))
                {
                    return null;
                }
            }

            foreach (NodeQueryMatcher notMatcher in matcher.NotMatchers)
            {
                if (notMatcher.IsMatch(result.Node) != null)
                {
                    return null;
                }
            }

            if (matcher.Skipped != null && node is Target target)
            {
                if (target.Skipped != matcher.Skipped)
                {
                    return null;
                }
            }

            return result;
        }
    }

    public class NodeEntry
    {
        public BaseNode Node;

        public int Field1;

        public int this[int index]
        {
            get => GetField(index);
        }

        public virtual int GetField(int index)
        {
            if (index == 0)
            {
                return Field1;
            }

            return 0;
        }
    }

    public class NodeEntry2 : NodeEntry
    {
        public int Field2;

        public override int GetField(int index)
        {
            if (index == 1)
            {
                return Field2;
            }

            return base.GetField(index);
        }
    }

    public class NodeEntry3 : NodeEntry2
    {
        public int Field3;

        public override int GetField(int index)
        {
            if (index == 2)
            {
                return Field3;
            }

            return base.GetField(index);
        }
    }

    public class NodeEntry4 : NodeEntry3
    {
        public int Field4;

        public override int GetField(int index)
        {
            if (index == 3)
            {
                return Field4;
            }

            return base.GetField(index);
        }
    }

    public class NodeEntry5 : NodeEntry4
    {
        public int Field5;

        public override int GetField(int index)
        {
            if (index == 4)
            {
                return Field5;
            }

            return base.GetField(index);
        }
    }

    public class NodeEntry6 : NodeEntry5
    {
        public int Field6;

        public override int GetField(int index)
        {
            if (index == 5)
            {
                return Field6;
            }

            return base.GetField(index);
        }
    }

    /// <summary>
    /// Getting string hashcode is very expensive. This was a failed experiment
    /// to use object hashcode instead of string hashcode to look up strings.
    /// Unfortunately strings aren't properly deduplicated in our string table,
    /// so two equal strings can be two different instances with different
    /// hashcodes. If we ever have proper deduplication, this might buy us
    /// 28s -> 21s improvement.
    /// </summary>
    struct StringWrapper : IEquatable<StringWrapper>
    {
        public string StringInstance;

        public StringWrapper(string instance)
        {
            StringInstance = instance;
        }

        public override int GetHashCode()
        {
            return RuntimeHelpers.GetHashCode(StringInstance);
        }

        public override bool Equals(object obj)
        {
            return Equals((StringWrapper)obj);
        }

        public bool Equals(StringWrapper other)
        {
            return object.ReferenceEquals(StringInstance, other.StringInstance) ||
                StringInstance.Equals(other.StringInstance, StringComparison.Ordinal);
        }

        public override string ToString()
        {
            return StringInstance;
        }
    }
}

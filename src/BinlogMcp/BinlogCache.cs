using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Logging.StructuredLogger;
using StructuredLogViewer;

namespace BinlogMcp
{
    public sealed class LoadedBinlog
    {
        private TimedNode[] indexMap;
        private SourceFileResolver sourceFileResolver;
        private PreprocessedFileManager preprocessedFileManager;
        private PropertiesAndItemsSearch propertiesAndItemsSearch;
        private PropertyGraph propertyGraph;
        private readonly object preprocessSync = new();

        public string Path { get; init; }
        public Build Build { get; init; }
        public long FileSize { get; init; }
        public DateTime LastWriteTimeUtc { get; init; }
        public long EstimatedMemoryBytes { get; init; }
        public DateTime LoadedAtUtc { get; init; }
        public DateTime LastAccessedUtc { get; set; }

        /// <summary>
        /// Lazily-built lookup from <see cref="TimedNode.Index"/> to node.
        /// Indices are assigned densely from 0 by <c>BuildAnalyzer</c>, so a
        /// flat array is the natural representation.
        /// </summary>
        public TimedNode[] IndexMap
        {
            get
            {
                var local = indexMap;
                if (local != null)
                {
                    return local;
                }

                var list = new List<TimedNode>();
                Build.VisitAllChildren<TimedNode>(node =>
                {
                    int i = node.Index;
                    while (list.Count <= i)
                    {
                        list.Add(null);
                    }

                    list[i] = node;
                });

                local = list.ToArray();
                indexMap = local;
                return local;
            }
        }

        // Lazily constructed source-file pipeline matching the viewer's wiring:
        // SourceFileResolver → PreprocessedFileManager → PropertyGraph subscribes
        // to PropertiesAndItemsSearch.AugmentResults so that scoped searches over
        // properties also surface the cross-import property graph as a result.
        public SourceFileResolver SourceFileResolver
        {
            get
            {
                EnsurePropertiesPipeline();
                return sourceFileResolver;
            }
        }

        public PreprocessedFileManager PreprocessedFileManager
        {
            get
            {
                EnsurePropertiesPipeline();
                return preprocessedFileManager;
            }
        }

        public PropertiesAndItemsSearch PropertiesAndItemsSearch
        {
            get
            {
                EnsurePropertiesPipeline();
                return propertiesAndItemsSearch;
            }
        }

        public PropertyGraph PropertyGraph
        {
            get
            {
                EnsurePropertiesPipeline();
                return propertyGraph;
            }
        }

        private void EnsurePropertiesPipeline()
        {
            if (propertyGraph != null)
            {
                return;
            }

            lock (preprocessSync)
            {
                if (propertyGraph != null)
                {
                    return;
                }

                var resolver = new SourceFileResolver(Build.SourceFiles ?? Array.Empty<ArchiveFile>());
                var preprocessed = new PreprocessedFileManager(Build, resolver);
                Build.TextProvider = evaluation => preprocessed.GetPreprocessedText(evaluation);

                var search = new PropertiesAndItemsSearch();
                // Constructor subscribes to search.AugmentResults; leave
                // AppendDependencyReferences null since the MCP doesn't render
                // clickable buttons.
                var graph = new PropertyGraph(preprocessed, search);

                sourceFileResolver = resolver;
                preprocessedFileManager = preprocessed;
                propertiesAndItemsSearch = search;
                propertyGraph = graph;
            }
        }
    }

    /// <summary>
    /// Caches loaded binlogs keyed by full path. Evicts least-recently-used
    /// entries when the estimated memory budget would be exceeded.
    /// </summary>
    /// <remarks>
    /// Memory is estimated as fileSize * <see cref="MemoryMultiplier"/>. This
    /// is a coarse heuristic: a 400 MB binlog typically expands to ~40 GB of
    /// managed objects after deserialization, indexing and analysis.
    /// </remarks>
    public sealed class BinlogCache
    {
        // Calibrated against observed binlog → RAM expansion.
        public const long MemoryMultiplier = 100;

        private readonly object syncRoot = new();
        private readonly Dictionary<string, LoadedBinlog> entries =
            new(PathComparer);

        public BinlogCache(long? memoryBudgetBytes = null)
        {
            MemoryBudgetBytes = memoryBudgetBytes ?? GetDefaultMemoryBudget();
        }

        public long MemoryBudgetBytes { get; }

        public long EstimatedMemoryUsedBytes
        {
            get
            {
                lock (syncRoot)
                {
                    long sum = 0;
                    foreach (var entry in entries.Values)
                    {
                        sum += entry.EstimatedMemoryBytes;
                    }

                    return sum;
                }
            }
        }

        public LoadedBinlog Load(string path, bool forceReload = false)
        {
            path = NormalizePath(path);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Binlog not found: {path}", path);
            }

            var info = new FileInfo(path);
            long estimatedMemory = EstimateMemory(info.Length);

            lock (syncRoot)
            {
                if (!forceReload &&
                    entries.TryGetValue(path, out var cached) &&
                    cached.FileSize == info.Length &&
                    cached.LastWriteTimeUtc == info.LastWriteTimeUtc)
                {
                    cached.LastAccessedUtc = DateTime.UtcNow;
                    return cached;
                }

                // Evict the existing entry (if any) before reloading so it
                // doesn't double-count toward the budget.
                if (entries.Remove(path))
                {
                    ForceCollect();
                }

                EvictToFit(estimatedMemory);
            }

            // Read/analyze/index OUTSIDE the lock — these are the slow parts
            // and we don't want to block other cache operations on them.
            var build = Serialization.Read(path);
            BuildAnalyzer.AnalyzeBuild(build);
            build.SearchIndex = new SearchIndex(build);

            // Match the viewer: register the optional search extensions so
            // queries like `$secret` and `$nuget` work out of the box.
            build.SearchExtensions.Add(new SecretsSearch(build));
            build.SearchExtensions.Add(new NuGetSearch(build));

            var entry = new LoadedBinlog
            {
                Path = path,
                Build = build,
                FileSize = info.Length,
                LastWriteTimeUtc = info.LastWriteTimeUtc,
                EstimatedMemoryBytes = estimatedMemory,
                LoadedAtUtc = DateTime.UtcNow,
                LastAccessedUtc = DateTime.UtcNow
            };

            lock (syncRoot)
            {
                entries[path] = entry;
            }

            return entry;
        }

        public bool TryGet(string path, out LoadedBinlog entry)
        {
            path = NormalizePath(path);
            lock (syncRoot)
            {
                if (entries.TryGetValue(path, out entry))
                {
                    entry.LastAccessedUtc = DateTime.UtcNow;
                    return true;
                }

                return false;
            }
        }

        public bool Unload(string path)
        {
            path = NormalizePath(path);
            lock (syncRoot)
            {
                if (entries.Remove(path))
                {
                    ForceCollect();
                    return true;
                }

                return false;
            }
        }

        public int UnloadAll()
        {
            lock (syncRoot)
            {
                int count = entries.Count;
                entries.Clear();
                if (count > 0)
                {
                    ForceCollect();
                }

                return count;
            }
        }

        public IReadOnlyList<LoadedBinlog> List()
        {
            lock (syncRoot)
            {
                return entries.Values.ToArray();
            }
        }

        // Caller must hold syncRoot.
        private void EvictToFit(long incoming)
        {
            // If a single new binlog exceeds the budget, evict everything and
            // try anyway — the user is asking for it explicitly.
            if (incoming >= MemoryBudgetBytes)
            {
                if (entries.Count > 0)
                {
                    entries.Clear();
                    ForceCollect();
                }

                return;
            }

            long used = 0;
            foreach (var entry in entries.Values)
            {
                used += entry.EstimatedMemoryBytes;
            }

            if (used + incoming <= MemoryBudgetBytes)
            {
                return;
            }

            var lru = entries.Values.OrderBy(e => e.LastAccessedUtc).ToList();
            foreach (var entry in lru)
            {
                entries.Remove(entry.Path);
                used -= entry.EstimatedMemoryBytes;
                if (used + incoming <= MemoryBudgetBytes)
                {
                    break;
                }
            }

            ForceCollect();
        }

        private static void ForceCollect()
        {
            // Help large binlog object graphs get reclaimed promptly so the
            // working set tracks the cache state instead of trailing it.
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        private static long EstimateMemory(long fileSize) => fileSize * MemoryMultiplier;

        private static string NormalizePath(string path) => Path.GetFullPath(path);

        private static StringComparer PathComparer =>
            OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal;

        private static long GetDefaultMemoryBudget()
        {
            // TotalAvailableMemoryBytes reflects DOTNET_GCHeapHardLimit if set,
            // otherwise the machine's physical RAM. Use 75% as the budget,
            // leaving headroom for non-cache allocations and OS overhead.
            var info = GC.GetGCMemoryInfo();
            long available = info.TotalAvailableMemoryBytes;
            return (long)(available * 0.75);
        }
    }
}

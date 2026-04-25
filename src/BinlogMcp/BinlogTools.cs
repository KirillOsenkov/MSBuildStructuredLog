using System.ComponentModel;
using System.Linq;
using ModelContextProtocol.Server;

namespace BinlogMcp
{
    [McpServerToolType]
    public static class BinlogTools
    {
        private static readonly BinlogCache Cache = new();

        [McpServerTool(Name = "load_binlog", ReadOnly = true, Idempotent = true)]
        [Description("Loads an MSBuild .binlog file into memory. Subsequent calls with the same path return the cached build unless the file has changed on disk. Must be called before any other tool that operates on the binlog.")]
        public static string LoadBinlog(
            [Description("Absolute path to a .binlog file")] string path)
        {
            var entry = Cache.Load(path);
            return Describe(entry);
        }

        [McpServerTool(Name = "reload_binlog", ReadOnly = true, Idempotent = true)]
        [Description("Re-reads a binlog from disk, replacing the cached version. Use this after a rebuild has overwritten the binlog file.")]
        public static string ReloadBinlog(
            [Description("Absolute path to a .binlog file")] string path)
        {
            var entry = Cache.Load(path, forceReload: true);
            return Describe(entry);
        }

        [McpServerTool(Name = "unload_binlog", ReadOnly = true, Idempotent = true)]
        [Description("Evicts a single binlog from the cache to free memory.")]
        public static string UnloadBinlog(
            [Description("Absolute path to the .binlog file to evict")] string path)
        {
            return Cache.Unload(path) ? $"unloaded {path}" : $"not loaded: {path}";
        }

        [McpServerTool(Name = "unload_all_binlogs", ReadOnly = true, Idempotent = true)]
        [Description("Evicts all loaded binlogs from the cache to free memory.")]
        public static string UnloadAllBinlogs()
        {
            int count = Cache.UnloadAll();
            return $"unloaded {count} binlog(s)";
        }

        [McpServerTool(Name = "list_loaded_binlogs", ReadOnly = true, Idempotent = true)]
        [Description("Lists all binlogs currently loaded in the cache, with file sizes and estimated memory usage.")]
        public static string ListLoadedBinlogs()
        {
            var entries = Cache.List();
            if (entries.Count == 0)
            {
                return "no binlogs loaded";
            }

            var lines = entries
                .OrderByDescending(e => e.LastAccessedUtc)
                .Select(e => $"{e.Path}\tfileSize={e.FileSize:n0}\testMem={e.EstimatedMemoryBytes:n0}\tlastAccessed={e.LastAccessedUtc:o}");
            return string.Join("\n", lines);
        }

        private static string Describe(LoadedBinlog entry)
        {
            var build = entry.Build;
            return string.Join("\n", new[]
            {
                $"path: {entry.Path}",
                $"fileSize: {entry.FileSize:n0} bytes",
                $"estimatedMemory: {entry.EstimatedMemoryBytes:n0} bytes",
                $"succeeded: {build.Succeeded}",
                $"duration: {build.Duration}",
                $"msbuildVersion: {build.MSBuildVersion}",
            });
        }
    }
}

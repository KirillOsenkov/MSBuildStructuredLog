using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Build.Logging.StructuredLogger;
using ModelContextProtocol.Server;

namespace BinlogMcp;

// Tools for inspecting source/text files embedded inside a binlog
// (the .buildsources.zip payload exposed via Build.SourceFiles).
public static partial class BinlogTools
{
    private const int DefaultReadWindow = 500;
    private const int MaxReadWindow = 5000;
    private const int DefaultSearchMaxPerFile = 20;

    [McpServerTool(Name = "list_files", ReadOnly = true, Idempotent = true)]
    [Description(@"Lists text files embedded inside the binlog (the .buildsources.zip payload — typically every project, .props/.targets, and Directory.Build.* file MSBuild touched during the build).

Each line: <fullPath>\t<lines>\t<bytes>. Sorted by full path so pagination is stable.

Use this first to discover what's available; then read_file or search_files to inspect contents.")]
    public static string ListFiles(
        [Description("Absolute path to a .binlog file")] string path,
        [Description("Optional case-insensitive substring filter on the full path (e.g. 'Common.props', 'BinlogTool')")] string pathFilter = null,
        [Description("Number of leading entries to skip for paging (default 0)")] int? skip = null,
        [Description("Maximum number of entries to return (default 200, max 5000)")] int? maxResults = null) => Run(() =>
    {
        int offset = Math.Max(skip ?? 0, 0);
        int take = Math.Clamp(maxResults ?? DefaultMaxResults, 1, MaxAllowedResults);

        var files = GetEmbeddedFiles(path);
        IEnumerable<KeyValuePair<string, SourceText>> filtered = files;
        if (!string.IsNullOrEmpty(pathFilter))
        {
            string normalizedFilter = NormalizePathForLookup(pathFilter);
            filtered = filtered.Where(kvp =>
                NormalizePathForLookup(kvp.Key).IndexOf(normalizedFilter, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        var ordered = filtered
            .OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        int total = ordered.Length;
        var page = ordered.Skip(offset).Take(take).ToArray();

        var sb = new StringBuilder();
        sb.Append("total: ").Append(total).Append(" file(s)");
        if (!string.IsNullOrEmpty(pathFilter))
        {
            sb.Append(" matching '").Append(pathFilter).Append('\'');
        }

        sb.AppendLine();
        sb.Append("returned: ").Append(page.Length)
          .Append(" (skip=").Append(offset)
          .Append(", take=").Append(take)
          .AppendLine(")");

        if (page.Length == 0)
        {
            sb.AppendLine("(no files)");
            return sb.ToString();
        }

        foreach (var kvp in page)
        {
            int lineCount = kvp.Value?.Lines.Count ?? 0;
            int byteCount = kvp.Value?.Text?.Length ?? 0;
            sb.Append(kvp.Key).Append('\t')
              .Append(lineCount).Append('\t')
              .Append(byteCount).AppendLine();
        }

        return sb.ToString();
    });

    [McpServerTool(Name = "read_file", ReadOnly = true, Idempotent = true)]
    [Description(@"Reads a slice of an embedded file. Line numbers are 1-based and inclusive on both ends.

The 'file' parameter may be the exact full path returned by list_files, or any unique suffix of it (so 'Microsoft.Common.props' or 'Directory.Build.props' usually works). If the suffix matches more than one file, the candidates are returned and the call fails — pass a longer suffix.

Defaults to lines 1..500. Hard cap is 5000 lines per call; for larger files, page with successive calls.

Output format:
file: <fullPath>
totalLines: <n>
lines: <start>-<end>
  <n> | <text>
  ...")]
    public static string ReadFile(
        [Description("Absolute path to a .binlog file")] string path,
        [Description("Embedded file path, or a unique suffix of one (case-insensitive)")] string file,
        [Description("First line to return (1-based, inclusive). Default 1.")] int? startLine = null,
        [Description("Last line to return (1-based, inclusive). Default startLine + 500. Capped to startLine + 5000.")] int? endLine = null) => Run(() =>
    {
        if (string.IsNullOrEmpty(file))
        {
            throw new ArgumentException("file is required");
        }

        var (fullPath, text) = ResolveFile(path, file);
        int total = text.Lines.Count;

        int start = Math.Max(startLine ?? 1, 1);
        int end = endLine ?? (start + DefaultReadWindow - 1);
        if (end < start)
        {
            end = start;
        }

        // Cap window before clamping to total so the user sees the right error.
        int maxEnd = start + MaxReadWindow - 1;
        if (end > maxEnd)
        {
            end = maxEnd;
        }

        if (total == 0)
        {
            var emptySb = new StringBuilder();
            emptySb.Append("file: ").AppendLine(fullPath);
            emptySb.AppendLine("totalLines: 0");
            emptySb.AppendLine("(empty)");
            return emptySb.ToString();
        }

        if (start > total)
        {
            throw new ArgumentOutOfRangeException(
                nameof(startLine),
                $"startLine {start} is past end of file (totalLines={total})");
        }

        if (end > total)
        {
            end = total;
        }

        int width = end.ToString().Length;
        var sb = new StringBuilder();
        sb.Append("file: ").AppendLine(fullPath);
        sb.Append("totalLines: ").Append(total).AppendLine();
        sb.Append("lines: ").Append(start).Append('-').Append(end).AppendLine();

        for (int i = start; i <= end; i++)
        {
            // SourceText.Lines is 0-indexed; user-facing lines are 1-based.
            string lineText = text.GetLineText(i - 1);
            sb.Append(i.ToString().PadLeft(width)).Append(" | ").AppendLine(lineText);
        }

        return sb.ToString();
    });

    [McpServerTool(Name = "search_files", ReadOnly = true, Idempotent = true)]
    [Description(@"Searches for text across embedded files. ripgrep-style output, grouped by file.

Query forms:
- Foo                — case-insensitive substring (default; ignoreCase param applies)
- /^<Project/        — regex (when wrapped in /.../). ignoreCase applies.
- ""exact phrase""     — quoted = case-sensitive literal (ignoreCase ignored)

Use pathFilter (case-insensitive substring) or files (explicit list of full paths or unique suffixes) to scope. Two bounds: maxResults (overall) and maxResultsPerFile (so one noisy file can't crowd out the rest).

Each match line: <lineNumber>: <text>. With contextLines > 0, surrounding lines are prefixed with '-' instead of ':'.")]
    public static string SearchFiles(
        [Description("Absolute path to a .binlog file")] string path,
        [Description("Search query. Plain text = substring; /regex/ = regex; \"quoted\" = case-sensitive literal.")] string query,
        [Description("Optional case-insensitive substring filter on the full path")] string pathFilter = null,
        [Description("Optional explicit list of files to search (full paths or unique suffixes). If set, pathFilter is ignored.")] string[] files = null,
        [Description("Case-insensitive matching for substring/regex queries (default true). Ignored for quoted literals.")] bool? ignoreCase = null,
        [Description("Number of context lines before/after each match (default 0)")] int? contextLines = null,
        [Description("Maximum total matches to return (default 200, max 5000)")] int? maxResults = null,
        [Description("Maximum matches per file (default 20)")] int? maxResultsPerFile = null,
        [Description("Number of leading matches to skip for paging (default 0)")] int? skip = null) => Run(() =>
    {
        if (string.IsNullOrEmpty(query))
        {
            throw new ArgumentException("query is required");
        }

        int offset = Math.Max(skip ?? 0, 0);
        int take = Math.Clamp(maxResults ?? DefaultMaxResults, 1, MaxAllowedResults);
        int perFile = Math.Max(maxResultsPerFile ?? DefaultSearchMaxPerFile, 1);
        int context = Math.Clamp(contextLines ?? 0, 0, 20);
        bool ci = ignoreCase ?? true;

        var matcher = ParseQuery(query, ci);

        var allFiles = GetEmbeddedFiles(path);

        // Build the candidate file set.
        IEnumerable<KeyValuePair<string, SourceText>> candidates;
        if (files != null && files.Length > 0)
        {
            var resolved = new List<KeyValuePair<string, SourceText>>(files.Length);
            foreach (var f in files)
            {
                var (fullPath, text) = ResolveFile(path, f);
                resolved.Add(new KeyValuePair<string, SourceText>(fullPath, text));
            }

            candidates = resolved;
        }
        else if (!string.IsNullOrEmpty(pathFilter))
        {
            string normalizedFilter = NormalizePathForLookup(pathFilter);
            candidates = allFiles.Where(kvp =>
                NormalizePathForLookup(kvp.Key).IndexOf(normalizedFilter, StringComparison.OrdinalIgnoreCase) >= 0);
        }
        else
        {
            candidates = allFiles;
        }

        var ordered = candidates
            .OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        // Collect matches per file (respecting per-file cap), then aggregate.
        var perFileMatches = new List<(string FullPath, List<int> LineIndices, SourceText Text)>();
        int totalMatches = 0;
        foreach (var kvp in ordered)
        {
            var text = kvp.Value;
            if (text == null)
            {
                continue;
            }

            var hits = new List<int>();
            int lineCount = text.Lines.Count;
            for (int i = 0; i < lineCount; i++)
            {
                string lineText = text.GetLineText(i);
                if (matcher(lineText))
                {
                    hits.Add(i);
                    if (hits.Count >= perFile)
                    {
                        break;
                    }
                }
            }

            if (hits.Count > 0)
            {
                perFileMatches.Add((kvp.Key, hits, text));
                totalMatches += hits.Count;
            }
        }

        // Build a flat ordered list of (fileIndex, lineIndex) for paging.
        var flat = new List<(int FileIdx, int LineIdx)>(totalMatches);
        for (int f = 0; f < perFileMatches.Count; f++)
        {
            foreach (var li in perFileMatches[f].LineIndices)
            {
                flat.Add((f, li));
            }
        }

        var page = flat.Skip(offset).Take(take).ToArray();

        var sb = new StringBuilder();
        sb.Append("query: ").AppendLine(query);
        sb.Append("total: ").Append(totalMatches).Append(" match(es) in ").Append(perFileMatches.Count).AppendLine(" file(s)");
        sb.Append("returned: ").Append(page.Length)
          .Append(" (skip=").Append(offset)
          .Append(", take=").Append(take)
          .Append(", perFile=").Append(perFile)
          .AppendLine(")");

        if (page.Length == 0)
        {
            sb.AppendLine("(no matches)");
            return sb.ToString();
        }

        int currentFile = -1;
        for (int pageIndex = 0; pageIndex < page.Length;)
        {
            int fileIdx = page[pageIndex].FileIdx;
            if (currentFile != -1)
            {
                sb.AppendLine();
            }

            currentFile = fileIdx;
            var fileEntry = perFileMatches[fileIdx];
            int total = fileEntry.Text.Lines.Count;
            sb.AppendLine(fileEntry.FullPath);

            var hitLines = new HashSet<int>();
            var ranges = new List<(int Start, int End)>();
            while (pageIndex < page.Length && page[pageIndex].FileIdx == fileIdx)
            {
                int lineIdx = page[pageIndex].LineIdx;
                hitLines.Add(lineIdx);
                ranges.Add((
                    Math.Max(0, lineIdx - context),
                    Math.Min(total - 1, lineIdx + context)));
                pageIndex++;
            }

            ranges.Sort((left, right) => left.Start != right.Start
                ? left.Start.CompareTo(right.Start)
                : left.End.CompareTo(right.End));

            var merged = new List<(int Start, int End)>();
            foreach (var range in ranges)
            {
                if (merged.Count == 0 || range.Start > merged[^1].End + 1)
                {
                    merged.Add(range);
                }
                else if (range.End > merged[^1].End)
                {
                    var previous = merged[^1];
                    merged[^1] = (previous.Start, range.End);
                }
            }

            int maxLine = merged[^1].End + 1;
            int width = maxLine.ToString().Length;
            foreach (var range in merged)
            {
                for (int i = range.Start; i <= range.End; i++)
                {
                    char sep = hitLines.Contains(i) ? ':' : '-';
                    sb.Append(' ', 2)
                      .Append((i + 1).ToString().PadLeft(width))
                      .Append(sep).Append(' ')
                      .AppendLine(fileEntry.Text.GetLineText(i));
                }
            }
        }

        return sb.ToString();
    });

    // ----- helpers -----

    private static IReadOnlyDictionary<string, SourceText> GetEmbeddedFiles(string path)
    {
        var entry = Cache.Load(path);
        var resolver = entry.SourceFileResolver?.ArchiveFile;
        if (resolver == null)
        {
            return new Dictionary<string, SourceText>(0);
        }

        return resolver.Files;
    }

    private static (string FullPath, SourceText Text) ResolveFile(string binlog, string file)
    {
        var files = GetEmbeddedFiles(binlog);
        if (files.Count == 0)
        {
            throw new InvalidOperationException("no embedded files in this binlog");
        }

        // 1) exact (after archive-path normalization)
        string normalized = ArchiveFile.CalculateArchivePath(file);
        if (files.TryGetValue(normalized, out var exact))
        {
            return (normalized, exact);
        }

        // 2) unique suffix (case-insensitive). Match on '/'+suffix or full path
        //    so 'Common.props' doesn't accidentally match 'UnCommon.props'.
        var suffixMatches = new List<string>();
        string lookupSuffix = NormalizePathForLookup(file);
        foreach (var key in files.Keys)
        {
            string lookupKey = NormalizePathForLookup(key);
            if (lookupKey.Equals(lookupSuffix, StringComparison.OrdinalIgnoreCase))
            {
                return (key, files[key]);
            }

            if (lookupKey.Length > lookupSuffix.Length &&
                lookupKey.EndsWith(lookupSuffix, StringComparison.OrdinalIgnoreCase) &&
                lookupKey[lookupKey.Length - lookupSuffix.Length - 1] == '/')
            {
                suffixMatches.Add(key);
            }
        }

        if (suffixMatches.Count == 1)
        {
            var key = suffixMatches[0];
            return (key, files[key]);
        }

        if (suffixMatches.Count == 0)
        {
            // 3) last resort: any key containing the substring
            var contains = files.Keys
                .Where(k => NormalizePathForLookup(k).IndexOf(lookupSuffix, StringComparison.OrdinalIgnoreCase) >= 0)
                .Take(20)
                .ToArray();

            if (contains.Length == 1)
            {
                return (contains[0], files[contains[0]]);
            }

            if (contains.Length == 0)
            {
                throw new FileNotFoundException(
                    $"no embedded file matches '{file}'. Use list_files to discover available paths.");
            }

            var sb = new StringBuilder();
            sb.Append("'").Append(file).Append("' matches ").Append(contains.Length).AppendLine(" files (showing up to 20). Pass a longer/more specific path:");
            foreach (var k in contains)
            {
                sb.Append("  ").AppendLine(k);
            }

            throw new InvalidOperationException(sb.ToString());
        }

        var ambiguous = new StringBuilder();
        ambiguous.Append("'").Append(file).Append("' is an ambiguous suffix matching ").Append(suffixMatches.Count).AppendLine(" files. Pass a longer/more specific path:");
        foreach (var k in suffixMatches.Take(20))
        {
            ambiguous.Append("  ").AppendLine(k);
        }

        throw new InvalidOperationException(ambiguous.ToString());
    }

    private static string NormalizePathForLookup(string path)
    {
        return path.Replace('\\', '/');
    }

    private static Func<string, bool> ParseQuery(string query, bool ignoreCase)
    {
        // /regex/
        if (query.Length >= 2 && query[0] == '/' && query[^1] == '/')
        {
            var pattern = query.Substring(1, query.Length - 2);
            var options = RegexOptions.CultureInvariant;
            if (ignoreCase)
            {
                options |= RegexOptions.IgnoreCase;
            }

            Regex regex;
            try
            {
                regex = new Regex(pattern, options, TimeSpan.FromSeconds(2));
            }
            catch (ArgumentException ex)
            {
                throw new ArgumentException($"invalid regex /{pattern}/: {ex.Message}", ex);
            }

            return line => regex.IsMatch(line);
        }

        // "literal"
        if (query.Length >= 2 && query[0] == '"' && query[^1] == '"')
        {
            var literal = query.Substring(1, query.Length - 2);
            return line => line.Contains(literal, StringComparison.Ordinal);
        }

        // substring
        var cmp = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return line => line.IndexOf(query, cmp) >= 0;
    }
}

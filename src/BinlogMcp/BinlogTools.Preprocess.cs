using System;
using System.ComponentModel;
using System.Text;
using Microsoft.Build.Logging.StructuredLogger;
using ModelContextProtocol.Server;

namespace BinlogMcp;

public static partial class BinlogTools
{
    [McpServerTool(Name = "preprocess_file", ReadOnly = true, Idempotent = true)]
    [Description(@"Returns the effective MSBuild XML for a project file (or any file imported during evaluation) with all <Import> elements recursively inlined — the same content the viewer's ""Preprocess"" command produces and equivalent to `msbuild /pp`.

Scope is a single ProjectEvaluation: imports are resolved using the chain captured during that evaluation. Find one with `search $projectevaluation Foo` and pass its id.

If 'file' is omitted, defaults to the evaluation's own project file. To preprocess an imported .props/.targets, pass its full path (or any unique suffix, like read_file accepts).

Line numbers in the output are 1-based and inclusive on both ends; defaults to lines 1..500 with a hard cap of 5000 lines per call. Page through larger files with successive calls (the preprocessed text is cached per-evaluation, so repeated paging is cheap).

Output:
file: <fullPath>
evaluation: <id> <projectFile>
totalLines: <n>
lines: <start>-<end>
  <n> | <text>
  ...")]
    public static string PreprocessFile(
        [Description("Absolute path to a .binlog file")] string path,
        [Description("Node id of a ProjectEvaluation (find one with `search $projectevaluation Foo`)")] string evaluationId,
        [Description("Optional embedded file path or unique suffix. Defaults to the evaluation's own project file.")] string file = null,
        [Description("First line to return (1-based, inclusive). Default 1.")] int? startLine = null,
        [Description("Last line to return (1-based, inclusive). Default startLine + 500. Capped to startLine + 5000.")] int? endLine = null) => Run(() =>
    {
        var entry = Cache.Load(path);
        var node = NodeId.Resolve(entry, evaluationId);
        if (node is not ProjectEvaluation evaluation)
        {
            throw new InvalidOperationException(
                $"Node [{evaluationId}] is a {node.GetType().Name}, but preprocess_file requires a ProjectEvaluation. Find one with `search $projectevaluation Foo`.");
        }

        // Resolve which file to preprocess. Default = the evaluation's project
        // file; otherwise look it up in the embedded archive (same suffix
        // matching as read_file) so callers don't need the full path.
        string sourceFilePath;
        if (string.IsNullOrEmpty(file))
        {
            sourceFilePath = evaluation.SourceFilePath;
            if (string.IsNullOrEmpty(sourceFilePath))
            {
                throw new InvalidOperationException(
                    $"ProjectEvaluation [{evaluationId}] has no SourceFilePath.");
            }
        }
        else
        {
            (sourceFilePath, _) = ResolveFile(path, file);
        }

        var manager = entry.PreprocessedFileManager;
        if (manager == null)
        {
            throw new InvalidOperationException("Binlog has no PreprocessedFileManager.");
        }

        string preprocessed = manager.GetPreprocessedText(
            sourceFilePath,
            PreprocessedFileManager.GetEvaluationKey(evaluation));

        if (string.IsNullOrEmpty(preprocessed))
        {
            throw new InvalidOperationException(
                $"No preprocessed text available for '{sourceFilePath}' in evaluation [{evaluationId}]. The file may not be embedded or may not be part of this evaluation's import chain.");
        }

        // SourceText computes line spans on demand; reuse it for windowing so
        // line numbering matches read_file exactly.
        var text = new SourceText(preprocessed);
        int total = text.Lines.Count;

        int start = Math.Max(startLine ?? 1, 1);
        int end = endLine ?? (start + DefaultReadWindow - 1);
        if (end < start)
        {
            end = start;
        }

        int maxEnd = start + MaxReadWindow - 1;
        if (end > maxEnd)
        {
            end = maxEnd;
        }

        var sb = new StringBuilder();
        sb.Append("file: ").AppendLine(sourceFilePath);
        sb.Append("evaluation: [").Append(evaluationId).Append("] ").AppendLine(evaluation.ProjectFile);
        sb.Append("totalLines: ").Append(total).AppendLine();

        if (total == 0)
        {
            sb.AppendLine("(empty)");
            return sb.ToString();
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

        sb.Append("lines: ").Append(start).Append('-').Append(end).AppendLine();

        int width = end.ToString().Length;
        for (int i = start; i <= end; i++)
        {
            string lineText = text.GetLineText(i - 1);
            sb.Append(i.ToString().PadLeft(width)).Append(" | ").AppendLine(lineText);
        }

        return sb.ToString();
    });
}

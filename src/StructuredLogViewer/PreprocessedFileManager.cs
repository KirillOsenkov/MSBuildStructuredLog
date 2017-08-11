using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Build.Logging.StructuredLogger;
using StructuredLogViewer.Controls;
using Bucket = System.Collections.Generic.HashSet<StructuredLogViewer.PreprocessedFileManager.ProjectImport>;

namespace StructuredLogViewer
{
    public class PreprocessedFileManager
    {
        private readonly Build build;
        private readonly SourceFileResolver sourceFileResolver;
        private readonly BuildControl buildControl;
        private readonly Dictionary<string, Bucket> importMap = new Dictionary<string, Bucket>(
            StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> preprocessedFileCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public PreprocessedFileManager(BuildControl buildControl, SourceFileResolver sourceFileResolver)
        {
            this.build = buildControl.Build;
            this.buildControl = buildControl;
            this.sourceFileResolver = sourceFileResolver;
            BuildImportMap();
        }

        private static readonly Regex importingProjectRegex = new Regex(
            @"Importing project ""([^""]+)"" into project ""([^""]+)"" at \((\d+),(\d+)\)\.", RegexOptions.Compiled);

        public struct ProjectImport : IEquatable<ProjectImport>
        {
            public ProjectImport(string importedProject, int line, int column)
            {
                ProjectPath = importedProject;
                Line = line;
                Column = column;
            }

            public string ProjectPath { get; set; }

            /// <summary>
            /// 0-based
            /// </summary>
            public int Line { get; set; }
            public int Column { get; set; }

            public bool Equals(ProjectImport other)
            {
                return ProjectPath == other.ProjectPath
                    && Line == other.Line
                    && Column == other.Column;
            }

            public override bool Equals(object obj)
            {
                if (obj is ProjectImport other)
                {
                    return Equals(other);
                }

                return false;
            }

            public override int GetHashCode()
            {
                return ProjectPath.GetHashCode() ^ Line.GetHashCode() ^ Column.GetHashCode();
            }

            public override string ToString()
            {
                return $"{ProjectPath} ({Line},{Column})";
            }
        }

        public void BuildImportMap()
        {
            var evaluation = build.FindChild<Folder>("Evaluation");
            if (evaluation == null)
            {
                return;
            }

            evaluation.VisitAllChildren<Message>(message =>
            {
                var match = importingProjectRegex.Match(message.Text);
                if (match.Success && match.Groups.Count == 5)
                {
                    var project = match.Groups[2].Value;
                    var importedProject = match.Groups[1].Value;
                    if (sourceFileResolver.HasFile(project) && sourceFileResolver.HasFile(importedProject))
                    {
                        var line = int.Parse(match.Groups[3].Value);
                        var column = int.Parse(match.Groups[4].Value);
                        if (line > 0)
                        {
                            line = line - 1; // should be 0-based 
                        }

                        AddImport(importMap, project, importedProject, line, column);
                    }
                }
            });
        }

        private int CorrectForMultilineImportElement(SourceText text, int lineNumber)
        {
            var line = text.Lines[lineNumber];
            var lineText = text.GetLineText(lineNumber);
            if (lineText.Contains("<Import"))
            {
                int lastElementLineNumber = lineNumber;
                while (!lineText.Contains("/>") && lastElementLineNumber < text.Lines.Length - 1)
                {
                    lastElementLineNumber++;
                    lineText = text.GetLineText(lastElementLineNumber);
                }

                return lastElementLineNumber;
            }

            return lineNumber;
        }

        private static void AddImport(Dictionary<string, Bucket> importMap, string project, string importedProject, int line, int column)
        {
            if (!importMap.TryGetValue(project, out var bucket))
            {
                bucket = new Bucket();
                importMap[project] = bucket;
            }

            bucket.Add(new ProjectImport(importedProject, line, column));
        }

        public Action GetPreprocessAction(string sourceFilePath)
        {
            var text = sourceFileResolver.GetSourceFileText(sourceFilePath);
            if (text == null)
            {
                return null;
            }

            return GetPreprocessAction(sourceFilePath, text);
        }

        public Action GetPreprocessAction(string sourceFilePath, SourceText text)
        {
            if (!CanPreprocess(sourceFilePath))
            {
                return null;
            }

            return () => ShowPreprocessed(sourceFilePath);
        }

        public string GetPreprocessedText(string sourceFilePath)
        {
            if (preprocessedFileCache.TryGetValue(sourceFilePath, out var result))
            {
                return result;
            }

            var sourceText = sourceFileResolver.GetSourceFileText(sourceFilePath);

            if (importMap.TryGetValue(sourceFilePath, out var imports) && imports.Count > 0)
            {
                var sb = new StringBuilder();
                int line = 0;

                if (sourceText.GetLineText(line).Contains("<?xml"))
                {
                    line++;
                }

                foreach (var import in imports.OrderBy(i => i.Line).ToArray())
                {
                    var importEndLine = CorrectForMultilineImportElement(sourceText, import.Line);

                    for (; line <= importEndLine; line++)
                    {
                        sb.AppendLine(sourceText.GetLineText(line));
                    }

                    var importText = GetPreprocessedText(import.ProjectPath);
                    sb.AppendLine($"<!-- ======== {import.ProjectPath} ======= -->");
                    sb.AppendLine(importText);
                    sb.AppendLine($"<!-- ======== END OF {import.ProjectPath} ======= -->");
                }

                for (; line < sourceText.Lines.Length; line++)
                {
                    sb.AppendLine(sourceText.GetLineText(line));
                }

                result = sb.ToString();
            }
            else
            {
                result = sourceText.Text;
                if (sourceText.GetLineText(0).Contains("<?xml"))
                {
                    result = result.Substring(sourceText.Lines[0].Length);
                }
            }

            preprocessedFileCache[sourceFilePath] = result;
            return result;
        }

        public void ShowPreprocessed(string sourceFilePath)
        {
            if (sourceFilePath == null)
            {
                return;
            }

            var preprocessedText = GetPreprocessedText(sourceFilePath);
            if (preprocessedText == null)
            {
                return;
            }

            var filePath = SettingsService.WriteContentToTempFileAndGetPath(preprocessedText, ".xml");
            buildControl.DisplayFile(filePath);
        }

        public bool CanPreprocess(string sourceFilePath)
        {
            return sourceFilePath != null
                && sourceFileResolver.HasFile(sourceFilePath)
                && importMap.TryGetValue(sourceFilePath, out var bucket)
                && bucket.Count > 0;
        }
    }
}

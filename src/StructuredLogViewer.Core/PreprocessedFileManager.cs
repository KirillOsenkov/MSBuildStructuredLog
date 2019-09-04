using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Build.Logging.StructuredLogger;
using Bucket = System.Collections.Generic.HashSet<StructuredLogViewer.ProjectImport>;

namespace StructuredLogViewer
{
    public class PreprocessedFileManager
    {
        private readonly Build build;
        private readonly SourceFileResolver sourceFileResolver;
        private readonly Dictionary<string, Bucket> importMap = new Dictionary<string, Bucket>(
            StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> preprocessedFileCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public PreprocessedFileManager(Build build, SourceFileResolver sourceFileResolver)
        {
            this.build = build;
            this.sourceFileResolver = sourceFileResolver;
            BuildImportMap();
        }

        public event Action<string> DisplayFile;

        public void BuildImportMap()
        {
            var evaluation = build.FindChild<Folder>("Evaluation");
            if (evaluation == null)
            {
                return;
            }

            evaluation.VisitAllChildren<Import>(VisitImport);
        }

        private void VisitImport(Import import)
        {
            if (sourceFileResolver.HasFile(import.ProjectFilePath) && sourceFileResolver.HasFile(import.ImportedProjectFilePath))
            {
                AddImport(importMap, import.ProjectFilePath, import.ImportedProjectFilePath, import.Line, import.Column);
            }
        }

        private int CorrectForMultilineImportElement(SourceText text, int lineNumber)
        {
            // can happen for corrupt binlogs where some files have no text
            // see https://github.com/KirillOsenkov/MSBuildStructuredLog/issues/258
            if (lineNumber < 0 || lineNumber >= text.Lines.Count)
            {
                return 0;
            }

            var line = text.Lines[lineNumber];
            var lineText = text.GetLineText(lineNumber);
            if (lineText.Contains("<Import"))
            {
                int lastElementLineNumber = lineNumber;
                while (!lineText.Contains("/>") && lastElementLineNumber < text.Lines.Count - 1)
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
            if (line > 0)
            {
                // convert to 0-based from 1-based
                line--;
            }

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

            if (importMap.TryGetValue(sourceFilePath, out var imports) &&
                imports.Count > 0 &&
                !string.IsNullOrWhiteSpace(sourceText.Text))
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

                for (; line < sourceText.Lines.Count; line++)
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
            DisplayFile?.Invoke(filePath);
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

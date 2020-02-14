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
        private readonly Dictionary<string, Dictionary<string, Bucket>> importMapsPerProject = new Dictionary<string, Dictionary<string, Bucket>>();
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
            var evaluation = build.FindChild<Folder>(Strings.Evaluation);
            if (evaluation == null)
            {
                return;
            }

            foreach (var projectEvaluation in evaluation.Children.OfType<Project>())
            {
                var imports = projectEvaluation.FindChild<Folder>(Strings.Imports);
                if (imports == null)
                {
                    continue;
                }

                // under which circumstances can this happen?
                // https://github.com/KirillOsenkov/MSBuildStructuredLog/issues/274
                if (projectEvaluation.ProjectFile == null)
                {
                    continue;
                }

                imports.VisitAllChildren<Import>(import => VisitImport(import, projectEvaluation));
            }
        }

        private void VisitImport(Import import, Project projectEvaluationContext)
        {
            if (sourceFileResolver.HasFile(import.ProjectFilePath) &&
                sourceFileResolver.HasFile(import.ImportedProjectFilePath) &&
                !string.IsNullOrEmpty(projectEvaluationContext.ProjectFile))
            {
                var importMap = GetOrCreateImportMap(projectEvaluationContext.ProjectFile);
                AddImport(importMap, import.ProjectFilePath, import.ImportedProjectFilePath, import.Line, import.Column);
            }
        }

        private Dictionary<string, Bucket> GetOrCreateImportMap(string projectFilePath)
        {
            if (!importMapsPerProject.TryGetValue(projectFilePath, out var importMap))
            {
                importMap = new Dictionary<string, Bucket>(StringComparer.OrdinalIgnoreCase);
                importMapsPerProject[projectFilePath] = importMap;
            }

            return importMap;
        }

        private Dictionary<string, Bucket> GetImportMap(string projectFilePath)
        {
            if (projectFilePath != null && importMapsPerProject.TryGetValue(projectFilePath, out var importMap))
            {
                return importMap;
            }

            return null;
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

        public Action GetPreprocessAction(string sourceFilePath, string preprocessContext)
        {
            if (!CanPreprocess(sourceFilePath, preprocessContext))
            {
                return null;
            }

            return () => ShowPreprocessed(sourceFilePath, preprocessContext);
        }

        public string GetPreprocessedText(string sourceFilePath, string projectEvaluationContext)
        {
            string preprocessedFileCacheKey = projectEvaluationContext + sourceFilePath;
            if (preprocessedFileCache.TryGetValue(preprocessedFileCacheKey, out var result))
            {
                return result;
            }

            var sourceText = sourceFileResolver.GetSourceFileText(sourceFilePath);

            var importMap = GetImportMap(projectEvaluationContext);
            if (importMap == null)
            {
                return string.Empty;
            }

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

                    var importText = GetPreprocessedText(import.ProjectPath, projectEvaluationContext);
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

            preprocessedFileCache[preprocessedFileCacheKey] = result;
            return result;
        }

        public string GetProjectEvaluationContext(object node)
        {
            if (node is TreeNode treeNode)
            {
                var project = treeNode.GetNearestParentOrSelf<Project>();
                if (project != null)
                {
                    return project.ProjectFile;
                }
            }

            return null;
        }

        public void ShowPreprocessed(IPreprocessable preprocessable)
        {
            if (preprocessable == null)
            {
                return;
            }

            ShowPreprocessed(preprocessable.RootFilePath, GetProjectEvaluationContext(preprocessable));
        }

        public void ShowPreprocessed(string sourceFilePath, string projectContext)
        {
            if (sourceFilePath == null || projectContext == null)
            {
                return;
            }

            var preprocessedText = GetPreprocessedText(sourceFilePath, projectContext);
            if (preprocessedText == null)
            {
                return;
            }

            var filePath = SettingsService.WriteContentToTempFileAndGetPath(preprocessedText, ".xml");
            DisplayFile?.Invoke(filePath);
        }

        public bool CanPreprocess(IPreprocessable preprocessable)
        {
            string sourceFilePath = preprocessable.RootFilePath;
            string projectContext = GetProjectEvaluationContext(preprocessable);
            return CanPreprocess(sourceFilePath, projectContext);
        }

        public bool CanPreprocess(string sourceFilePath, string projectContext)
        {
            return sourceFilePath != null
                && sourceFileResolver.HasFile(sourceFilePath)
                && GetImportMap(projectContext) is Dictionary<string, Bucket> importMap
                && importMap.TryGetValue(sourceFilePath, out var bucket)
                && bucket.Count > 0;
        }
    }
}

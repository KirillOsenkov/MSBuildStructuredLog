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
            var evaluation = build.FindChild<NamedNode>(Strings.Evaluation);
            if (evaluation == null)
            {
                return;
            }

            foreach (var projectEvaluation in evaluation.Children.OfType<ProjectEvaluation>())
            {
                var imports = projectEvaluation.FindChild<NamedNode>(Strings.Imports);
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

        private void VisitImport(Import import, ProjectEvaluation projectEvaluationContext)
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

        private int CorrectForMultilineTag(SourceText text, int lineNumber, string startText = "<Import", string endText = "/>")
        {
            // can happen for corrupt binlogs where some files have no text
            // see https://github.com/KirillOsenkov/MSBuildStructuredLog/issues/258
            if (lineNumber < 0 || lineNumber >= text.Lines.Count)
            {
                return 0;
            }

            var lineText = text.GetLineText(lineNumber);
            if (lineText.Contains(startText))
            {
                while (!lineText.Contains(endText) && lineNumber < text.Lines.Count - 1)
                {
                    lineNumber++;
                    lineText = text.GetLineText(lineNumber);
                }
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
                result = GetPreprocessedTextCore(projectEvaluationContext, sourceText, imports);
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

        private string GetPreprocessedTextCore(string projectEvaluationContext, SourceText sourceText, Bucket imports)
        {
            string result;
            var sb = new StringBuilder();
            int line = 0;

            if (sourceText.GetLineText(line).Contains("<?xml"))
            {
                line++;
            }

            var importsList = imports.OrderBy(i => i.Line).ToList();

            var sdkProps = importsList.FirstOrDefault(i => i.ProjectPath.EndsWith("Sdk.props", StringComparison.OrdinalIgnoreCase) && i.Line == 0 && i.Column == 0);
            if (sdkProps != default)
            {
                while (sourceText.GetLineText(line) is string firstLine && !firstLine.Contains("<Project"))
                {
                    sb.AppendLine(firstLine);
                    line++;
                }

                line = SkipTag(sourceText, sb, line, line, "<Project", ">");

                InjectImportedProject(projectEvaluationContext, sb, sdkProps);
                importsList.Remove(sdkProps);
            }

            var sdkTargets = importsList.FirstOrDefault(i => i.ProjectPath.EndsWith("Sdk.targets", StringComparison.OrdinalIgnoreCase) && i.Line == 0 && i.Column == 0);
            if (sdkTargets != default)
            {
                importsList.Remove(sdkTargets);
            }

            foreach (var import in importsList)
            {
                line = SkipTag(sourceText, sb, line, import.Line);

                InjectImportedProject(projectEvaluationContext, sb, import);
            }

            for (; line < sourceText.Lines.Count; line++)
            {
                var lastLineText = sourceText.GetLineText(line);
                if (lastLineText.Contains("</Project>"))
                {
                    if (sdkTargets != default)
                    {
                        InjectImportedProject(projectEvaluationContext, sb, sdkTargets);
                        sdkTargets = default;
                    }
                }

                if (lastLineText.Length > 0)
                {
                    sb.AppendLine(lastLineText);
                }
            }

            result = sb.ToString();
            
            return result;
        }

        private void InjectImportedProject(string projectEvaluationContext, StringBuilder sb, ProjectImport import)
        {
            string projectPath = import.ProjectPath;
            var importText = GetPreprocessedText(projectPath, projectEvaluationContext);
            sb.AppendLine($"<!-- ======== {projectPath} ======= -->");
            sb.Append(importText);
            sb.AppendLine($"<!-- ======== END OF {projectPath} ======= -->");
        }

        private int SkipTag(SourceText sourceText, StringBuilder sb, int line, int lineNumber, string startText = "<Import", string endText = "/>")
        {
            var elementEndLine = CorrectForMultilineTag(sourceText, lineNumber, startText, endText);
            for (; line <= elementEndLine; line++)
            {
                sb.AppendLine(sourceText.GetLineText(line));
            }

            return line;
        }

        public string GetProjectEvaluationContext(object node)
        {
            if (node is TreeNode treeNode)
            {
                var project = (IPreprocessable)treeNode.GetNearestParentOrSelf<Project>() ?? treeNode.GetNearestParentOrSelf<ProjectEvaluation>();
                if (project != null)
                {
                    return project.RootFilePath;
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

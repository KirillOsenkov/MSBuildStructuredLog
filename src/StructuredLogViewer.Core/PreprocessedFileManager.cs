﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging.StructuredLogger;
using Bucket = System.Collections.Generic.List<StructuredLogViewer.ProjectImport>;

namespace StructuredLogViewer
{
    public class PreprocessedFileManager
    {
        private readonly Build build;
        private readonly SourceFileResolver sourceFileResolver;
        private readonly Dictionary<string, Dictionary<string, Bucket>> importMapsPerEvaluation = new Dictionary<string, Dictionary<string, Bucket>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> preprocessedFileCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, PreprocessContext> preprocessContexts = new Dictionary<string, PreprocessContext>(StringComparer.OrdinalIgnoreCase);

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

            var allEvaluations = evaluation.Children.OfType<ProjectEvaluation>().ToArray();
            foreach (var projectEvaluation in allEvaluations)
            {
                var imports = projectEvaluation.FindChild<NamedNode>(Strings.Imports);
                if (imports == null)
                {
                    continue;
                }

                // under which circumstances can this happen?
                // https://github.com/KirillOsenkov/MSBuildStructuredLog/issues/274
                if (string.IsNullOrEmpty(projectEvaluation.ProjectFile))
                {
                    continue;
                }

                string evaluationKey = GetEvaluationKey(projectEvaluation);
                var importMap = GetOrCreateImportMap(evaluationKey, projectEvaluation.ProjectFile);
                build.RunInBackground(() =>
                {
                    imports.VisitAllChildren<Import>(import => VisitImport(import, importMap));
                });
            }
        }

        private void VisitImport(Import import, Dictionary<string, Bucket> importMap)
        {
            AddImport(importMap, import);
        }

        public static string GetEvaluationKey(ProjectEvaluation evaluation) => evaluation == null ? null : evaluation.ProjectFile + evaluation.Id.ToString();

        public static string GetEvaluationKey(Project project)
        {
            if (project == null)
            {
                return null;
            }

            if (project.EvaluationId == BuildEventContext.InvalidEvaluationId)
            {
                return project.ProjectFile;
            }

            return project.ProjectFile + project.EvaluationId.ToString();
        }

        private Dictionary<string, Bucket> GetOrCreateImportMap(string key, string projectFilePath)
        {
            lock (importMapsPerEvaluation)
            {
                if (!importMapsPerEvaluation.TryGetValue(key, out var importMap))
                {
                    importMap = new Dictionary<string, Bucket>(StringComparer.OrdinalIgnoreCase);
                    importMapsPerEvaluation[key] = importMap;

                    // we want to have a "default" import map for each project, without specifying an evaluation id
                    // this is when we click on a project and want to preprocess (we currently don't know which
                    // evaluation id is associated with this project).
                    // TODO: improve this when https://github.com/dotnet/msbuild/issues/4926 is fixed.
                    importMapsPerEvaluation[projectFilePath] = importMap;
                }

                return importMap;
            }
        }

        private Dictionary<string, Bucket> GetImportMap(string projectEvaluationKey)
        {
            if (projectEvaluationKey != null && importMapsPerEvaluation.TryGetValue(projectEvaluationKey, out var importMap))
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

        private static void AddImport(Dictionary<string, Bucket> importMap, Import import)
        {
            string project = import.ProjectFilePath;
            string importedProject = import.ImportedProjectFilePath;
            int line = import.Line;
            int column = import.Column;

            if (line > 0)
            {
                // convert to 0-based from 1-based
                line--;
            }

            Bucket bucket;
            lock (importMap)
            {
                if (!importMap.TryGetValue(project, out bucket))
                {
                    bucket = new Bucket();
                    importMap[project] = bucket;
                }
            }

            lock (bucket)
            {
                bucket.Add(new ProjectImport(importedProject, line, column, import));
            }
        }

        public Action GetPreprocessAction(string sourceFilePath, string preprocessEvaluationContext)
        {
            if (!CanPreprocess(sourceFilePath, preprocessEvaluationContext))
            {
                return null;
            }

            return () => ShowPreprocessed(null, sourceFilePath, preprocessEvaluationContext);
        }

        public class PreprocessContext
        {
            public int Position;

            private readonly List<(Span span, ProjectImport import)> spans = new();

            public void AddProjectSpan(Span span, ProjectImport import)
            {
                spans.Add((span, import));
            }

            public ProjectEvaluation Evaluation
            {
                get
                {
                    if (spans.Any())
                    {
                        return spans[0].import.Import.GetNearestParent<ProjectEvaluation>();
                    }

                    return null;
                }
            }

            public Import GetImportFromPosition(int position)
            {
                Span bestSpan = default;
                ProjectImport bestImport = default;

                foreach (var span in spans)
                {
                    if (!span.span.Contains(position))
                    {
                        continue;
                    }

                    if (bestSpan.End == 0 || bestSpan.Length > span.span.Length)
                    {
                        bestSpan = span.span;
                        bestImport = span.import;
                    }
                }

                return bestImport.Import;
            }

            public int FindFileOffset(string sourceFilePath)
            {
                foreach (var span in spans)
                {
                    if (string.Equals(span.import.ProjectPath, sourceFilePath, StringComparison.OrdinalIgnoreCase))
                    {
                        return span.span.Start;
                    }
                }

                return 0;
            }
        }

        public PreprocessContext TryGetContext(string filePath)
        {
            preprocessContexts.TryGetValue(filePath, out var result);
            return result;
        }

        public string GetPreprocessedText(
            string sourceFilePath,
            string projectEvaluationContext)
        {
            return GetPreprocessedText(sourceFilePath, projectEvaluationContext, context: null);
        }

        private string GetPreprocessedText(
            string sourceFilePath,
            string projectEvaluationContext,
            PreprocessContext context)
        {
            string preprocessedFileCacheKey = projectEvaluationContext + sourceFilePath;
            if (preprocessedFileCache.TryGetValue(preprocessedFileCacheKey, out var result))
            {
                return result;
            }

            var sourceText = sourceFileResolver.GetSourceFileText(sourceFilePath);
            if (sourceText == null)
            {
                return string.Empty;
            }

            var importMap = GetImportMap(projectEvaluationContext);
            if (importMap == null)
            {
                return string.Empty;
            }

            if (importMap.TryGetValue(sourceFilePath, out var imports) &&
                imports.Count > 0 &&
                !string.IsNullOrWhiteSpace(sourceText.Text))
            {
                bool createdContext = false;
                if (context == null)
                {
                    context = new PreprocessContext();
                    createdContext = true;
                }

                result = GetPreprocessedTextCore(projectEvaluationContext, sourceText, imports, context);

                if (createdContext)
                {
                    var preprocessedFilePath = SettingsService.GetPreprocessedFilePath(result);
                    preprocessContexts[preprocessedFilePath] = context;
                }
            }
            else
            {
                result = sourceText.Text;
                if (sourceText.GetLineText(0).Contains("<?xml"))
                {
                    result = result.Substring(sourceText.Lines[0].Length);
                }

                context.Position += result.Length;
            }

            preprocessedFileCache[preprocessedFileCacheKey] = result;
            return result;
        }

        private string GetPreprocessedTextCore(
            string projectEvaluationContext,
            SourceText sourceText,
            Bucket imports,
            PreprocessContext context)
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
                    AppendLine(firstLine, sb, context);
                    line++;
                }

                line = SkipTag(sourceText, sb, line, line, "<Project", ">", context);

                InjectImportedProject(projectEvaluationContext, sb, sdkProps, context);
                importsList.Remove(sdkProps);
            }

            var sdkTargets = importsList.FirstOrDefault(i => i.ProjectPath.EndsWith("Sdk.targets", StringComparison.OrdinalIgnoreCase) && i.Line == 0 && i.Column == 0);
            if (sdkTargets != default)
            {
                importsList.Remove(sdkTargets);
            }

            foreach (var import in importsList)
            {
                line = SkipTag(sourceText, sb, line, import.Line, context: context);

                InjectImportedProject(projectEvaluationContext, sb, import, context);
            }

            int count = sourceText.Lines.Count;
            for (; line < count; line++)
            {
                var lastLineText = sourceText.GetLineText(line);
                if (lastLineText.Contains("</Project>"))
                {
                    if (sdkTargets != default)
                    {
                        InjectImportedProject(projectEvaluationContext, sb, sdkTargets, context);
                        sdkTargets = default;
                    }
                }

                if (line < count - 1 || lastLineText.Length > 0)
                {
                    AppendLine(lastLineText, sb, context);
                }
            }

            result = sb.ToString();

            return result;
        }

        private void AppendLine(string text, StringBuilder sb, PreprocessContext context)
        {
            int before = sb.Length;
            sb.AppendLine(text);
            int after = sb.Length;
            context.Position += after - before;
        }

        private void InjectImportedProject(
            string projectEvaluationContext,
            StringBuilder sb,
            ProjectImport import,
            PreprocessContext context)
        {
            string projectPath = import.ProjectPath;

            int start = context.Position;
            int sbStart = sb.Length;
            AppendLine($"<!-- ======== {projectPath} ======= -->", sb, context);

            var importText = GetPreprocessedText(projectPath, projectEvaluationContext, context);

            // This is the only place where we don't call Append(string, sb, context)
            // to avoid incrementing context.Position twice, because it has been incremented by the recursive calls already
            sb.Append(importText);

            if (!importText.EndsWith("\n"))
            {
                AppendLine("", sb, context);
            }

            AppendLine($"<!-- ======== END OF {projectPath} ======= -->", sb, context);

            int end = context.Position;
            int sbEnd = sb.Length;
            if (end - start != sbEnd - sbStart)
            {
            }

            context.AddProjectSpan(new Span(start, end - start), import);
        }

        private int SkipTag(
            SourceText sourceText,
            StringBuilder sb,
            int line,
            int lineNumber,
            string startText = "<Import",
            string endText = "/>",
            PreprocessContext context = null)
        {
            var elementEndLine = CorrectForMultilineTag(sourceText, lineNumber, startText, endText);
            for (; line <= elementEndLine; line++)
            {
                AppendLine(sourceText.GetLineText(line), sb, context);
            }

            return line;
        }

        public static string GetNodeEvaluationKey(object node)
        {
            if (node is TreeNode treeNode)
            {
                var project = treeNode.GetNearestParentOrSelf<Project>();
                if (project != null)
                {
                    return GetEvaluationKey(project);
                }

                var evaluation = treeNode.GetNearestParentOrSelf<ProjectEvaluation>();
                if (evaluation != null)
                {
                    return GetEvaluationKey(evaluation);
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

            ShowPreprocessed(preprocessable, preprocessable.RootFilePath, GetNodeEvaluationKey(preprocessable));
        }

        public class TargetNode
        {
            public string Name;
            public List<TargetNode> BeforeTargets = new ();
            public List<TargetNode> AfterTargets = new();
            public List<TargetNode> DependsOnTargets = new();
        }

        public void ShowPreprocessed(IPreprocessable preprocessable, string sourceFilePath, string projectContext)
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

            var props = new Dictionary<string, string>();

            var targetNodes = new Dictionary<string, TargetNode>(StringComparer.OrdinalIgnoreCase);

            if (preprocessable is ProjectEvaluation eval)
            {
                Folder properties = eval.FindChild<Folder>(Strings.Properties);
                if (properties != null)
                {
                    foreach (var property in properties.Children.OfType<Property>())
                    {
                        props[property.Name] = property.Value;
                    }
                }

                var xdoc = XDocument.Parse(preprocessedText);
                var targets = xdoc
                    .DescendantNodes()
                    .OfType<XElement>()
                    .Where(element => element.Name.LocalName == "Target")
                    .ToArray();

                foreach (var target in targets)
                {
                    var name = GetAttribute(target, "Name");
                    var node = new TargetNode()
                    {
                        Name = name
                    };

                    targetNodes[name] = node;
                }

                foreach (var target in targets)
                {
                    var name = GetAttribute(target, "Name");
                    var beforeTargetsText = GetAttribute(target, "BeforeTargets");
                    var afterTargetsText = GetAttribute(target, "AfterTargets");
                    var dependsOnTargetsText = GetAttribute(target, "DependsOnTargets");

                    var beforeTargets = Expand(beforeTargetsText, props);
                    var afterTargets = Expand(afterTargetsText, props);
                    var dependsOnTargets = Expand(dependsOnTargetsText, props);

                    var node = GetTarget(name);

                    foreach (var before in beforeTargets)
                    {
                        var dest = GetTarget(before);
                        dest.BeforeTargets.Insert(0, node);
                    }

                    foreach (var after in afterTargets)
                    {
                        var dest = GetTarget(after);
                        dest.AfterTargets.Insert(0, node);
                    }

                    foreach (var dep in dependsOnTargets)
                    {
                        var dest = GetTarget(dep);
                        node.DependsOnTargets.Add(dest);
                    }
                }

                var sb = new StringBuilder();
                sb.AppendLine("digraph d {");

                foreach (var targetNode in targetNodes)
                {
                    var node = targetNode.Value;
                    var name = node.Name;
                    sb.AppendLine($"\"{name}\"");

                    foreach (var before in node.BeforeTargets)
                    {
                        sb.AppendLine($"\"{name}\" -> \"{before.Name}\" [label = before]");
                    }

                    foreach (var after in node.AfterTargets)
                    {
                        sb.AppendLine($"\"{name}\" -> \"{after.Name}\" [label = after]");
                    }

                    foreach (var depends in node.DependsOnTargets)
                    {
                        sb.AppendLine($"\"{name}\" -> \"{depends.Name}\" [label = depends]");
                    }
                }

                sb.AppendLine("}");

                preprocessedText = sb.ToString() + "\r\n\r\n\r\n" + preprocessedText;
            }

            TargetNode GetTarget(string text)
            {
                if (!targetNodes.TryGetValue(text, out var node))
                {
                    node = new TargetNode { Name = text };
                    targetNodes.Add(text, node);
                }

                return node;
            }

            var filePath = SettingsService.WriteContentToTempFileAndGetPath(preprocessedText, ".xml");
            DisplayFile?.Invoke(filePath);
        }


        private string[] Expand(string expression, Dictionary<string, string> props)
        {
            if (expression == null)
            {
                return Array.Empty<string>();
            }

            if (expression.Contains("$("))
            {
                foreach (var kvp in props)
                {
                    expression = expression.Replace("$(" + kvp.Key + ")", kvp.Value);
                }
            }

            return expression.Split([';'], StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray();
        }

        private static string GetAttribute(XElement element, string attributeName)
        {
            return element.Attribute(XName.Get(attributeName))?.Value;
        }

        public bool CanPreprocess(IPreprocessable preprocessable)
        {
            string sourceFilePath = preprocessable.RootFilePath;
            string projectContext = GetNodeEvaluationKey(preprocessable);
            return CanPreprocess(sourceFilePath, projectContext);
        }

        public bool CanPreprocess(string sourceFilePath, string projectEvaluationKey)
        {
            return sourceFilePath != null
                && sourceFileResolver.HasFile(sourceFilePath)
                && GetImportMap(projectEvaluationKey) is Dictionary<string, Bucket> importMap
                && importMap.TryGetValue(sourceFilePath, out var bucket)
                && bucket.Count > 0;
        }
    }
}

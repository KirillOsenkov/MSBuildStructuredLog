using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Language.Xml;
using StructuredLogViewer;

namespace Microsoft.Build.Logging.StructuredLogger;

public class PropertyGraph
{
    public PreprocessedFileManager PreprocessedFileManager { get; }

    public PropertyGraph(PreprocessedFileManager preprocessedFileManager, PropertiesAndItemsSearch search)
    {
        search.AugmentResults += Search_AugmentResults;
        PreprocessedFileManager = preprocessedFileManager;
    }

    private IEnumerable<SearchResult> Search_AugmentResults(ProjectEvaluation evaluation, IEnumerable<SearchResult> results)
    {
        if (results.All(r => r.Node is Property) && results.Count() < 10)
        {
            var names = results.Select(r => ((Property)r.Node).Name).ToArray();

            var graphNode = GetPropertyGraph(evaluation, names);
            if (graphNode != null)
            {
                results = results.Append(graphNode).ToArray();
            }
        }

        return results;
    }

    public SearchResult GetPropertyGraph(ProjectEvaluation evaluation, IEnumerable<string> properties)
    {
        var importsFolder = evaluation.ImportsFolder;

        if (importsFolder == null)
        {
            return null;
        }

        var resultFolder = new Folder { Name = "Property Graph", IsExpanded = true };

        var text = PreprocessedFileManager.SourceFileResolver.GetSourceFileText(evaluation.ProjectFile);
        var propertyNames = new HashSet<string>(properties, StringComparer.OrdinalIgnoreCase);

        if (Visit(evaluation.ProjectFile, resultFolder, text, importsFolder.Children.OfType<Import>(), propertyNames))
        {
            return new SearchResult(resultFolder);
        }

        return null;
    }

    private bool Visit(
        string filePath,
        TreeNode resultParent,
        SourceText text,
        IEnumerable<Import> imports,
        HashSet<string> propertyNames)
    {
        bool foundResults = false;

        var root = text.RootElement;

        var importsBefore = imports.Where(i => i.Line == 0 && Import.IsImportBefore(i.ImportedProjectFilePath)).ToArray();
        var importsAfter = imports.Where(i => i.Line == 0 && Import.IsImportAfter(i.ImportedProjectFilePath)).ToArray();

        foreach (var importBefore in importsBefore)
        {
            var resultImport = new Import(
                importBefore.ProjectFilePath,
                importBefore.ImportedProjectFilePath,
                importBefore.Line,
                importBefore.Column)
            {
                IsExpanded = true,
                Text = Path.GetFileName(importBefore.ImportedProjectFilePath)
            };
            var importText = PreprocessedFileManager.SourceFileResolver.GetSourceFileText(importBefore.ImportedProjectFilePath);
            bool nestedFound = Visit(importBefore.ImportedProjectFilePath, resultImport, importText, importBefore.Children.OfType<Import>(), propertyNames);
            if (nestedFound)
            {
                resultParent.AddChild(resultImport);
                foundResults = true;
            }
        }

        var importPositions = new List<int>();
        var importArray = imports.ToArray();

        foreach (var import in imports)
        {
            var importPosition = text.GetPosition(import.Line, import.Column);
            importPositions.Add(importPosition);
        }

        VisitElement(root);

        foreach (var importAfter in importsAfter)
        {
            var resultImport = new Import(
                importAfter.ProjectFilePath,
                importAfter.ImportedProjectFilePath,
                importAfter.Line,
                importAfter.Column)
            {
                IsExpanded = true,
                Text = Path.GetFileName(importAfter.ImportedProjectFilePath)
            };
            var importText = PreprocessedFileManager.SourceFileResolver.GetSourceFileText(importAfter.ImportedProjectFilePath);
            bool nestedFound = Visit(
                importAfter.ImportedProjectFilePath,
                resultImport,
                importText,
                importAfter.Children.OfType<Import>(),
                propertyNames);
            if (nestedFound)
            {
                resultParent.AddChild(resultImport);
                foundResults = true;
            }
        }

        void VisitElement(IXmlElement parentElement)
        {
            foreach (var element in parentElement.Elements)
            {
                var name = element.Name;

                if (name == "PropertyGroup")
                {
                    var condition = GetParsedCondition(element, text, filePath);
                    if (condition != null)
                    {
                        if (condition.PropertyNames.Overlaps(propertyNames))
                        {
                            var sourceTextLineResult = GetOrAddLine(
                                resultParent,
                                filePath,
                                condition.Line,
                                condition.Text);
                            foundResults = true;
                        }
                    }

                    foreach (var propertyElement in element.Elements)
                    {
                        string propertyName = propertyElement.Name;
                        var propertyNode = (SyntaxNode)propertyElement;
                        var propertySpan = propertyNode.Span;
                        var propertyStart = propertySpan.Start;
                        var startLine = text.GetLineNumberFromPosition(propertyStart) + 1;
                        List<PropertyUsage> usages = new();

                        var propertyCondition = GetParsedCondition(propertyElement, text, filePath);
                        AddUsages(propertyCondition, usages);

                        if (propertyNames.Contains(propertyName))
                        {
                            var usage = new PropertyUsage
                            {
                                Name = propertyName,
                                Position = propertyElement.AsSyntaxElement.NameNode.SpanStart,
                                IsWrite = true
                            };
                            usages.Add(usage);
                        }

                        var value = propertyElement.Value;
                        var parsedValue = GetParsedValue(propertyElement, text, filePath);
                        AddUsages(parsedValue, usages);

                        if (usages.Count > 0)
                        {
                            foundResults = true;
                            var sourceTextLineResult = GetOrAddLine(
                                resultParent,
                                filePath,
                                startLine,
                                text.GetText(propertySpan));
                            foreach (var usage in usages)
                            {
                                usage.Position = usage.Position - propertyStart;
                                sourceTextLineResult.AddUsage(usage);
                            }
                        }
                    }
                }
                else if (name == "Choose")
                {
                    VisitElement(element);
                }
                else if (name == "When")
                {
                    var condition = GetParsedCondition(element, text, filePath);
                    if (condition != null)
                    {
                        if (condition.PropertyNames.Overlaps(propertyNames))
                        {
                            var sourceTextLineResult = GetOrAddLine(
                                resultParent,
                                filePath,
                                condition.Line,
                                condition.Text);
                            foundResults = true;
                        }
                    }

                    VisitElement(element);
                }
                else if (name == "Otherwise")
                {
                    VisitElement(element);
                }
                else if (name == "ImportGroup")
                {
                    var condition = GetParsedCondition(element, text, filePath);
                    if (condition != null)
                    {
                        if (condition.PropertyNames.Overlaps(propertyNames))
                        {
                            var sourceTextLineResult = GetOrAddLine(
                                resultParent,
                                filePath,
                                condition.Line,
                                condition.Text);
                            foundResults = true;
                        }
                    }

                    VisitElement(element);
                }
                else if (name == "Import")
                {
                    var condition = GetParsedCondition(element, text, filePath);
                    if (condition != null)
                    {
                        if (condition.PropertyNames.Overlaps(propertyNames))
                        {
                            var sourceTextLineResult = GetOrAddLine(
                                resultParent,
                                filePath,
                                condition.Line,
                                condition.Text);
                            foundResults = true;
                        }
                    }

                    for (int i = 0; i < importPositions.Count; i++)
                    {
                        var importNode = importArray[i];
                        var position = importPositions[i];
                        if (((SyntaxNode)element).FullSpan.Contains(position))
                        {
                            var resultImport = new Import(
                                importNode.ProjectFilePath,
                                importNode.ImportedProjectFilePath,
                                importNode.Line,
                                importNode.Column)
                            {
                                IsExpanded = true,
                                Text = Path.GetFileName(importNode.ImportedProjectFilePath)
                            };
                            var importText = PreprocessedFileManager.SourceFileResolver.GetSourceFileText(importNode.ImportedProjectFilePath);
                            var nestedFound = Visit(
                                importNode.ImportedProjectFilePath,
                                resultImport,
                                importText,
                                importNode.Children.OfType<Import>(),
                                propertyNames);
                            if (nestedFound)
                            {
                                resultParent.AddChild(resultImport);
                                foundResults = true;
                            }

                            break;
                        }
                    }
                }
            }
        }

        void AddUsages(ParsedExpression expression, List<PropertyUsage> usages)
        {
            if (expression == null)
            {
                return;
            }

            int conditionStart = expression.Position;
            foreach (var occurrence in expression.PropertyReads)
            {
                var occurrenceStart = conditionStart + occurrence.Start;
                var occurrenceText = expression.Text.Substring(occurrence.Start, occurrence.Length);
                if (!propertyNames.Contains(occurrenceText))
                {
                    continue;
                }

                var usage = new PropertyUsage
                {
                    Name = occurrenceText,
                    Position = occurrenceStart
                };
                usages.Add(usage);
            }
        }

        return foundResults;
    }

    private SourceFileLineWithHighlights GetOrAddLine(
        TreeNode parent,
        string filePath,
        int lineNumber,
        string lineText)
    {
        foreach (var child in parent.Children.OfType<SourceFileLineWithHighlights>())
        {
            if (string.Equals(child.SourceFilePath, filePath, StringComparison.OrdinalIgnoreCase) &&
                child.LineNumber == lineNumber &&
                child.LineText == lineText)
            {
                return child;
            }
        }

        var sourceTextLineResult = new SourceFileLineWithHighlights
        {
            SourceFilePath = filePath,
            LineText = lineText,
            LineNumber = lineNumber
        };
        parent.AddChild(sourceTextLineResult);

        return sourceTextLineResult;
    }

    private static ParsedExpression GetParsedValue(IXmlElement element, SourceText text, string filePath)
    {
        var syntax = element.AsSyntaxElement;
        if (string.IsNullOrWhiteSpace(element.Value))
        {
            return null;
        }

        if (syntax.Content.Count == 1 && syntax.Content[0] is XmlTextSyntax textSyntax)
        {
            var parsed = ParsedExpression.Parse(textSyntax.Value);
            parsed.FilePath = filePath;
            var lineAndColumn = text.GetLineAndColumn1Based(textSyntax.SpanStart);
            parsed.Line = lineAndColumn.Line;
            parsed.Column = lineAndColumn.Column;
            parsed.Position = textSyntax.Span.Start;
            return parsed;
        }

        return null;
    }

    private static ParsedExpression GetParsedCondition(IXmlElement element, SourceText text, string filePath)
    {
        var conditionAttribute = GetConditionAttribute(element);
        if (conditionAttribute != null)
        {
            var parsed = ParsedExpression.Parse(conditionAttribute.Value);
            if (parsed != null)
            {
                parsed.FilePath = filePath;
                var lineAndColumn = text.GetLineAndColumn1Based(conditionAttribute.SpanStart);
                parsed.Line = lineAndColumn.Line;
                parsed.Column = lineAndColumn.Column;
                parsed.Position = conditionAttribute.ValueNode.TextTokens.Span.Start;
                return parsed;
            }
        }

        return null;
    }

    private static XmlAttributeSyntax GetConditionAttribute(IXmlElement element)
    {
        if (element == null || element.Attributes == null)
        {
            return null;
        }

        var syntax = element.AsSyntaxElement;
        foreach (var attribute in syntax.Attributes)
        {
            if (attribute.Name == "Condition")
            {
                return attribute;
            }
        }

        return null;
    }
}
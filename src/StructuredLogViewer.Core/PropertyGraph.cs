using System;
using System.Collections.Generic;
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
        if (results.All(r => r.Node is Property))
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
                IsExpanded = true
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
                IsExpanded = true
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
                            var sourceTextLineResult = new SourceFileLine
                            {
                                SourceFilePath = filePath,
                                LineText = condition.Text,
                                LineNumber = condition.Line
                            };
                            resultParent.AddChild(sourceTextLineResult);
                            foundResults = true;
                        }
                    }

                    foreach (var propertyElement in element.Elements)
                    {
                        string propertyName = propertyElement.Name;
                        var startLine = text.GetLineNumberFromPosition(((SyntaxNode)propertyElement).SpanStart) + 1;
                        if (propertyName == "IntermediateOutputPath")
                        {
                        }

                        var propertyCondition = GetParsedCondition(propertyElement, text, filePath);
                        if (propertyCondition != null)
                        {
                            if (propertyCondition.PropertyNames.Overlaps(propertyNames))
                            {
                                var sourceTextLineResult = new SourceFileLine
                                {
                                    SourceFilePath = filePath,
                                    LineText = propertyCondition.Text,
                                    LineNumber = propertyCondition.Line
                                };
                                resultParent.AddChild(sourceTextLineResult);
                                foundResults = true;
                            }
                        }

                        if (propertyNames.Contains(propertyName))
                        {
                            var sourceTextLineResult = new SourceFileLine
                            {
                                SourceFilePath = filePath,
                                LineText = text.GetText(((SyntaxNode)propertyElement).Span),
                                LineNumber = startLine
                            };
                            resultParent.AddChild(sourceTextLineResult);
                            foundResults = true;
                        }

                        var value = propertyElement.Value;
                        var parsedValue = GetParsedValue(propertyElement, text, filePath);
                        if (parsedValue != null)
                        {
                            if (parsedValue.PropertyNames.Overlaps(propertyNames))
                            {
                                var sourceTextLineResult = new SourceFileLine
                                {
                                    SourceFilePath = filePath,
                                    LineText = parsedValue.Text,
                                    LineNumber = parsedValue.Line
                                };
                                resultParent.AddChild(sourceTextLineResult);
                                foundResults = true;
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
                            var sourceTextLineResult = new SourceFileLine
                            {
                                SourceFilePath = filePath,
                                LineText = condition.Text,
                                LineNumber = condition.Line
                            };
                            resultParent.AddChild(sourceTextLineResult);
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
                            var sourceTextLineResult = new SourceFileLine
                            {
                                SourceFilePath = filePath,
                                LineText = condition.Text,
                                LineNumber = condition.Line
                            };
                            resultParent.AddChild(sourceTextLineResult);
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
                            var sourceTextLineResult = new SourceFileLine
                            {
                                SourceFilePath = filePath,
                                LineText = condition.Text,
                                LineNumber = condition.Line
                            };
                            resultParent.AddChild(sourceTextLineResult);
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
                                IsExpanded = true
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

        return foundResults;
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
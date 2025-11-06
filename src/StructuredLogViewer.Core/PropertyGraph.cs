using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Language.Xml;
using StructuredLogViewer;

namespace Microsoft.Build.Logging.StructuredLogger;

public class PropertyGraph
{
    public class GraphWalkContext
    {
        public ProjectEvaluation Evaluation { get; set; }
        public HashSet<string> PropertyNames { get; set; }
        public Digraph Graph { get; set; }
        public Dictionary<string, Vertex> CurrentNodes { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public Vertex GetVertex(string propertyName)
        {
            if (CurrentNodes.TryGetValue(propertyName, out var existing))
            {
                return existing;
            }

            return Graph.GetOrCreate($"Initial value: {propertyName}", t => propertyName);
        }

        public string FindAssignment(string propertyName, string filePath, int lineNumber)
        {
            string result;

            if (Evaluation.PropertyAssignmentFolder != null)
            {
                result = FindAssignment(Evaluation.PropertyAssignmentFolder);
                if (result != null)
                {
                    return result;
                }
            }

            if (Evaluation.PropertyReassignmentFolder != null)
            {
                result = FindAssignment(Evaluation.PropertyReassignmentFolder);
                if (result != null)
                {
                    return result;
                }
            }

            return null;

            string FindAssignment(TimedNode folder)
            {
                var childFolder = folder.FindChild<Folder>(propertyName);
                if (childFolder == null)
                {
                    return null;
                }

                foreach (var messageWithLocation in childFolder.Children.OfType<PropertyAssignmentMessage>())
                {
                    if (string.Equals(filePath, messageWithLocation.FilePath, StringComparison.OrdinalIgnoreCase) &&
                        messageWithLocation.Line == lineNumber)
                    {
                        return messageWithLocation.NewValue;
                    }
                }

                return null;
            }
        }
    }

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

            var context = new GraphWalkContext();
            context.Evaluation = evaluation;
            context.PropertyNames = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);

            var graphNode = GetPropertyGraph(context);
            if (graphNode != null)
            {
                results = results.Append(graphNode).ToArray();
            }
        }

        return results;
    }

    public SearchResult GetPropertyGraph(GraphWalkContext context)
    {
        var evaluation = context.Evaluation;
        var importsFolder = evaluation.ImportsFolder;

        if (importsFolder == null)
        {
            return null;
        }

        var resultFolder = new Folder { Name = "Property Graph", IsExpanded = true };

        var projectFile = evaluation.ProjectFile;
        var text = PreprocessedFileManager.SourceFileResolver.GetSourceFileText(projectFile);

        if (evaluation.PropertyAssignmentFolder is { } assignmentFolder)
        {
            if (context.PropertyNames != null)
            {
                foreach (var propertyName in context.PropertyNames)
                {
                    var propertyFolder = assignmentFolder.FindChild<Folder>(propertyName);
                    if (propertyFolder != null &&
                        propertyFolder.Children.OfType<PropertyInitialAssignmentMessage>().FirstOrDefault() is { } initialAssignment &&
                        initialAssignment.FilePath == null)
                    {
                        var proxy = new ProxyNode() { Original = initialAssignment };
                        resultFolder.AddChild(proxy);
                    }
                }
            }
        }

        if (Visit(projectFile, resultFolder, text, importsFolder.Children.OfType<Import>(), context) || resultFolder.HasChildren)
        {
            var nodesInReverseOrder = resultFolder.FindChildrenRecursive<SourceFileLineWithHighlights>().Reverse().ToArray();
            bool foundWrite = false;
            foreach (var line in nodesInReverseOrder)
            {
                if (line.IsBold)
                {
                    foundWrite = true;
                }
                else if (foundWrite && !line.IsLowRelevance)
                {
                    line.IsReadBeforeWrite = true;
                }
            }

            return new SearchResult(resultFolder);
        }

        return null;
    }

    private bool Visit(
        string filePath,
        TreeNode resultParent,
        SourceText text,
        IEnumerable<Import> imports,
        GraphWalkContext context)
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
            bool nestedFound = Visit(
                importBefore.ImportedProjectFilePath,
                resultImport,
                importText,
                importBefore.Children.OfType<Import>(),
                context);
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
                context);
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
                    Vertex propertyGroupConditionVertex = null;

                    var usagesInCondition = TryAddCondition(element);
                    if (usagesInCondition != null)
                    {
                        foundResults = true;
                        if (context.Graph is { } graph)
                        {
                            var lineNumber = text.GetLineNumberFromPosition(element.AsSyntaxElement.AsNode.SpanStart) + 1;
                            propertyGroupConditionVertex = graph.GetOrCreate($"PropertyGroup Condition {Path.GetFileName(filePath)}:{lineNumber}");
                            foreach (var usage in usagesInCondition.Select(u => u.Name).Distinct())
                            {
                                var previousVertex = context.GetVertex(usage);
                                propertyGroupConditionVertex.AddChild(previousVertex);
                            }
                        }
                    }

                    foreach (var propertyElement in element.Elements)
                    {
                        string propertyName = propertyElement.Name;
                        string newValue = null;
                        bool reportWrite = false;
                        var propertyNode = (SyntaxNode)propertyElement;
                        var propertySpan = propertyNode.Span;
                        var propertyStart = propertySpan.Start;
                        var startLine = text.GetLineNumberFromPosition(propertyStart) + 1;
                        List<PropertyUsage> usages = new();

                        var propertyCondition = GetParsedCondition(propertyElement, text, filePath);
                        AddUsages(propertyCondition, usages);

                        if (context.PropertyNames == null || context.PropertyNames.Contains(propertyName))
                        {
                            var usage = new PropertyUsage
                            {
                                Name = propertyName,
                                Position = propertyElement.AsSyntaxElement.NameNode.SpanStart,
                                IsWrite = true
                            };
                            usages.Add(usage);
                            reportWrite = true;

                            newValue = context.FindAssignment(propertyName, filePath, startLine);
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

                            if (newValue != null)
                            {
                                var property = new Property
                                {
                                    Name = propertyName,
                                    Value = newValue
                                };
                                sourceTextLineResult.AddChild(property);
                                sourceTextLineResult.IsBold = true;
                                sourceTextLineResult.IsExpanded = true;
                            }
                            else if (reportWrite && context.Evaluation.PropertyAssignmentFolder != null)
                            {
                                sourceTextLineResult.IsLowRelevance = true;
                            }

                            if (context.Graph is { } graph)
                            {
                                var assignmentVertex = graph.GetOrCreate($"{propertyName} {Path.GetFileName(filePath)}:{startLine}");

                                foreach (var usage in usages.Select(u => u.Name).Distinct())
                                {
                                    var previousVertex = context.GetVertex(usage);
                                    assignmentVertex.AddChild(previousVertex);
                                }

                                context.CurrentNodes[propertyName] = assignmentVertex;
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
                    foundResults |= TryAddCondition(element) != null;

                    VisitElement(element);
                }
                else if (name == "Otherwise")
                {
                    VisitElement(element);
                }
                else if (name == "ImportGroup")
                {
                    foundResults |= TryAddCondition(element) != null;

                    VisitElement(element);
                }
                else if (name == "Import")
                {
                    foundResults |= TryAddCondition(element) != null;

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
                                context);
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

        List<PropertyUsage> TryAddCondition(IXmlElement element)
        {
            var condition = GetParsedCondition(element, text, filePath);
            if (condition != null)
            {
                if ((context.PropertyNames == null || condition.PropertyNames.Overlaps(context.PropertyNames)) && element is XmlElementSyntax xmlElement)
                {
                    var sourceTextLineResult = GetOrAddLine(
                        resultParent,
                        filePath,
                        text,
                        xmlElement.StartTag);
                    List<PropertyUsage> usages = new();
                    AddUsages(condition, usages);
                    foreach (var usage in usages)
                    {
                        usage.Position = usage.Position - xmlElement.StartTag.SpanStart;
                        sourceTextLineResult.AddUsage(usage);
                    }

                    return usages;
                }
            }

            return null;
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
                if (context.PropertyNames != null && !context.PropertyNames.Contains(occurrenceText))
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
        SourceText text,
        SyntaxNode node)
    {
        var startLine = text.GetLineNumberFromPosition(node.Span.Start) + 1;
        var lineText = text.GetText(node.Span);
        return GetOrAddLine(parent, filePath, startLine, lineText);
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
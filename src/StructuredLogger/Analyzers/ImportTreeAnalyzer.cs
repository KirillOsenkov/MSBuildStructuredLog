using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class ImportTreeAnalyzer
    {
        private StringCache stringTable;
        private Dictionary<(string, string), string> falseConditionStrings = new();

        public ImportTreeAnalyzer(StringCache stringTable)
        {
            this.stringTable = stringTable;
        }

        public TextNode TryGetImportOrNoImport(ProjectImportedEventArgs args)
        {
            var message = Reflector.GetMessage(args);
            var arguments = Reflector.GetArguments(args);

            if (arguments != null && arguments.Length > 0)
            {
                if (arguments.Length == 4)
                {
                    if (message == Strings.ProjectImported)
                    {
                        var importedProject = stringTable.SoftIntern((string)arguments[0]);
                        var containingProject = stringTable.SoftIntern((string)arguments[1]);
                        var line = ParseInt(arguments[2]);
                        var column = ParseInt(arguments[3]);
                        var import = new Import(
                            containingProject,
                            importedProject,
                            line,
                            column);
                        return import;
                    }
                    else if (
                        message == Strings.ProjectImportSkippedExpressionEvaluatedToEmpty ||
                        message == Strings.ProjectImportSkippedNoMatches ||
                        message == Strings.ProjectImportSkippedMissingFile ||
                        message == Strings.ProjectImportSkippedInvalidFile)
                    {
                        var importedProject = stringTable.SoftIntern((string)arguments[0]);
                        var containingProject = stringTable.SoftIntern((string)arguments[1]);
                        var line = ParseInt(arguments[2]);
                        var column = ParseInt(arguments[3]);

                        var reason = GetNoImportReason(message);

                        var noImport = new NoImport(
                            containingProject,
                            importedProject,
                            line,
                            column,
                            reason);
                        return noImport;
                    }
                }
                else if (arguments.Length == 6)
                {
                    if (message == Strings.ProjectImportSkippedFalseCondition)
                    {
                        var project = (string)arguments[0];
                        var containingProject = (string)arguments[1];
                        var line = ParseInt(arguments[2]);
                        var column = ParseInt(arguments[3]);
                        var condition = (string)arguments[4];
                        var evaluatedCondition = (string)arguments[5];
                        string reason = GetFalseCondition(condition, evaluatedCondition);

                        var noImport = new NoImport(
                            stringTable.SoftIntern(containingProject),
                            stringTable.SoftIntern(project),
                            line,
                            column,
                            reason);
                        return noImport;
                    }
                }
                else if (arguments.Length == 1)
                {
                    if (message == Strings.CouldNotResolveSdk)
                    {
                        var sdk = (string)arguments[0];
                        var noImport = new NoImport(
                            stringTable.SoftIntern(args.ProjectFile),
                            stringTable.SoftIntern(args.ImportedProjectFile),
                            args.LineNumber,
                            args.ColumnNumber,
                            stringTable.SoftIntern(string.Format(message, sdk)));
                        return noImport;
                    }
                }
            }

            // This shouldn't be reachable for newer binlogs
            var parsed = TryGetImportOrNoImport(args.Message, stringTable);
            return parsed;
        }

        private string GetFalseCondition(string condition, string evaluated)
        {
            var key = (condition, evaluated);
            lock (falseConditionStrings)
            {
                if (!falseConditionStrings.TryGetValue(key, out var result))
                {
                    result = $"false condition; ({condition} was evaluated as {evaluated}).";
                    falseConditionStrings[key] = result;
                    stringTable.Intern(result);
                }

                return result;
            }
        }

        private static string GetNoImportReason(string message)
        {
            string reason = "";
            if (message == Strings.ProjectImportSkippedExpressionEvaluatedToEmpty)
            {
                reason = Strings.NoImportEmptyExpression;
            }
            else if (message == Strings.ProjectImportSkippedNoMatches)
            {
                reason = Strings.NoImportNoMatches;
            }
            else if (message == Strings.ProjectImportSkippedMissingFile)
            {
                reason = Strings.NoImportMissingFile;
            }
            else if (message == Strings.ProjectImportSkippedInvalidFile)
            {
                reason = Strings.NoImportInvalidFile;
            }

            return reason;
        }

        public static TextNode TryGetImportOrNoImport(string text, StringCache stringTable)
        {
            var match = Strings.ProjectImportedRegex.Match(text);
            if (match.Success && match.Groups.Count == 5)
            {
                var project = match.Groups["File"].Value;
                var importedProject = match.Groups["ImportedProject"].Value;
                var line = ParseInt(match.Groups["Line"].Value);
                var column = ParseInt(match.Groups["Column"].Value);

                project = stringTable.SoftIntern(project);
                importedProject = stringTable.SoftIntern(importedProject);

                var result = new Import(project, importedProject, line, column);
                return result;
            }

            match = Strings.ProjectWasNotImportedRegex(text, out string reason);
            if (match.Success && match.Groups.Count > 4)
            {
                var project = match.Groups["File"].Value;
                var importedProject = match.Groups["ImportedProject"].Value;
                var line = ParseInt(match.Groups["Line"].Value);
                var column = ParseInt(match.Groups["Column"].Value);

                project = stringTable.SoftIntern(project);
                importedProject = stringTable.SoftIntern(importedProject);
                reason = stringTable.SoftIntern("Not imported due to " + reason);

                var noImport = new NoImport(project, importedProject, line, column, reason);
                return noImport;
            }

            return null;
        }

        private static readonly NumberFormatInfo currentNumberFormatInfo = NumberFormatInfo.CurrentInfo;

        private static int ParseInt(object arg)
        {
            if (arg is string text && int.TryParse(text, NumberStyles.Integer, currentNumberFormatInfo, out int result))
            {
                return result;
            }
            else if (arg is int integer)
            {
                return integer;
            }

            return 0;
        }
    }
}

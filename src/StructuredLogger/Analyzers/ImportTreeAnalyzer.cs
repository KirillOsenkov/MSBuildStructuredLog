using Microsoft.Build.Framework;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class ImportTreeAnalyzer
    {
        public static TextNode TryGetImportOrNoImport(ProjectImportedEventArgs args, StringCache stringTable)
        {
            var message = (string)Reflector.BuildEventArgs_message?.GetValue(args);

            var arguments = Reflector.LazyFormattedBuildEventArgs_arguments?.GetValue(args) as object[];
            if (arguments != null && arguments.Length > 0)
            {
                if (arguments.Length == 4)
                {
                    if (message == Strings.ProjectImported)
                    {
                        var importedProject = stringTable.Intern((string)arguments[0]);
                        var containingProject = stringTable.Intern((string)arguments[1]);
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
                        var importedProject = stringTable.Intern((string)arguments[0]);
                        var containingProject = stringTable.Intern((string)arguments[1]);
                        var line = ParseInt(arguments[2]);
                        var column = ParseInt(arguments[3]);

                        string reason = "";
                        if (message == Strings.ProjectImportSkippedExpressionEvaluatedToEmpty)
                        {
                            reason = "empty expression";
                        }
                        else if (message == Strings.ProjectImportSkippedNoMatches)
                        {
                            reason = "no matches";
                        }
                        else if (message == Strings.ProjectImportSkippedMissingFile)
                        {
                            reason = "missing file";
                        }
                        else if (message == Strings.ProjectImportSkippedInvalidFile)
                        {
                            reason = "invalid file";
                        }

                        var noImport = new NoImport(
                            containingProject,
                            importedProject,
                            line,
                            column,
                            stringTable.Intern(reason));
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
                        string reason = $"false condition; ({condition} was evaluated as {evaluatedCondition}).";

                        var noImport = new NoImport(
                            stringTable.Intern(containingProject),
                            stringTable.Intern(project),
                            line,
                            column,
                            stringTable.Intern(reason));
                        return noImport;
                    }
                }
                else if (arguments.Length == 1)
                {
                    if (message == Strings.CouldNotResolveSdk)
                    {
                        var sdk = (string)arguments[0];
                        var noImport = new NoImport(
                            stringTable.Intern(args.ProjectFile),
                            stringTable.Intern(args.ImportedProjectFile),
                            args.LineNumber,
                            args.ColumnNumber,
                            stringTable.Intern(string.Format(message, sdk)));
                        return noImport;
                    }
                }
            }

            var parsed = TryGetImportOrNoImport(args.Message, stringTable);
            return parsed;
        }

        public static TextNode TryGetImportOrNoImport(string text, StringCache stringTable)
        {
            var match = Strings.ProjectImportedRegex.Match(text);
            if (match.Success && match.Groups.Count == 5)
            {
                var project = match.Groups["File"].Value;
                var importedProject = match.Groups["ImportedProject"].Value;
                var line = int.Parse(match.Groups["Line"].Value);
                var column = int.Parse(match.Groups["Column"].Value);

                project = stringTable.Intern(project);
                importedProject = stringTable.Intern(importedProject);

                var result = new Import(project, importedProject, line, column);
                return result;
            }

            match = Strings.ProjectWasNotImportedRegex(text, out string reason);
            if (match.Success && match.Groups.Count > 4)
            {
                var project = match.Groups["File"].Value;
                var importedProject = match.Groups["ImportedProject"].Value;
                var line = int.Parse(match.Groups["Line"].Value);
                var column = int.Parse(match.Groups["Column"].Value);

                project = stringTable.Intern(project);
                importedProject = stringTable.Intern(importedProject);
                reason = stringTable.Intern("Not imported due to " + reason);

                var noImport = new NoImport(project, importedProject, line, column, reason);
                return noImport;
            }

            return null;
        }

        private static int ParseInt(object arg)
        {
            if (arg is string text && int.TryParse(text, out int result))
            {
                return result;
            }
            else if (arg is int integer)
            {
                return integer;
            }

            return 0;
        }

        public static void VisitMessage(Message message, StringCache stringTable)
        {
            var import = TryGetImportOrNoImport(message.Text, stringTable);
            if (import == null)
            {
                return;
            }

            var evaluation = message.Parent as ProjectEvaluation;
            if (evaluation == null)
            {
                // possible with localized logs
                return;
            }

            evaluation.AddImport(import);

            message.Parent.Children.Remove(message);
        }
    }
}

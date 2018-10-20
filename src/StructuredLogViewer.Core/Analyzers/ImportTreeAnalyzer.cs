using System;
using System.Linq;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class ImportTreeAnalyzer
    {
        public static void Analyze(Folder evaluation, StringCache stringTable)
        {
            evaluation.VisitAllChildren<Message>(m => VisitMessage(m, stringTable), takeChildrenSnapshot: true);
        }

        private static void VisitMessage(Message message, StringCache stringTable)
        {
            var match = Strings.ImportingProjectRegex.Match(message.Text);
            if (match.Success && match.Groups.Count == 5)
            {
                var project = match.Groups["File"].Value;
                var importedProject = match.Groups["ImportedProject"].Value;
                var line = int.Parse(match.Groups["Line"].Value);
                var column = int.Parse(match.Groups["Column"].Value);

                project = stringTable.Intern(project);
                importedProject = stringTable.Intern(importedProject);

                AddImport(
                    message,
                    project,
                    importedProject,
                    line,
                    column,
                    imported: true);
                return;
            }

            match = Strings.ProjectWasNotImportedRegex.Match(message.Text);
            if (match.Success && match.Groups.Count == 6)
            {
                var project = match.Groups["File"].Value;
                var importedProject = match.Groups["ImportedProject"].Value;
                var line = int.Parse(match.Groups["Line"].Value);
                var column = int.Parse(match.Groups["Column"].Value);
                var reason = match.Groups["Reason"].Value;

                project = stringTable.Intern(project);
                importedProject = stringTable.Intern(importedProject);
                reason = stringTable.Intern("Not imported due to " + reason);

                AddImport(
                    message,
                    project,
                    importedProject,
                    line,
                    column,
                    imported: false,
                    reason: reason);
                return;
            }
        }

        private static void AddImport(
            Message message,
            string project,
            string importedProject,
            int line,
            int column,
            bool imported,
            string reason = null)
        {
            var rootProjectNode = (Project)message.Parent as Project;
            if (rootProjectNode.Children.First() is Folder importsFolder && importsFolder.Name == "Imports")
            {
            }
            else
            {
                importsFolder = new Folder()
                {
                    Name = "Imports"
                };
                rootProjectNode.AddChildAtBeginning(importsFolder);
            }

            TextNode import;
            if (imported)
            {
                import = new Import(project, importedProject, line, column);
            }
            else
            {
                import = new NoImport(project, importedProject, line, column, reason);
            }
            
            NamedNode parent = importsFolder;

            if (project != rootProjectNode.ProjectFile)
            {
                parent = rootProjectNode.FindFirstDescendant<Import>(i => string.Equals(i.ImportedProjectFilePath, project, StringComparison.OrdinalIgnoreCase));
                if (parent == null)
                {
                    parent = new Import(rootProjectNode.Name, project, 0, 0);
                    importsFolder.AddChild(parent);
                }
            }

            parent.AddChild(import);

            message.Parent.Children.Remove(message);
        }
    }
}

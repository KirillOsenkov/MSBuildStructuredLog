using System;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class BuildLogWriter : IDisposable
    {
        private readonly string filePath;
        private TreeBinaryWriter writer;

        public static void Write(Build build, string filePath)
        {
            using (var binaryLogWriter = new BuildLogWriter(filePath))
            {
                binaryLogWriter.WriteNode(build);
            }
        }

        private BuildLogWriter(string filePath)
        {
            this.filePath = filePath;
            this.writer = new TreeBinaryWriter(filePath);
        }

        private void WriteNode(BaseNode node)
        {
            writer.WriteNode(Serialization.GetNodeName(node));
            WriteAttributes(node);
            writer.WriteEndAttributes();
            WriteChildren(node);

            if (node is Build build)
            {
                writer.WriteByteArray(build.SourceFilesArchive);
            }
        }

        private void WriteChildren(BaseNode node)
        {
            var treeNode = node as TreeNode;
            if (treeNode != null && treeNode.HasChildren)
            {
                writer.WriteChildrenCount(treeNode.Children.Count);
                foreach (var child in treeNode.Children)
                {
                    WriteNode(child);
                }
            }
            else
            {
                writer.WriteChildrenCount(0);
            }
        }

        private void WriteAttributes(BaseNode node)
        {
            var metadata = node as Metadata;
            if (metadata != null)
            {
                SetString(nameof(Metadata.Name), metadata.Name);
                SetString(nameof(Metadata.Value), metadata.Value);
                return;
            }

            var property = node as Property;
            if (property != null)
            {
                SetString(nameof(Property.Name), property.Name);
                SetString(nameof(Property.Value), property.Value);
                return;
            }

            var message = node as Message;
            if (message != null)
            {
                SetString(nameof(Message.IsLowRelevance), message.IsLowRelevance.ToString());
                SetString(nameof(Message.Timestamp), ToString(message.Timestamp));
                SetString(nameof(Message.Text), message.Text);
                return;
            }

            var folder = node as Folder;
            if (folder != null)
            {
                SetString(nameof(Folder.IsLowRelevance), folder.IsLowRelevance.ToString());
                return;
            }

            var namedNode = node as NamedNode;
            if (namedNode != null)
            {
                SetString(nameof(NamedNode.Name), namedNode.Name?.Replace("\"", ""));
            }

            var textNode = node as TextNode;
            if (textNode != null)
            {
                SetString(nameof(TextNode.Text), textNode.Text);
            }

            if (node is TimedNode timedNode)
            {
                AddStartAndEndTime(timedNode);
                SetString(nameof(TimedNode.NodeId), timedNode.NodeId.ToString());
            }

            var task = node as Task;
            if (task != null)
            {
                SetString(nameof(Task.FromAssembly), task.FromAssembly);
                SetString(nameof(Task.CommandLineArguments), task.CommandLineArguments);
                SetString(nameof(Task.SourceFilePath), task.SourceFilePath);
                return;
            }

            var target = node as Target;
            if (target != null)
            {
                SetString(nameof(Target.DependsOnTargets), target.DependsOnTargets);
                SetString(nameof(Target.IsLowRelevance), target.IsLowRelevance.ToString());
                SetString(nameof(Target.SourceFilePath), target.SourceFilePath);
                return;
            }

            var diagnostic = node as AbstractDiagnostic;
            if (diagnostic != null)
            {
                SetString(nameof(AbstractDiagnostic.Code), diagnostic.Code);
                SetString(nameof(AbstractDiagnostic.File), diagnostic.File);
                SetString(nameof(AbstractDiagnostic.LineNumber), diagnostic.LineNumber.ToString());
                SetString(nameof(AbstractDiagnostic.ColumnNumber), diagnostic.ColumnNumber.ToString());
                SetString(nameof(AbstractDiagnostic.EndLineNumber), diagnostic.EndLineNumber.ToString());
                SetString(nameof(AbstractDiagnostic.EndColumnNumber), diagnostic.EndColumnNumber.ToString());
                SetString(nameof(AbstractDiagnostic.ProjectFile), diagnostic.ProjectFile);
                return;
            }

            var project = node as Project;
            if (project != null)
            {
                SetString(nameof(Project.ProjectFile), project.ProjectFile);
                return;
            }

            var build = node as Build;
            if (build != null)
            {
                SetString(nameof(Build.Succeeded), build.Succeeded.ToString());
                SetString(nameof(Build.IsAnalyzed), build.IsAnalyzed.ToString());
                return;
            }

            var import = node as Import;
            if (import != null)
            {
                SetString(nameof(Import.ProjectFilePath), import.ProjectFilePath);
                SetString(nameof(Import.ImportedProjectFilePath), import.ImportedProjectFilePath);
                SetString(nameof(Import.Line), import.Line.ToString());
                SetString(nameof(Import.Column), import.Column.ToString());
                SetString(nameof(Import.IsLowRelevance), import.IsLowRelevance.ToString());
                return;
            }

            var noImport = node as NoImport;
            if (noImport != null)
            {
                SetString(nameof(Import.ProjectFilePath), noImport.ProjectFilePath);
                SetString(nameof(Import.ImportedProjectFilePath), noImport.ImportedFileSpec);
                SetString(nameof(Import.Line), noImport.Line.ToString());
                SetString(nameof(Import.Column), noImport.Column.ToString());
                SetString(nameof(Import.IsLowRelevance), noImport.IsLowRelevance.ToString());
                return;
            }
        }

        private void SetString(string name, string value)
        {
            writer.WriteAttributeValue(value);
        }

        private void AddStartAndEndTime(TimedNode node)
        {
            SetString(nameof(TimedNode.StartTime), ToString(node.StartTime));
            SetString(nameof(TimedNode.EndTime), ToString(node.EndTime));
        }

        private string ToString(DateTime time)
        {
            return time.ToString("o");
        }

        public void Dispose()
        {
            if (writer != null)
            {
                writer.Dispose();
                writer = null;
            }
        }
    }
}

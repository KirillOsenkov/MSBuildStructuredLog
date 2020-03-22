using System;
using System.IO;
using System.Xml;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class XmlLogWriter
    {
        private XmlWriter xmlWriter;

        public static void WriteToXml(Build build, string logFile)
        {
            var writer = new XmlLogWriter();
            writer.Write(build, logFile);
        }

        public void Write(Build build, string logFile)
        {
            var settings = new XmlWriterSettings()
            {
                Indent = true
            };
            using (FileStream stream = File.Open(logFile, FileMode.Create))
            using (xmlWriter = XmlWriter.Create(stream, settings))
            {
                xmlWriter.WriteStartDocument();
                WriteNode(build);
                xmlWriter.WriteEndDocument();
            }

            xmlWriter = null;
        }

        private void WriteNode(BaseNode node)
        {
            var elementName = Serialization.GetNodeName(node);

            xmlWriter.WriteStartElement(elementName);

            try
            {
                var metadata = node as Metadata;
                if (metadata != null)
                {
                    SetString(nameof(Metadata.Name), metadata.Name);
                    WriteContent(metadata.Value);
                    return;
                }

                var property = node as Property;
                if (property != null)
                {
                    SetString(nameof(Property.Name), property.Name);
                    WriteContent(property.Value);
                    return;
                }

                var message = node as Message;
                if (message != null)
                {
                    if (message.IsLowRelevance)
                    {
                        SetString(nameof(message.IsLowRelevance), "true");
                    }

                    SetString(nameof(Message.Timestamp), ToString(message.Timestamp));
                    WriteContent(message.Text);
                    return;
                }

                var treeNode = node as TreeNode;

                WriteAttributes(treeNode);

                if (treeNode.HasChildren)
                {
                    foreach (var child in treeNode.Children)
                    {
                        WriteNode(child);
                    }
                }
            }
            finally
            {
                xmlWriter.WriteEndElement();
            }
        }

        private void WriteContent(string value)
        {
            xmlWriter.WriteString(value);
        }

        private void WriteAttributes(TreeNode node)
        {
            var folder = node as Folder;
            if (folder != null)
            {
                if (!Serialization.IsValidXmlElementName(folder.Name))
                {
                    SetString(nameof(folder.Name), folder.Name);
                }

                if (folder.IsLowRelevance)
                {
                    SetString(nameof(folder.IsLowRelevance), "true");
                }

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
                SetString(nameof(task.FromAssembly), task.FromAssembly);
                SetString(nameof(task.CommandLineArguments), task.CommandLineArguments);
                SetString(nameof(AttributeNames.File), task.SourceFilePath);

                return;
            }

            var target = node as Target;
            if (target != null)
            {
                SetString(nameof(target.DependsOnTargets), target.DependsOnTargets);
                if (target.IsLowRelevance)
                {
                    SetString(nameof(target.IsLowRelevance), "true");
                }

                SetString(nameof(AttributeNames.File), target.SourceFilePath);

                return;
            }

            var diagnostic = node as AbstractDiagnostic;
            if (diagnostic != null)
            {
                SetString(nameof(diagnostic.Code), diagnostic.Code);
                SetString(nameof(diagnostic.File), diagnostic.File);
                SetString(nameof(diagnostic.LineNumber), diagnostic.LineNumber.ToString());
                SetString(nameof(diagnostic.ColumnNumber), diagnostic.ColumnNumber.ToString());
                SetString(nameof(diagnostic.EndLineNumber), diagnostic.EndLineNumber.ToString());
                SetString(nameof(diagnostic.EndColumnNumber), diagnostic.EndColumnNumber.ToString());
                SetString(nameof(diagnostic.ProjectFile), diagnostic.ProjectFile);
                return;
            }

            var project = node as Project;
            if (project != null)
            {
                SetString(nameof(project.ProjectFile), project.ProjectFile);
                return;
            }

            var build = node as Build;
            if (build != null)
            {
                SetString(nameof(build.Succeeded), build.Succeeded.ToString());
                SetString(nameof(Build.IsAnalyzed), build.IsAnalyzed.ToString());
                return;
            }
        }

        private void SetString(string name, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                xmlWriter.WriteAttributeString(name, value);
            }
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
    }
}

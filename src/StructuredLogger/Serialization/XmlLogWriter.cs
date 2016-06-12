using System;
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
            using (xmlWriter = XmlWriter.Create(logFile, new XmlWriterSettings() { Indent = true }))
            {
                xmlWriter.WriteStartDocument();
                WriteNode(build);
                xmlWriter.WriteEndDocument();
            }

            xmlWriter = null;
        }

        private void WriteNode(object node)
        {
            var elementName = GetName(node);

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

                    xmlWriter.WriteAttributeString(nameof(Message.Timestamp), XmlConvert.ToString(message.Timestamp, XmlDateTimeSerializationMode.RoundtripKind));
                    WriteContent(message.Text);
                    return;
                }

                var treeNode = node as TreeNode;

                WriteAttributes(treeNode);

                var nameValueNode = node as NameValueNode;
                if (nameValueNode != null)
                {
                    xmlWriter.WriteString(nameValueNode.Value);
                    return;
                }

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
            if (!string.IsNullOrEmpty(value))
            {
                xmlWriter.WriteString(value);
            }
            else
            {
                xmlWriter.WriteWhitespace("");
            }
        }

        private void WriteAttributes(TreeNode node)
        {
            var namedNode = node as NamedNode;
            if (namedNode != null)
            {
                SetString(nameof(NamedNode.Name), namedNode.Name?.Replace("\"", ""));
            }

            var folder = node as Folder;
            if (folder != null)
            {
                if (folder.IsLowRelevance)
                {
                    SetString(nameof(folder.IsLowRelevance), "true");
                }

                return;
            }

            var textNode = node as TextNode;
            if (textNode != null)
            {
                SetString(nameof(TextNode.Text), textNode.Text);
            }

            if (node is TimedNode)
            {
                AddStartAndEndTime((TimedNode)node);
            }

            var task = node as Task;
            if (task != null)
            {
                SetString(nameof(task.FromAssembly), task.FromAssembly);
                if (task.CommandLineArguments != null)
                {
                    SetString(nameof(task.CommandLineArguments), task.CommandLineArguments);
                }

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

        private string GetName(object node)
        {
            var folder = node as Folder;
            if (folder != null && folder.Name != null)
            {
                return folder.Name;
            }

            return node.GetType().Name;
        }
    }
}

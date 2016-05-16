using System;
using System.Xml.Linq;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class XmlLogWriter
    {
        public static void WriteToXml(Build build, string logFile)
        {
            var document = new XDocument();
            var writer = new XmlLogWriter();
            var root = writer.WriteNode(build);
            document.Add(root);
            document.Save(logFile);
        }

        public XElement WriteNode(object node)
        {
            var result = new XElement(GetName(node));

            var metadata = node as Metadata;
            if (metadata != null)
            {
                SetString(result, nameof(Metadata.Name), metadata.Name);
                result.Value = metadata.Value;
                return result;
            }

            var property = node as Property;
            if (property != null)
            {
                SetString(result, nameof(Property.Name), property.Name);
                result.Value = property.Value;
                return result;
            }

            var message = node as Message;
            if (message != null)
            {
                if (message.IsLowRelevance)
                {
                    SetString(result, nameof(message.IsLowRelevance), "true");
                }

                result.Add(new XAttribute(nameof(Message.Timestamp), message.Timestamp));
                result.Value = message.Text;
                return result;
            }

            var treeNode = node as TreeNode;

            WriteAttributes(treeNode, result);

            var nameValueNode = node as NameValueNode;
            if (nameValueNode != null)
            {
                result.Value = nameValueNode.Value;
                return result;
            }

            if (treeNode.HasChildren)
            {
                foreach (var child in treeNode.Children)
                {
                    var childElement = WriteNode(child);
                    result.Add(childElement);
                }
            }

            return result;
        }

        private void WriteAttributes(TreeNode node, XElement element)
        {
            var namedNode = node as NamedNode;
            if (namedNode != null)
            {
                SetString(element, nameof(NamedNode.Name), namedNode.Name?.Replace("\"", ""));
            }

            var folder = node as Folder;
            if (folder != null)
            {
                if (folder.IsLowRelevance)
                {
                    SetString(element, nameof(folder.IsLowRelevance), "true");
                }

                return;
            }

            var textNode = node as TextNode;
            if (textNode != null)
            {
                SetString(element, nameof(TextNode.Text), textNode.Text);
            }

            if (node is TimedNode)
            {
                AddStartAndEndTime(element, (TimedNode)node);
            }

            var task = node as Task;
            if (task != null)
            {
                SetString(element, nameof(task.FromAssembly), task.FromAssembly);
                if (task.CommandLineArguments != null)
                {
                    SetString(element, nameof(task.CommandLineArguments), task.CommandLineArguments);
                }

                return;
            }

            var target = node as Target;
            if (target != null)
            {
                SetString(element, nameof(target.DependsOnTargets), target.DependsOnTargets);
                if (target.IsLowRelevance)
                {
                    SetString(element, nameof(target.IsLowRelevance), "true");
                }

                return;
            }

            var diagnostic = node as AbstractDiagnostic;
            if (diagnostic != null)
            {
                SetString(element, nameof(diagnostic.Code), diagnostic.Code);
                SetString(element, nameof(diagnostic.File), diagnostic.File);
                SetString(element, nameof(diagnostic.LineNumber), diagnostic.LineNumber.ToString());
                SetString(element, nameof(diagnostic.ColumnNumber), diagnostic.ColumnNumber.ToString());
                SetString(element, nameof(diagnostic.EndLineNumber), diagnostic.EndLineNumber.ToString());
                SetString(element, nameof(diagnostic.EndColumnNumber), diagnostic.EndColumnNumber.ToString());
                SetString(element, nameof(diagnostic.ProjectFile), diagnostic.ProjectFile);
                return;
            }

            var project = node as Project;
            if (project != null)
            {
                SetString(element, nameof(project.ProjectFile), project.ProjectFile);
                return;
            }

            var build = node as Build;
            if (build != null)
            {
                SetString(element, nameof(build.Succeeded), build.Succeeded.ToString());
                SetString(element, nameof(Build.IsAnalyzed), build.IsAnalyzed.ToString());
                return;
            }
        }

        private void SetString(XElement element, string name, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                element.Add(new XAttribute(name, value));
            }
        }

        private void AddStartAndEndTime(XElement element, TimedNode node)
        {
            SetString(element, nameof(TimedNode.StartTime), ToString(node.StartTime));
            SetString(element, nameof(TimedNode.EndTime), ToString(node.EndTime));
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

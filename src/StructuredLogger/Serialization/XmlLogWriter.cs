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

        public XElement WriteNode(TreeNode node)
        {
            var result = new XElement(GetName(node));

            var message = node as Message;
            if (message != null)
            {
                result.Add(new XAttribute(nameof(Message.Timestamp), message.Timestamp));
                result.Value = message.Text;
                return result;
            }

            WriteAttributes(node, result);

            var nameValueNode = node as NameValueNode;
            if (nameValueNode != null)
            {
                result.Value = nameValueNode.Value;
                return result;
            }

            if (node.HasChildren)
            {
                foreach (var child in node.Children)
                {
                    var childNode = child as TreeNode;
                    if (childNode != null)
                    {
                        var childElement = WriteNode(childNode);
                        result.Add(childElement);
                    }
                }
            }

            return result;
        }

        private void WriteAttributes(TreeNode node, XElement element)
        {
            if (node.IsLowRelevance)
            {
                element.Add(new XAttribute(nameof(node.IsLowRelevance), "true"));
            }

            if (node is NamedNode)
            {
                var named = node as NamedNode;
                if (!string.IsNullOrEmpty(named.Name))
                {
                    element.Add(new XAttribute(nameof(named.Name), named.Name.Replace("\"", "")));
                }
            }

            if (node is TextNode)
            {
                var textNode = node as TextNode;
                if (!string.IsNullOrEmpty(textNode.Text))
                {
                    element.Add(new XAttribute(nameof(textNode.Text), textNode.Text));
                }
            }

            if (node is TimedNode)
            {
                AddStartAndEndTime(element, (TimedNode)node);
            }

            var build = node as Build;
            if (build != null)
            {
                element.Add(new XAttribute(nameof(build.Succeeded), build.Succeeded));
                return;
            }

            var project = node as Project;
            if (project != null)
            {
                element.Add(new XAttribute(nameof(project.ProjectFile), project.ProjectFile));
                return;
            }

            var task = node as Task;
            if (task != null)
            {
                element.Add(new XAttribute(nameof(task.FromAssembly), task.FromAssembly));
                if (task.CommandLineArguments != null)
                {
                    element.Add(new XAttribute(nameof(task.CommandLineArguments), task.CommandLineArguments));
                }

                return;
            }

            var diagnostic = node as AbstractDiagnostic;
            if (diagnostic != null)
            {
                element.Add(new XAttribute(nameof(diagnostic.Code), diagnostic.Code));
                element.Add(new XAttribute(nameof(diagnostic.File), diagnostic.File));
                element.Add(new XAttribute(nameof(diagnostic.LineNumber), diagnostic.LineNumber));
                element.Add(new XAttribute(nameof(diagnostic.ColumnNumber), diagnostic.ColumnNumber));
                element.Add(new XAttribute(nameof(diagnostic.EndLineNumber), diagnostic.EndLineNumber));
                element.Add(new XAttribute(nameof(diagnostic.EndColumnNumber), diagnostic.EndColumnNumber));
            }
        }

        private static void AddStartAndEndTime(XElement element, TimedNode node)
        {
            element.Add(new XAttribute(nameof(TimedNode.StartTime), node.StartTime));
            element.Add(new XAttribute(nameof(TimedNode.EndTime), node.EndTime));
        }

        private string GetName(TreeNode node)
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

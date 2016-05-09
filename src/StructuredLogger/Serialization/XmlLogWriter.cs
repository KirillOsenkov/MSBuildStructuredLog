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
                SetString(element, nameof(node.IsLowRelevance), "true");
            }

            if (node is NamedNode)
            {
                var named = node as NamedNode;
                SetString(element, nameof(named.Name), named.Name?.Replace("\"", ""));
            }

            if (node is TextNode)
            {
                var textNode = node as TextNode;
                SetString(element, nameof(textNode.Text), textNode.Text);
            }

            if (node is TimedNode)
            {
                AddStartAndEndTime(element, (TimedNode)node);
            }

            var build = node as Build;
            if (build != null)
            {
                SetString(element, nameof(build.Succeeded), build.Succeeded.ToString());
                return;
            }

            var project = node as Project;
            if (project != null)
            {
                SetString(element, nameof(project.ProjectFile), project.ProjectFile);
                return;
            }

            var target = node as Target;
            if (target != null)
            {
                SetString(element, nameof(target.DependsOnTargets), target.DependsOnTargets);
                return;
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
            SetString(element, nameof(TimedNode.StartTime), node.StartTime.ToString());
            SetString(element, nameof(TimedNode.EndTime), node.EndTime.ToString());
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

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

        public XElement WriteNode(LogProcessNode node)
        {
            var result = new XElement(GetName(node));

            WriteAttributes(node, result);

            var property = node as Property;
            if (property != null)
            {
                result.Value = property.Value;
                return result;
            }

            var metadata = node as Metadata;
            if (metadata != null)
            {
                result.Value = metadata.Value;
                return result;
            }

            var message = node as Message;
            if (message != null)
            {
                result.Add(new XAttribute("Timestamp", message.Timestamp));
                result.Value = message.Text;
                return result;
            }

            if (node.HasChildren)
            {
                foreach (var child in node.Children)
                {
                    var childNode = child as LogProcessNode;
                    if (childNode != null)
                    {
                        var childElement = WriteNode(childNode);
                        result.Add(childElement);
                    }
                }
            }

            return result;
        }

        private void WriteAttributes(LogProcessNode node, XElement element)
        {
            if (node is Parameter || node is Property || node is Metadata)
            {
                element.Add(new XAttribute("Name", node.Name));
                return;
            }

            var build = node as Build;
            if (build != null)
            {
                element.Add(new XAttribute("Succeeded", build.Succeeded));
                AddStartAndEndTime(element, build);
                return;
            }

            var project = node as Project;
            if (project != null)
            {
                element.Add(new XAttribute("Name", project.Name.Replace("\"", "")));
                AddStartAndEndTime(element, project);
                element.Add(new XAttribute("ProjectFile", project.ProjectFile));
                return;
            }

            var target = node as Target;
            if (target != null)
            {
                element.Add(new XAttribute("Name", target.Name));
                AddStartAndEndTime(element, target);
                return;
            }

            var task = node as Task;
            if (task != null)
            {
                element.Add(new XAttribute("Name", task.Name));
                element.Add(new XAttribute("FromAssembly", task.FromAssembly));
                AddStartAndEndTime(element, task);
                if (task.CommandLineArguments != null)
                {
                    element.Add(new XAttribute("CommandLineArguments", task.CommandLineArguments));
                }

                return;
            }

            var item = node as Item;
            if (item != null)
            {
                if (!string.IsNullOrEmpty(item.Name))
                {
                    element.Add(new XAttribute("Name", item.Name));
                }

                element.Add(new XAttribute("ItemSpec", item.ItemSpec));
                return;
            }
        }

        private static void AddStartAndEndTime(XElement element, LogProcessNode node)
        {
            element.Add(new XAttribute("StartTime", node.StartTime));
            element.Add(new XAttribute("EndTime", node.EndTime));
        }

        private string GetName(LogProcessNode node)
        {
            if (node is Folder && node.Name != null)
            {
                return node.Name;
            }

            return node.GetType().Name;
        }
    }
}

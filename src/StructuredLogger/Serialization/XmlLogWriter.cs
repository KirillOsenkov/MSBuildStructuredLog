using System.Xml.Linq;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class XmlLogWriter
    {
        public static void SaveToXml(Build build, string logFile)
        {
            var document = new XDocument();
            var writer = new XmlLogWriter();
            var root = writer.SaveNode(build);
            document.Add(root);
            document.Save(logFile);
        }

        public XElement SaveNode(LogProcessNode node)
        {
            var result = new XElement(GetName(node));

            var property = node as Property;
            if (property != null)
            {
                result.Add(new XAttribute("Name", property.Name));
                result.Value = property.Value;
                return result;
            }

            var metadata = node as Metadata;
            if (metadata != null)
            {
                result.Add(new XAttribute("Name", metadata.Name));
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

            WriteAttributes(node, result);

            if (node.HasChildren)
            {
                foreach (var child in node.Children)
                {
                    var childNode = child as LogProcessNode;
                    if (childNode != null)
                    {
                        var childElement = SaveNode(childNode);
                        result.Add(childElement);
                    }
                }
            }

            return result;
        }

        private void WriteAttributes(LogProcessNode node, XElement element)
        {
            var build = node as Build;
            if (build != null)
            {
                element.Add(new XAttribute("BuildSucceeded", build.Succeeded));
                element.Add(new XAttribute("StartTime", build.StartTime));
                element.Add(new XAttribute("EndTime", build.EndTime));
                return;
            }

            var project = node as Project;
            if (project != null)
            {
                element.Add(new XAttribute("Name", project.Name.Replace("\"", "")));
                element.Add(new XAttribute("StartTime", project.StartTime));
                element.Add(new XAttribute("EndTime", project.EndTime));
                element.Add(new XAttribute("ProjectFile", project.ProjectFile));
                return;
            }

            var target = node as Target;
            if (target != null)
            {
                element.Add(new XAttribute("Name", target.Name));
                element.Add(new XAttribute("StartTime", target.StartTime));
                element.Add(new XAttribute("EndTime", target.EndTime));
                return;
            }

            var task = node as Task;
            if (task != null)
            {
                element.Add(new XAttribute("Name", task.Name));
                element.Add(new XAttribute("FromAssembly", task.FromAssembly));
                element.Add(new XAttribute("StartTime", task.StartTime));
                element.Add(new XAttribute("EndTime", task.EndTime));
                if (task.CommandLineArguments != null)
                {
                    element.Add(new XElement("CommandLineArguments", task.CommandLineArguments));
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

                element.Add(new XAttribute("Value", item.Text));
            }

            var parameter = node as Parameter;
            if (parameter != null)
            {
                element.Add(new XAttribute("Name", parameter.Name));
            }
        }

        private string GetName(LogProcessNode node)
        {
            if ((node is Folder) && node.Name != null)
            {
                return node.Name;
            }

            return node.GetType().Name;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class XmlLogReader
    {
        public static Build ReadFromXml(string xmlFilePath)
        {
            var doc = XDocument.Load(xmlFilePath, LoadOptions.PreserveWhitespace);
            var root = doc.Root;

            var reader = new XmlLogReader();
            var build = (Build)reader.ReadNode(root);

            return build;
        }

        private static readonly Dictionary<string, Type> objectModelTypes =
            typeof(LogProcessNode)
                .Assembly
                .GetTypes()
                .Where(t => typeof(LogProcessNode).IsAssignableFrom(t))
                .ToDictionary(t => t.Name);

        private LogProcessNode ReadNode(XElement element)
        {
            var name = element.Name.LocalName;
            Type type = null;
            if (!objectModelTypes.TryGetValue(name, out type))
            {
                type = typeof(Folder);
            }

            var node = (LogProcessNode)Activator.CreateInstance(type);

            if (node is Folder)
            {
                node.Name = name;
            }

            ReadAttributes(node, element);

            if (element.HasElements)
            {
                foreach (var childElement in element.Elements())
                {
                    var childNode = ReadNode(childElement);
                    node.AddChild(childNode);
                }
            }

            var property = node as Property;
            if (property != null)
            {
                property.Value = element.Value;
            }

            var metadata = node as Metadata;
            if (metadata != null)
            {
                metadata.Value = element.Value;
            }

            var message = node as Message;
            if (message != null)
            {
                message.Timestamp = GetDateTime(element, "Timestamp");
                message.Text = element.Value;
            }

            return node;
        }

        private void ReadAttributes(LogProcessNode node, XElement element)
        {
            var name = GetString(element, "Name");
            if (node is Parameter || node is Property || node is Metadata)
            {
                node.Name = name;
                return;
            }

            var build = node as Build;
            if (build != null)
            {
                build.Succeeded = GetBoolean(element, "Succeeded");
                AddStartAndEndTime(element, build);
                return;
            }

            var project = node as Project;
            if (project != null)
            {
                project.Name = name;
                project.ProjectFile = GetString(element, "ProjectFile");
                AddStartAndEndTime(element, project);
                return;
            }

            var target = node as Target;
            if (target != null)
            {
                target.Name = name;
                AddStartAndEndTime(element, target);
                return;
            }

            var task = node as Task;
            if (task != null)
            {
                task.Name = name;
                task.FromAssembly = GetString(element, "FromAssembly");
                AddStartAndEndTime(element, task);
                task.CommandLineArguments = GetString(element, "CommandLineArguments");

                return;
            }

            var item = node as Item;
            if (item != null)
            {
                item.Name = name;
                item.ItemSpec = GetString(element, "ItemSpec");
                return;
            }
        }

        private void AddStartAndEndTime(XElement element, LogProcessNode node)
        {
            node.StartTime = GetDateTime(element, "StartTime");
            node.EndTime = GetDateTime(element, "EndTime");
        }

        private static bool GetBoolean(XElement element, string attributeName)
        {
            var text = GetString(element, attributeName);
            bool result;
            bool.TryParse(text, out result);
            return result;
        }

        private static DateTime GetDateTime(XElement element, string attributeName)
        {
            var text = GetString(element, attributeName);
            DateTime result;
            DateTime.TryParse(text, out result);
            return result;
        }

        private static string GetString(XElement element, string attributeName)
        {
            return element.Attribute(attributeName)?.Value;
        }
    }
}

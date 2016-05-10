using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class XmlLogReader
    {
        public static Build ReadFromXml(string xmlFilePath, Action<string> statusUpdate = null)
        {
            Build build = null;

            try
            {
                if (statusUpdate != null)
                {
                    statusUpdate("Loading " + xmlFilePath);
                }

                var doc = XDocument.Load(xmlFilePath, LoadOptions.PreserveWhitespace);
                var root = doc.Root;

                if (statusUpdate != null)
                {
                    statusUpdate("Populating tree");
                }

                var reader = new XmlLogReader();
                build = (Build)reader.ReadNode(root);
            }
            catch (Exception ex)
            {
                build = new Build() { Succeeded = false };
                build.AddChild(new Error() { Text = "Error when opening file: " + xmlFilePath });
                build.AddChild(new Error() { Text = ex.ToString() });
            }

            return build;
        }

        private static readonly Dictionary<string, Type> objectModelTypes =
            typeof(TreeNode)
                .Assembly
                .GetTypes()
                .Where(t => typeof(TreeNode).IsAssignableFrom(t))
                .ToDictionary(t => t.Name);

        private TreeNode ReadNode(XElement element)
        {
            var name = element.Name.LocalName;
            Type type = null;
            if (!objectModelTypes.TryGetValue(name, out type))
            {
                type = typeof(Folder);
            }

            var node = (TreeNode)Activator.CreateInstance(type);

            var folder = node as Folder;
            if (folder != null)
            {
                folder.Name = name;
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

            var nameValueNode = node as NameValueNode;
            if (nameValueNode != null)
            {
                nameValueNode.Value = element.Value;
            }

            var message = node as Message;
            if (message != null)
            {
                message.Timestamp = GetDateTime(element, nameof(Message.Timestamp));
                message.Text = element.Value;
            }

            return node;
        }

        private void ReadAttributes(TreeNode node, XElement element)
        {
            node.IsLowRelevance = GetBoolean(element, nameof(node.IsLowRelevance));

            var name = GetString(element, nameof(NamedNode.Name));
            if (node is NamedNode && name != null)
            {
                var named = node as NamedNode;
                named.Name = name;
            }

            var textNode = node as TextNode;
            if (textNode != null)
            {
                var text = GetString(element, nameof(textNode.Text));
                if (text != null)
                {
                    textNode.Text = text;
                }
            }

            var timedNode = node as TimedNode;
            if (timedNode != null)
            {
                AddStartAndEndTime(element, timedNode);
            }

            var build = node as Build;
            if (build != null)
            {
                build.Succeeded = GetBoolean(element, nameof(Build.Succeeded));
                return;
            }

            var project = node as Project;
            if (project != null)
            {
                project.ProjectFile = GetString(element, nameof(Project.ProjectFile));
                return;
            }

            var target = node as Target;
            if (target != null)
            {
                target.DependsOnTargets = GetString(element, nameof(Target.DependsOnTargets));
            }

            var task = node as Task;
            if (task != null)
            {
                task.FromAssembly = GetString(element, nameof(Task.FromAssembly));
                task.CommandLineArguments = GetString(element, nameof(Task.CommandLineArguments));
                return;
            }

            var diagnostic = node as AbstractDiagnostic;
            if (diagnostic != null)
            {
                diagnostic.Code = GetString(element, nameof(diagnostic.Code));
                diagnostic.File = GetString(element, nameof(diagnostic.File));
                diagnostic.LineNumber = GetInteger(element, nameof(diagnostic.LineNumber));
                diagnostic.ColumnNumber = GetInteger(element, nameof(diagnostic.ColumnNumber));
                diagnostic.EndLineNumber = GetInteger(element, nameof(diagnostic.EndLineNumber));
                diagnostic.EndColumnNumber = GetInteger(element, nameof(diagnostic.EndColumnNumber));
                diagnostic.ProjectFile = GetString(element, nameof(diagnostic.ProjectFile));
            }
        }

        private void AddStartAndEndTime(XElement element, TimedNode node)
        {
            node.StartTime = GetDateTime(element, nameof(TimedNode.StartTime));
            node.EndTime = GetDateTime(element, nameof(TimedNode.EndTime));
        }

        private static bool GetBoolean(XElement element, string attributeName)
        {
            var text = GetString(element, attributeName);
            if (text == null)
            {
                return false;
            }

            bool result;
            bool.TryParse(text, out result);
            return result;
        }

        private static DateTime GetDateTime(XElement element, string attributeName)
        {
            var text = GetString(element, attributeName);
            if (text == null)
            {
                return default(DateTime);
            }

            DateTime result;
            DateTime.TryParse(text, out result);
            return result;
        }

        private static int GetInteger(XElement element, string attributeName)
        {
            var text = GetString(element, attributeName);
            if (text == null)
            {
                return 0;
            }

            int result;
            int.TryParse(text, out result);
            return result;
        }

        private static string GetString(XElement element, string attributeName)
        {
            return element.Attribute(attributeName)?.Value;
        }
    }
}

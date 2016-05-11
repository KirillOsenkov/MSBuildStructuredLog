using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

        private readonly StringTable stringTable = new StringTable();

        private enum AttributeNames
        {
            Name,
            IsLowRelevance,
            Text,
            ProjectFile,
            DependsOnTargets,
            FromAssembly,
            CommandLineArguments,
            Code,
            File,
            LineNumber,
            ColumnNumber,
            EndLineNumber,
            EndColumnNumber,
            StartTime,
            EndTime,
            Succeeded,
            Timestamp
        }

        private static readonly XName[] attributeNames = typeof(AttributeNames)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => XNamespace.None.GetName(f.Name)).ToArray();

        private static readonly Dictionary<string, Type> objectModelTypes =
            typeof(TreeNode)
                .Assembly
                .GetTypes()
                .Where(t => typeof(TreeNode).IsAssignableFrom(t))
                .ToDictionary(t => t.Name);

        private object ReadNode(XElement element)
        {
            var name = element.Name.LocalName;

            if (name == "Metadata")
            {
                var metadata = new Metadata()
                {
                    Name = GetString(element, AttributeNames.Name),
                    Value = stringTable.Intern(element.Value)
                };

                return metadata;
            }
            else if (name == "Property")
            {
                var property = new Property()
                {
                    Name = GetString(element, AttributeNames.Name),
                    Value = stringTable.Intern(element.Value)
                };

                return property;
            }

            Type type = null;
            if (!objectModelTypes.TryGetValue(name, out type))
            {
                type = typeof(Folder);
            }

            var node = (TreeNode)Activator.CreateInstance(type);

            ReadAttributes(node, element);

            if (element.HasElements)
            {
                foreach (var childElement in element.Elements())
                {
                    var childNode = ReadNode(childElement);
                    node.AddChild(childNode);
                }
            }

            return node;
        }

        private void ReadAttributes(TreeNode node, XElement element)
        {
            var item = node as Item;
            if (item != null)
            {
                item.Name = GetString(element, AttributeNames.Name);
                item.Text = GetString(element, AttributeNames.Text);
                return;
            }

            var message = node as Message;
            if (message != null)
            {
                message.IsLowRelevance = GetBoolean(element, AttributeNames.IsLowRelevance);
                message.Timestamp = GetDateTime(element, AttributeNames.Timestamp);
                message.Text = stringTable.Intern(element.Value);
                return;
            }

            var folder = node as Folder;
            if (folder != null)
            {
                folder.IsLowRelevance = GetBoolean(element, AttributeNames.IsLowRelevance);
                folder.Name = GetString(element, AttributeNames.Name);
                return;
            }

            // then, shared "fall-through" tests that are common to many types of nodes
            var namedNode = node as NamedNode;
            if (namedNode != null)
            {
                namedNode.Name = GetString(element, AttributeNames.Name);
            }

            var textNode = node as TextNode;
            if (textNode != null)
            {
                textNode.Text = GetString(element, AttributeNames.Text);
            }

            var timedNode = node as TimedNode;
            if (timedNode != null)
            {
                AddStartAndEndTime(element, timedNode);
            }

            // finally, concrete tests with early exit, sorted by commonality
            var diagnostic = node as AbstractDiagnostic;
            if (diagnostic != null)
            {
                diagnostic.Code = GetString(element, AttributeNames.Code);
                diagnostic.File = GetString(element, AttributeNames.File);
                diagnostic.LineNumber = GetInteger(element, AttributeNames.LineNumber);
                diagnostic.ColumnNumber = GetInteger(element, AttributeNames.ColumnNumber);
                diagnostic.EndLineNumber = GetInteger(element, AttributeNames.EndLineNumber);
                diagnostic.EndColumnNumber = GetInteger(element, AttributeNames.EndColumnNumber);
                diagnostic.ProjectFile = GetString(element, AttributeNames.ProjectFile);
                return;
            }

            var task = node as Task;
            if (task != null)
            {
                task.FromAssembly = GetString(element, AttributeNames.FromAssembly);
                task.CommandLineArguments = GetString(element, AttributeNames.CommandLineArguments);
                return;
            }

            var target = node as Target;
            if (target != null)
            {
                target.IsLowRelevance = GetBoolean(element, AttributeNames.IsLowRelevance);
                target.DependsOnTargets = GetString(element, AttributeNames.DependsOnTargets);
                return;
            }

            var project = node as Project;
            if (project != null)
            {
                project.ProjectFile = GetString(element, AttributeNames.ProjectFile);
                return;
            }

            var build = node as Build;
            if (build != null)
            {
                build.Succeeded = GetBoolean(element, AttributeNames.Succeeded);
                return;
            }
        }

        private void AddStartAndEndTime(XElement element, TimedNode node)
        {
            node.StartTime = GetDateTime(element, AttributeNames.StartTime);
            node.EndTime = GetDateTime(element, AttributeNames.EndTime);
        }

        private bool GetBoolean(XElement element, AttributeNames attributeIndex)
        {
            var text = GetString(element, attributeIndex);
            if (text == null)
            {
                return false;
            }

            bool result;
            bool.TryParse(text, out result);
            return result;
        }

        private DateTime GetDateTime(XElement element, AttributeNames attributeIndex)
        {
            var text = GetString(element, attributeIndex);
            if (text == null)
            {
                return default(DateTime);
            }

            DateTime result;
            DateTime.TryParse(text, out result);
            return result;
        }

        private int GetInteger(XElement element, AttributeNames attributeIndex)
        {
            var text = GetString(element, attributeIndex);
            if (text == null)
            {
                return 0;
            }

            int result;
            int.TryParse(text, out result);
            return result;
        }

        private string GetString(XElement element, AttributeNames attributeIndex)
        {
            var attributeName = attributeNames[(int)attributeIndex];
            var attribute = element.Attribute(attributeName);
            if (attribute != null)
            {
                return stringTable.Intern(attribute.Value);
            }

            return null;
        }
    }
}

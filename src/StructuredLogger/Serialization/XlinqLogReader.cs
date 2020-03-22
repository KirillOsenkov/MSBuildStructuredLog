using System;
using System.Xml.Linq;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class XlinqLogReader
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

                var reader = new XlinqLogReader();
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

        private StringCache stringTable;

        private BaseNode ReadNode(XElement element)
        {
            var name = element.Name.LocalName;

            if (name == "Metadata")
            {
                var metadata = new Metadata()
                {
                    Name = GetString(element, AttributeNames.Name),
                    Value = ReadTextContent(element)
                };

                return metadata;
            }
            else if (name == "Property")
            {
                var property = new Property()
                {
                    Name = GetString(element, AttributeNames.Name),
                    Value = ReadTextContent(element)
                };

                return property;
            }

            var node = Serialization.CreateNode(name);

            var folder = node as Folder;
            if (folder != null)
            {
                folder.Name = name;
            }

            var build = node as Build;
            if (build != null)
            {
                this.stringTable = build.StringTable;
            }

            ReadAttributes(node, element);

            if (element.HasElements)
            {
                var treeNode = (TreeNode)node;
                foreach (var childElement in element.Elements())
                {
                    var childNode = ReadNode(childElement);
                    treeNode.AddChild(childNode);
                }
            }

            return node;
        }

        private string ReadTextContent(XElement element)
        {
            return stringTable.Intern(element.Value);
        }

        private void ReadAttributes(BaseNode node, XElement element)
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
                message.Text = ReadTextContent(element);
                return;
            }

            var folder = node as Folder;
            if (folder != null)
            {
                folder.IsLowRelevance = GetBoolean(element, AttributeNames.IsLowRelevance);
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
                timedNode.NodeId = GetInteger(element, AttributeNames.NodeId);
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
                task.SourceFilePath = GetString(element, AttributeNames.File);
                return;
            }

            var target = node as Target;
            if (target != null)
            {
                target.IsLowRelevance = GetBoolean(element, AttributeNames.IsLowRelevance);
                target.DependsOnTargets = GetString(element, AttributeNames.DependsOnTargets);
                target.SourceFilePath = GetString(element, AttributeNames.File);
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
                build.IsAnalyzed = GetBoolean(element, AttributeNames.IsAnalyzed);
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
            return Serialization.GetBoolean(GetString(element, attributeIndex));
        }

        private DateTime GetDateTime(XElement element, AttributeNames attributeIndex)
        {
            return Serialization.GetDateTime(GetString(element, attributeIndex));
        }

        private int GetInteger(XElement element, AttributeNames attributeIndex)
        {
            return Serialization.GetInteger(GetString(element, attributeIndex));
        }

        private string GetString(XElement element, AttributeNames attributeIndex)
        {
            var attributeName = Serialization.AttributeNameList[(int)attributeIndex];
            var attribute = element.Attribute(attributeName);
            if (attribute != null)
            {
                return stringTable.Intern(attribute.Value);
            }

            return null;
        }
    }
}

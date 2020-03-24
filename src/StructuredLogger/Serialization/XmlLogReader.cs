using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class XmlLogReader
    {
        private StringCache stringTable;
        private XmlReader reader;
        private readonly List<KeyValuePair<string, string>> attributes = new List<KeyValuePair<string, string>>(10);

        public static Build ReadFromXml(string xmlFilePath)
        {
            var build = new XmlLogReader().Read(xmlFilePath);
            return build;
        }

        public static Build ReadFromXml(Stream stream)
        {
            return new XmlLogReader().Read(stream);
        }

        public Build Read(string filePath)
        {
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var build = Read(stream);
                build.LogFilePath = filePath;
                return build;
            }
        }

        public Build Read(Stream stream)
        {
            Build build = new Build();
            this.stringTable = build.StringTable;

            var stack = new Stack<BaseNode>(1024);
            stack.Push(build);

            XmlNodeType previous = XmlNodeType.None;

            try
            {
                var xmlReaderSettings = new XmlReaderSettings()
                {
                    IgnoreWhitespace = true,
                };

                using (reader = XmlReader.Create(stream, xmlReaderSettings))
                {
                    reader.MoveToContent();

                    ReadAttributes();
                    PopulateAttributes(build); // read the attributes on the root Build element that we created manually

                    while (reader.Read())
                    {
                        var nodeType = reader.NodeType;
                        switch (reader.NodeType)
                        {
                            case XmlNodeType.Element:
                                var node = ReadNode();

                                var parent = (TreeNode)stack.Peek();
                                parent.AddChild(node);

                                if (!reader.IsEmptyElement)
                                {
                                    stack.Push(node);
                                }

                                break;
                            case XmlNodeType.EndElement:
                                {
                                    // if the element content is an empty string
                                    if (previous == XmlNodeType.Element)
                                    {
                                        var valueNode = stack.Peek();
                                        SetElementValue(valueNode, "");
                                    }

                                    stack.Pop();
                                    break;
                                }
                            case XmlNodeType.Text:
                                {
                                    var valueNode = stack.Peek();
                                    string value = reader.Value;
                                    SetElementValue(valueNode, stringTable.Intern(value));

                                    break;
                                }
                            case XmlNodeType.Whitespace:
                                {
                                    var valueNode = stack.Peek();
                                    var nameValueNode = valueNode as NameValueNode;
                                    if (nameValueNode != null)
                                    {
                                        nameValueNode.Value = GetCurrentValue();
                                    }
                                    else
                                    {
                                        var message = valueNode as Message;
                                        if (message != null)
                                        {
                                            message.Text = GetCurrentValue();
                                        }
                                    }

                                    break;
                                }
                            case XmlNodeType.None:
                            case XmlNodeType.Attribute:
                            case XmlNodeType.CDATA:
                            case XmlNodeType.EntityReference:
                            case XmlNodeType.Entity:
                            case XmlNodeType.ProcessingInstruction:
                            case XmlNodeType.Comment:
                            case XmlNodeType.Document:
                            case XmlNodeType.DocumentType:
                            case XmlNodeType.DocumentFragment:
                            case XmlNodeType.Notation:
                            case XmlNodeType.SignificantWhitespace:
                            case XmlNodeType.EndEntity:
                            case XmlNodeType.XmlDeclaration:
                            default:
                                break;
                        }

                        previous = nodeType;
                    }
                }
            }
            catch (Exception ex)
            {
                build = new Build() { Succeeded = false };
                build.AddChild(new Error() { Text = "Error when opening XML log file." });
                build.AddChild(new Error() { Text = ex.ToString() });
            }

            return build;
        }

        private void SetElementValue(BaseNode valueNode, string value)
        {
            if (valueNode is NameValueNode nameValueNode)
            {
                nameValueNode.Value = value;
            }
            else if (valueNode is Message message)
            {
                message.Text = value;
            }
        }

        private string GetCurrentValue()
        {
            return stringTable.Intern(reader.Value);
        }

        private BaseNode ReadNode()
        {
            var name = stringTable.Intern(reader.Name);

            ReadAttributes();

            // shortcut for most common types (Metadata and Property)
            if (name == "Metadata")
            {
                var metadata = new Metadata()
                {
                    Name = GetString(AttributeNames.Name)
                };

                return metadata;
            }
            else if (name == "Property")
            {
                var property = new Property()
                {
                    Name = GetString(AttributeNames.Name)
                };

                return property;
            }

            var node = Serialization.CreateNode(name);

            var folder = node as Folder;
            if (folder != null)
            {
                folder.Name = GetString(AttributeNames.Name) ?? name;
                folder.IsLowRelevance = GetBoolean(AttributeNames.IsLowRelevance);
                return folder;
            }

            PopulateAttributes(node);

            return node;
        }

        private void PopulateAttributes(BaseNode node)
        {
            var item = node as Item;
            if (item != null)
            {
                item.Name = GetString(AttributeNames.Name);
                item.Text = GetString(AttributeNames.Text);
                return;
            }

            var message = node as Message;
            if (message != null)
            {
                message.IsLowRelevance = GetBoolean(AttributeNames.IsLowRelevance);
                message.Timestamp = GetDateTime(AttributeNames.Timestamp);
                return;
            }

            // then, shared "fall-through" tests that are common to many types of nodes
            var namedNode = node as NamedNode;
            if (namedNode != null)
            {
                namedNode.Name = GetString(AttributeNames.Name);
            }

            var textNode = node as TextNode;
            if (textNode != null)
            {
                textNode.Text = GetString(AttributeNames.Text);
            }

            var timedNode = node as TimedNode;
            if (timedNode != null)
            {
                AddStartAndEndTime(timedNode);
                timedNode.NodeId = GetInteger(AttributeNames.NodeId);
            }

            // finally, concrete tests with early exit, sorted by commonality
            var diagnostic = node as AbstractDiagnostic;
            if (diagnostic != null)
            {
                diagnostic.Code = GetString(AttributeNames.Code);
                diagnostic.File = GetString(AttributeNames.File);
                diagnostic.LineNumber = GetInteger(AttributeNames.LineNumber);
                diagnostic.ColumnNumber = GetInteger(AttributeNames.ColumnNumber);
                diagnostic.EndLineNumber = GetInteger(AttributeNames.EndLineNumber);
                diagnostic.EndColumnNumber = GetInteger(AttributeNames.EndColumnNumber);
                diagnostic.ProjectFile = GetString(AttributeNames.ProjectFile);
                return;
            }

            var task = node as Task;
            if (task != null)
            {
                task.FromAssembly = GetString(AttributeNames.FromAssembly);
                task.CommandLineArguments = GetString(AttributeNames.CommandLineArguments);
                task.SourceFilePath = GetString(AttributeNames.File);
                return;
            }

            var target = node as Target;
            if (target != null)
            {
                target.IsLowRelevance = GetBoolean(AttributeNames.IsLowRelevance);
                target.DependsOnTargets = GetString(AttributeNames.DependsOnTargets);
                target.SourceFilePath = GetString(AttributeNames.File);
                return;
            }

            var project = node as Project;
            if (project != null)
            {
                project.ProjectFile = GetString(AttributeNames.ProjectFile);
                return;
            }

            var build = node as Build;
            if (build != null)
            {
                build.Succeeded = GetBoolean(AttributeNames.Succeeded);
                build.IsAnalyzed = GetBoolean(AttributeNames.IsAnalyzed);
                return;
            }
        }

        private void ReadAttributes()
        {
            attributes.Clear();

            if (reader.HasAttributes)
            {
                if (reader.MoveToFirstAttribute())
                {
                    do
                    {
                        var attributeName = stringTable.Intern(reader.Name);
                        var attributeValue = stringTable.Intern(reader.Value);
                        attributes.Add(new KeyValuePair<string, string>(attributeName, attributeValue));
                    }
                    while (reader.MoveToNextAttribute());
                    reader.MoveToElement();
                }
            }
        }

        private void AddStartAndEndTime(TimedNode node)
        {
            node.StartTime = GetDateTime(AttributeNames.StartTime);
            node.EndTime = GetDateTime(AttributeNames.EndTime);
        }

        private bool GetBoolean(AttributeNames attributeIndex)
        {
            return Serialization.GetBoolean(GetString(attributeIndex));
        }

        private DateTime GetDateTime(AttributeNames attributeIndex)
        {
            return Serialization.GetDateTime(GetString(attributeIndex));
        }

        private int GetInteger(AttributeNames attributeIndex)
        {
            return Serialization.GetInteger(GetString(attributeIndex));
        }

        private string GetString(AttributeNames attributeIndex)
        {
            var attributeName = Serialization.AttributeLocalNameList[(int)attributeIndex];
            for (int i = 0; i < attributes.Count; i++)
            {
                if (attributeName == attributes[i].Key)
                {
                    return attributes[i].Value;
                }
            }

            return null;
        }
    }
}

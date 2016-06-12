using System;
using System.Collections.Generic;
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
            return new XmlLogReader().Read(xmlFilePath);
        }

        public Build Read(string filePath)
        {
            Build build = new Build();
            this.stringTable = build.StringTable;

            var stack = new Stack<object>(1024);
            stack.Push(build);

            try
            {
                var xmlReaderSettings = new XmlReaderSettings()
                {
                    IgnoreWhitespace = true,
                };

                using (reader = XmlReader.Create(filePath, xmlReaderSettings))
                {
                    reader.MoveToContent();

                    ReadAttributes();
                    PopulateAttributes(build); // read the attributes on the root Build element that we created manually

                    while (reader.Read())
                    {
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
                                stack.Pop();
                                break;
                            case XmlNodeType.Text:
                                {
                                    var valueNode = stack.Peek();
                                    var nameValueNode = valueNode as NameValueNode;
                                    if (nameValueNode != null)
                                    {
                                        nameValueNode.Value = reader.Value;
                                    }
                                    else
                                    {
                                        var message = valueNode as Message;
                                        if (message != null)
                                        {
                                            message.Text = reader.Value;
                                        }
                                    }

                                    break;
                                }
                            case XmlNodeType.Whitespace:
                                {
                                    var valueNode = stack.Peek();
                                    var nameValueNode = valueNode as NameValueNode;
                                    if (nameValueNode != null)
                                    {
                                        nameValueNode.Value = reader.Value;
                                    }
                                    else
                                    {
                                        var message = valueNode as Message;
                                        if (message != null)
                                        {
                                            message.Text = reader.Value;
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
                    }
                }
            }
            catch (Exception ex)
            {
                build = new Build() { Succeeded = false };
                build.AddChild(new Error() { Text = "Error when opening file: " + filePath });
                build.AddChild(new Error() { Text = ex.ToString() });
            }

            return build;
        }

        private object ReadNode()
        {
            var name = reader.Name;

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

            TreeNode node = CreateNode(name);

            PopulateAttributes(node);

            return node;
        }

        private static TreeNode CreateNode(string name)
        {
            Type type = null;
            if (!Serialization.ObjectModelTypes.TryGetValue(name, out type))
            {
                type = typeof(Folder);
            }

            var node = (TreeNode)Activator.CreateInstance(type);
            return node;
        }

        private void PopulateAttributes(TreeNode node)
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

            var folder = node as Folder;
            if (folder != null)
            {
                folder.IsLowRelevance = GetBoolean(AttributeNames.IsLowRelevance);
                folder.Name = GetString(AttributeNames.Name);
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
                return;
            }

            var target = node as Target;
            if (target != null)
            {
                target.IsLowRelevance = GetBoolean(AttributeNames.IsLowRelevance);
                target.DependsOnTargets = GetString(AttributeNames.DependsOnTargets);
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
            var text = GetString(attributeIndex);
            if (text == null)
            {
                return false;
            }

            bool result;
            bool.TryParse(text, out result);
            return result;
        }

        private DateTime GetDateTime(AttributeNames attributeIndex)
        {
            var text = GetString(attributeIndex);
            if (text == null)
            {
                return default(DateTime);
            }

            DateTime result;
            DateTime.TryParse(text, out result);
            return result;
        }

        private int GetInteger(AttributeNames attributeIndex)
        {
            var text = GetString(attributeIndex);
            if (text == null)
            {
                return 0;
            }

            int result;
            int.TryParse(text, out result);
            return result;
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

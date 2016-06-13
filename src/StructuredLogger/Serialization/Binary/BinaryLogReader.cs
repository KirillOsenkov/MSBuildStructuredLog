using System;
using System.Collections.Generic;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class BinaryLogReader : IDisposable
    {
        private readonly string filePath;
        private TreeBinaryReader reader;
        private readonly Queue<string> attributes = new Queue<string>(10);

        public static Build Read(string filePath)
        {
            using (var binaryLogReader = new BinaryLogReader(filePath))
            {
                return (Build)binaryLogReader.ReadNode();
            }
        }

        private BinaryLogReader(string filePath)
        {
            this.filePath = filePath;
            this.reader = new TreeBinaryReader(filePath);
        }

        private object ReadNode()
        {
            var name = reader.ReadString();
            var node = Serialization.CreateNode(name);
            var folder = node as Folder;
            if (folder != null)
            {
                folder.Name = name;
            }

            reader.ReadStringArray(attributes);
            SetAttributes(node);
            int childrenCount = reader.ReadInt32();
            if (childrenCount > 0)
            {
                var treeNode = (TreeNode)node;
                for (int i = 0; i < childrenCount; i++)
                {
                    var child = ReadNode();
                    treeNode.AddChild(child);
                }
            }

            return node;
        }

        private void SetAttributes(object node)
        {
            var metadata = node as Metadata;
            if (metadata != null)
            {
                metadata.Name = attributes.Dequeue();
                metadata.Value = attributes.Dequeue();
                return;
            }

            var property = node as Property;
            if (property != null)
            {
                property.Name = attributes.Dequeue();
                property.Value = attributes.Dequeue();
                return;
            }

            var message = node as Message;
            if (message != null)
            {
                message.IsLowRelevance = Serialization.GetBoolean(attributes.Dequeue());
                message.Timestamp = Serialization.GetDateTime(attributes.Dequeue());
                message.Text = attributes.Dequeue();
                return;
            }

            var folder = node as Folder;
            if (folder != null)
            {
                folder.IsLowRelevance = Serialization.GetBoolean(attributes.Dequeue());
                return;
            }

            var namedNode = node as NamedNode;
            if (namedNode != null)
            {
                namedNode.Name = attributes.Dequeue();
            }

            var textNode = node as TextNode;
            if (textNode != null)
            {
                textNode.Text = attributes.Dequeue();
            }

            var timedNode = node as TimedNode;
            if (timedNode != null)
            {
                timedNode.StartTime = Serialization.GetDateTime(attributes.Dequeue());
                timedNode.EndTime = Serialization.GetDateTime(attributes.Dequeue());
            }

            var task = node as Task;
            if (task != null)
            {
                task.FromAssembly = attributes.Dequeue();
                task.CommandLineArguments = attributes.Dequeue();
                return;
            }

            var target = node as Target;
            if (target != null)
            {
                target.DependsOnTargets = attributes.Dequeue();
                target.IsLowRelevance = Serialization.GetBoolean(attributes.Dequeue());
                return;
            }

            var diagnostic = node as AbstractDiagnostic;
            if (diagnostic != null)
            {
                diagnostic.Code = attributes.Dequeue();
                diagnostic.File = attributes.Dequeue();
                diagnostic.LineNumber = Serialization.GetInteger(attributes.Dequeue());
                diagnostic.ColumnNumber = Serialization.GetInteger(attributes.Dequeue());
                diagnostic.EndLineNumber = Serialization.GetInteger(attributes.Dequeue());
                diagnostic.EndColumnNumber = Serialization.GetInteger(attributes.Dequeue());
                diagnostic.ProjectFile = attributes.Dequeue();
                return;
            }

            var project = node as Project;
            if (project != null)
            {
                project.ProjectFile = attributes.Dequeue();
                return;
            }

            var build = node as Build;
            if (build != null)
            {
                build.Succeeded = Serialization.GetBoolean(attributes.Dequeue());
                build.IsAnalyzed = Serialization.GetBoolean(attributes.Dequeue());
                return;
            }
        }

        public void Dispose()
        {
            if (reader != null)
            {
                reader.Dispose();
                reader = null;
            }
        }
    }
}

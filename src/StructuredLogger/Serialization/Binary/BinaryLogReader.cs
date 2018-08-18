using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class BinaryLogReader : IDisposable
    {
        private TreeBinaryReader reader;
        private readonly Queue<string> attributes = new Queue<string>(10);

        private readonly bool formatSupportsSourceFiles;
        private readonly bool formatSupportsEmbeddedProjectImportsArchive;
        private readonly bool formatSupportsTimedNodeId;
        private readonly bool formatIsValid;

        public static Build Read(string filePath)
        {
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var projectImportsZipFile = Path.ChangeExtension(filePath, ".ProjectImports.zip");
                byte[] projectImportsArchive = null;
                if (File.Exists(projectImportsZipFile))
                {
                    projectImportsArchive = File.ReadAllBytes(projectImportsZipFile);
                }

                var build = Read(stream, projectImportsArchive);
                build.LogFilePath = filePath;
                return build;
            }
        }

        public static Build Read(Stream stream, byte[] projectImportsArchive = null)
        {
            using (var binaryLogReader = new BinaryLogReader(stream))
            {
                if (!binaryLogReader.formatIsValid)
                {
                    throw new Exception("Invalid log file format");
                }

                var build = (Build)binaryLogReader.ReadNode();
                var buildStringCache = build.StringTable;

                foreach (var stringInstance in binaryLogReader.reader.StringTable)
                {
                    buildStringCache.Intern(stringInstance);
                }

                if (build.SourceFilesArchive == null && projectImportsArchive != null)
                {
                    build.SourceFilesArchive = projectImportsArchive;
                }

                return build;
            }
        }

        private BinaryLogReader(string filePath)
        {
            this.reader = new TreeBinaryReader(filePath);
            this.formatSupportsSourceFiles = reader.Version > new Version(1, 0, 130);
            this.formatSupportsEmbeddedProjectImportsArchive = reader.Version > new Version(1, 1, 87);
            this.formatSupportsTimedNodeId = reader.Version > new Version(1, 1, 153);
            this.formatIsValid = reader.IsValid();
        }

        private BinaryLogReader(Stream stream)
        {
            this.reader = new TreeBinaryReader(stream);
            this.formatSupportsSourceFiles = reader.Version > new Version(1, 0, 130);
            this.formatSupportsEmbeddedProjectImportsArchive = reader.Version > new Version(1, 1, 87);
            this.formatSupportsTimedNodeId = reader.Version > new Version(1, 1, 153);
            this.formatIsValid = reader.IsValid();
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

            if (node is Build build && formatSupportsEmbeddedProjectImportsArchive)
            {
                build.SourceFilesArchive = reader.ReadByteArray();
            }

            return node;
        }

        private string Dequeue()
        {
            if (attributes.Count > 0)
            {
                return attributes.Dequeue();
            }

            return null;
        }

        private void SetAttributes(object node)
        {
            var metadata = node as Metadata;
            if (metadata != null)
            {
                metadata.Name = Dequeue();
                metadata.Value = Dequeue();
                return;
            }

            var property = node as Property;
            if (property != null)
            {
                property.Name = Dequeue();
                property.Value = Dequeue();
                return;
            }

            var message = node as Message;
            if (message != null)
            {
                message.IsLowRelevance = Serialization.GetBoolean(Dequeue());
                message.Timestamp = Serialization.GetDateTime(Dequeue());
                message.Text = Dequeue();
                return;
            }

            var folder = node as Folder;
            if (folder != null)
            {
                folder.IsLowRelevance = Serialization.GetBoolean(Dequeue());
                return;
            }

            var namedNode = node as NamedNode;
            if (namedNode != null)
            {
                namedNode.Name = Dequeue();
            }

            var textNode = node as TextNode;
            if (textNode != null)
            {
                textNode.Text = Dequeue();
            }

            var timedNode = node as TimedNode;
            if (timedNode != null)
            {
                timedNode.StartTime = Serialization.GetDateTime(Dequeue());
                timedNode.EndTime = Serialization.GetDateTime(Dequeue());
                if (formatSupportsTimedNodeId)
                {
                    timedNode.NodeId = Serialization.GetInteger(Dequeue());
                }
            }

            var task = node as Task;
            if (task != null)
            {
                task.FromAssembly = Dequeue();
                task.CommandLineArguments = Dequeue();
                if (formatSupportsSourceFiles)
                {
                    task.SourceFilePath = Dequeue();
                }

                return;
            }

            var target = node as Target;
            if (target != null)
            {
                target.DependsOnTargets = Dequeue();
                target.IsLowRelevance = Serialization.GetBoolean(Dequeue());
                if (formatSupportsSourceFiles)
                {
                    target.SourceFilePath = Dequeue();
                }

                return;
            }

            var diagnostic = node as AbstractDiagnostic;
            if (diagnostic != null)
            {
                diagnostic.Code = Dequeue();
                diagnostic.File = Dequeue();
                diagnostic.LineNumber = Serialization.GetInteger(Dequeue());
                diagnostic.ColumnNumber = Serialization.GetInteger(Dequeue());
                diagnostic.EndLineNumber = Serialization.GetInteger(Dequeue());
                diagnostic.EndColumnNumber = Serialization.GetInteger(Dequeue());
                diagnostic.ProjectFile = Dequeue();
                return;
            }

            var project = node as Project;
            if (project != null)
            {
                project.ProjectFile = Dequeue();
                return;
            }

            var build = node as Build;
            if (build != null)
            {
                build.Succeeded = Serialization.GetBoolean(Dequeue());
                build.IsAnalyzed = Serialization.GetBoolean(Dequeue());
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

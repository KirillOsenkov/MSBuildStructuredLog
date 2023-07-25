using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class BuildLogReader : IDisposable
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
            return Read(stream, projectImportsArchive, version: null);
        }

        public static Build Read(Stream stream, byte[] projectImportsArchive, Version version)
        {
            using (var binaryLogReader = new BuildLogReader(stream, version))
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

        private BuildLogReader(string filePath)
        {
            this.reader = new TreeBinaryReader(filePath);
            this.formatSupportsSourceFiles = reader.Version > new Version(1, 0, 130);
            this.formatSupportsEmbeddedProjectImportsArchive = reader.Version > new Version(1, 1, 87);
            this.formatSupportsTimedNodeId = reader.Version > new Version(1, 1, 153);
            this.formatIsValid = reader.IsValid();
        }

        private BuildLogReader(Stream stream, Version version)
        {
            this.reader = new TreeBinaryReader(stream, version);
            this.formatSupportsSourceFiles = reader.Version > new Version(1, 0, 130);
            this.formatSupportsEmbeddedProjectImportsArchive = reader.Version > new Version(1, 1, 87);
            this.formatSupportsTimedNodeId = reader.Version > new Version(1, 1, 153);
            this.formatIsValid = reader.IsValid();
        }

        private BaseNode ReadNode()
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
                if (!(node is TreeNode))
                {
                    // OK we got ourselves into a situation here.
                    // https://github.com/KirillOsenkov/MSBuildStructuredLog/issues/242
                    // There's a design flaw in the BuildLog format. I took a shortcut
                    // and for Folder nodes I just write the name of the folder instead
                    // of specifying that the element is a Folder in the first place.
                    // Unfortunately I didn't think about Folders named "Property", 
                    // "Target", etc. 
                    // So the deserialization logic when it sees a string called "Property"
                    // it assumes we have a property here, instead of a Folder named
                    // "Property". But properties have children! We're in a pickle now.
                    // Longer term I need to modify the format to not do this optimization
                    // and always write "Folder" for folders and write the name separately.
                    // For now I don't have time to do this right, so put in the dirty 
                    // hack to recover from this situation. If it says it's a "Property"
                    // but expects children, it means it's actually a folder with name
                    // "Property".
                    folder = new Folder();
                    folder.Name = Serialization.GetNodeName(node);
                    node = folder;
                }

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

        private void SetAttributes(BaseNode node)
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

            var import = node as Import;
            if (import != null)
            {
                import.ProjectFilePath = Dequeue();
                import.ImportedProjectFilePath = Dequeue();
                import.Line = Serialization.GetInteger(Dequeue());
                import.Column = Serialization.GetInteger(Dequeue());
                import.IsLowRelevance = Serialization.GetBoolean(Dequeue());
                return;
            }

            var noImport = node as NoImport;
            if (noImport != null)
            {
                noImport.ProjectFilePath = Dequeue();
                noImport.ImportedFileSpec = Dequeue();
                noImport.Line = Serialization.GetInteger(Dequeue());
                noImport.Column = Serialization.GetInteger(Dequeue());
                noImport.IsLowRelevance = Serialization.GetBoolean(Dequeue());
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

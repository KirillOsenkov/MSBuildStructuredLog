using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class FileCopyInfo
    {
        public FileCopyOperation FileCopyOperation { get; set; }
        public Task Task { get; set; }
        public Target Target { get; set; }
        public Project Project { get; set; }
    }

    public class FileData : IComparable<FileData>
    {
        public string Name { get; set; }

        public List<FileCopyInfo> Incoming { get; } = new List<FileCopyInfo>();
        public List<FileCopyInfo> Outgoing { get; } = new List<FileCopyInfo>();

        public int CompareTo(FileData other)
        {
            if (other == null)
            {
                return 1;
            }

            return StringComparer.OrdinalIgnoreCase.Compare(Name, other.Name);
        }
    }

    public class DirectoryData
    {
        public string Name { get; set; }
        public DirectoryData Parent { get; set; }

        public List<FileData> Files { get; } = new List<FileData>();
    }

    public class FileCopyMap
    {
        private Dictionary<string, DirectoryData> directories = new Dictionary<string, DirectoryData>(StringComparer.OrdinalIgnoreCase);

        public void AnalyzeTask(Task task)
        {
            if (task is CopyTask copyTask)
            {
                AnalyzeCopyTask(copyTask);
            }
        }

        private void AnalyzeCopyTask(CopyTask copyTask)
        {
            foreach (var copyOperation in copyTask.FileCopyOperations)
            {
                AnalyzeCopyOperation(copyOperation, copyTask);
            }
        }

        private void AnalyzeCopyOperation(FileCopyOperation copyOperation, Task task)
        {
            var source = copyOperation.Source;
            var destination = copyOperation.Destination;

            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(destination))
            {
                return;
            }

            var project = task.GetNearestParent<Project>();

            if (!Path.IsPathRooted(source))
            {
                source = Path.Combine(project.ProjectDirectory, source);
            }

            if (!Path.IsPathRooted(destination))
            {
                destination = Path.Combine(project.ProjectDirectory, destination);
            }

            source = Path.GetFullPath(source);
            destination = Path.GetFullPath(destination);

            if (string.Equals(source, destination))
            {
                return;
            }

            var info = new FileCopyInfo
            {
                FileCopyOperation = copyOperation,
                Task = task,
                Target = task.GetNearestParent<Target>(),
                Project = project
            };

            var sourceData = GetFile(source);
            sourceData.Outgoing.Add(info);

            var destinationData = GetFile(destination);
            destinationData.Incoming.Add(info);
        }

        public FileData GetFile(string filePath)
        {
            lock (directories)
            {
                var directory = Path.GetDirectoryName(filePath);
                var fileName = Path.GetFileName(filePath);

                FileData fileData;

                var directoryData = GetDirectory(directory);
                int index = directoryData.Files.BinarySearch(fileName, f => f.Name);
                if (index >= 0)
                {
                    fileData = directoryData.Files[index];
                }
                else
                {
                    fileData = new FileData();
                    fileData.Name = fileName;
                    directoryData.Files.Insert(~index, fileData);
                }

                return fileData;
            }
        }

        private static char[] separators = new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };

        public DirectoryData GetDirectory(string path)
        {
            if (path.Length > 3)
            {
                path = path.TrimEnd(separators);
            }

            lock (directories)
            {
                if (!directories.TryGetValue(path, out var directoryData))
                {
                    var parentDirectory = Path.GetDirectoryName(path);
                    var name = Path.GetFileName(path);
                    if (string.IsNullOrEmpty(parentDirectory))
                    {
                        name = path;
                    }

                    directoryData = new DirectoryData()
                    {
                        Name = name,
                    };

                    if (!string.IsNullOrEmpty(parentDirectory))
                    {
                        var parentData = GetDirectory(parentDirectory);
                        directoryData.Parent = parentData;
                    }

                    directories[path] = directoryData;
                }

                return directoryData;
            }
        }
    }
}

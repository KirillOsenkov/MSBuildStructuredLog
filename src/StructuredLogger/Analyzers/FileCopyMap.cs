using System;
using System.Collections.Generic;
using System.IO;
using StructuredLogViewer;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class FileCopyInfo
    {
        public FileCopyOperation FileCopyOperation { get; set; }
        public Task Task { get; set; }
        public Target Target { get; set; }
        public Project Project { get; set; }

        public override string ToString() => FileCopyOperation.ToString();
    }

    public class FileData : IComparable<FileData>
    {
        public string Name { get; set; }
        public string FilePath { get; set; }
        public DirectoryData Directory { get; set; }

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

        public override string ToString() => Name;
    }

    public class DirectoryData
    {
        public string Name { get; set; }
        public DirectoryData Parent { get; set; }

        public List<FileData> Files { get; } = new List<FileData>();

        public override string ToString()
        {
            if (Parent != null)
            {
                return Path.Combine(Parent.ToString(), Name);
            }

            return Name;
        }
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

        public FileData GetFile(string filePath, bool create = true)
        {
            lock (directories)
            {
                var directory = Path.GetDirectoryName(filePath);
                var fileName = Path.GetFileName(filePath);

                FileData fileData = null;

                var directoryData = GetDirectory(directory, create);
                if (directoryData == null)
                {
                    return null;
                }

                int index = directoryData.Files.BinarySearch(fileName, f => f.Name);
                if (index >= 0)
                {
                    fileData = directoryData.Files[index];
                }
                else if (create)
                {
                    fileData = new FileData();
                    fileData.Name = fileName;
                    fileData.FilePath = filePath;
                    fileData.Directory = directoryData;
                    directoryData.Files.Insert(~index, fileData);
                }

                return fileData;
            }
        }

        private static char[] separators = new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
        private static readonly string DirectorySeparator = Path.DirectorySeparatorChar.ToString();
        private static readonly string AltDirectorySeparator = Path.AltDirectorySeparatorChar.ToString();

        public DirectoryData GetDirectory(string path, bool create = true)
        {
            if (path.Length > 3)
            {
                path = path.TrimEnd(separators);
            }

            lock (directories)
            {
                if (!directories.TryGetValue(path, out var directoryData) && create)
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

        public void GetResults(NodeQueryMatcher matcher, IList<SearchResult> resultSet)
        {
            if (matcher.Words.Count == 1)
            {
                var word = matcher.Words[0];
                var text = word.Word;

                object data = TryGetDirectoryOrFile(text);
                if (data is DirectoryData directoryData)
                {
                    GetResults(directoryData, resultSet);
                }
                else if (data is FileData fileData)
                {
                    GetResults(fileData, resultSet);
                }
            }
        }

        private void GetResults(FileData fileData, IList<SearchResult> resultSet, string matchText = null)
        {
            if (matchText == null)
            {
                matchText = fileData.FilePath;
            }

            foreach (var incoming in fileData.Incoming)
            {
                var message = incoming.FileCopyOperation.Message;
                var result = new SearchResult(message);
                result.AddMatch(message.Text, matchText);
                result.RootFolder = "Incoming";
                resultSet.Add(result);
            }

            foreach (var outgoing in fileData.Outgoing)
            {
                var message = outgoing.FileCopyOperation.Message;
                var result = new SearchResult(message);
                result.AddMatch(message.Text, matchText);
                result.RootFolder = "Outgoing";
                resultSet.Add(result);
            }
        }

        private void GetResults(DirectoryData directoryData, IList<SearchResult> resultSet)
        {
            string directoryPath = directoryData.ToString();

            foreach (var fileData in directoryData.Files)
            {
                GetResults(fileData, resultSet, matchText: directoryPath);
            }
        }

        private object TryGetDirectoryOrFile(string text)
        {
            text = text.TrimEnd(separators);

            if (text.Contains(DirectorySeparator))
            {
                var fileData = GetFile(text, create: false);
                if (fileData != null)
                {
                    return fileData;
                }

                var directoryData = GetDirectory(text, create: false);
                if (directoryData != null)
                {
                    return directoryData;
                }
            }

            return null;
        }
    }
}

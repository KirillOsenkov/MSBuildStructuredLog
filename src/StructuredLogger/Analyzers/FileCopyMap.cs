using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

    public class FileCopyMap : ISearchExtension
    {
        private static char[] separators = new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
        private static readonly string DirectorySeparator = Path.DirectorySeparatorChar.ToString();
        private static readonly string AltDirectorySeparator = Path.AltDirectorySeparatorChar.ToString();

        private Dictionary<string, DirectoryData> directories = new Dictionary<string, DirectoryData>(StringComparer.OrdinalIgnoreCase);

        public event Action<FileData, IList<SearchResult>> FoundSingleFileCopy;

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

            source = TextUtilities.NormalizeFilePath(source);
            destination = TextUtilities.NormalizeFilePath(destination);

            if (string.Equals(source, destination, StringComparison.OrdinalIgnoreCase))
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

        public bool TryGetResults(NodeQueryMatcher matcher, IList<SearchResult> resultSet, int maxResults)
        {
            if (!string.Equals(matcher.TypeKeyword, "copy", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (matcher.Terms.Count == 0)
            {
                resultSet.Add(new SearchResult(new Note { Text = "Specify a directory or file path, or a partial file name" }));

                return true;
            }

            if (matcher.Terms.Count == 1)
            {
                var word = matcher.Terms[0];
                var text = word.Word;

                object data = TryGetDirectoryOrFile(text);
                if (data is DirectoryData directoryData)
                {
                    GetResults(directoryData, resultSet, matcher, maxResults);
                }
                else if (data is FileData fileData)
                {
                    GetResults(fileData, resultSet, matcher, maxResults);
                    if (resultSet.Count == 1)
                    {
                        FoundSingleFileCopy?.Invoke(fileData, resultSet);
                        TryExplainSingleFileCopy(fileData, resultSet);
                    }
                }
                else
                {
                    TryGetFiles(text, resultSet, matcher, maxResults);
                }

                return true;
            }

            return false;
        }

        private void TryExplainSingleFileCopy(FileData fileData, IList<SearchResult> resultSet)
        {
            var fileCopyInfo = fileData.Incoming.FirstOrDefault() ?? fileData.Outgoing.FirstOrDefault();
            var project = fileCopyInfo.Project;

            var filePath = fileData.FilePath;
            if (fileData.Incoming.Count == 1)
            {
                filePath = fileCopyInfo.FileCopyOperation.Source;
            }

            var fileName = Path.GetFileName(filePath);

            var build = project.GetRoot() as Build;
            if (build == null)
            {
                return;
            }

            var evaluation = build.FindEvaluation(project.EvaluationId);
            if (evaluation == null)
            {
                return;
            }

            var itemsFolder = evaluation.FindChild<NamedNode>("Items");
            if (itemsFolder == null)
            {
                return;
            }

            FindCopyToOutputDirectoryItem(resultSet, itemsFolder, fileName, "None");
            FindCopyToOutputDirectoryItem(resultSet, itemsFolder, fileName, "Content");
        }

        private static void FindCopyToOutputDirectoryItem(IList<SearchResult> resultSet, NamedNode itemsFolder, string fileName, string itemName)
        {
            var addItem = itemsFolder.FindChild<AddItem>(itemName);
            if (addItem != null)
            {
                foreach (var item in addItem.Children.OfType<Item>())
                {
                    string name = Path.GetFileName(item.Name);
                    if (fileName.Equals(name, StringComparison.OrdinalIgnoreCase))
                    {
                        if (item.FindChild<Metadata>("CopyToOutputDirectory") is { } metadata && (metadata.Value == "Always" || metadata.Value == "PreserveNewest"))
                        {
                            resultSet.Add(new SearchResult(item));
                        }
                    }
                }
            }
        }

        private void TryGetFiles(string text, IList<SearchResult> resultSet, NodeQueryMatcher matcher, int maxResults)
        {
            var results = new List<SearchResult>();

            lock (directories)
            {
                foreach (var kvp in directories)
                {
                    var directoryData = kvp.Value;
                    foreach (var file in directoryData.Files)
                    {
                        if (file.FilePath.IndexOf(text, StringComparison.OrdinalIgnoreCase) != -1)
                        {
                            if (!FileMatches(file, matcher))
                            {
                                continue;
                            }

                            var item = new Item { Name = file.FilePath };
                            var result = new SearchResult(item);
                            result.AddMatch(file.FilePath, text);
                            results.Add(result);
                            if (results.Count >= maxResults)
                            {
                                break;
                            }
                        }
                    }

                    if (results.Count >= maxResults)
                    {
                        break;
                    }
                }
            }

            results.Sort((l, r) => l.Node.Title.CompareTo(r.Node.Title));

            foreach (var result in results)
            {
                resultSet.Add(result);
            }
        }

        private bool FileMatches(FileData file, NodeQueryMatcher matcher)
        {
            foreach (var fileCopyInfo in file.Incoming.Concat(file.Outgoing))
            {
                if (FileCopyMatches(fileCopyInfo, matcher))
                {
                    return true;
                }
            }

            return false;
        }

        private bool FileCopyMatches(FileCopyInfo fileCopyInfo, NodeQueryMatcher matcher)
        {
            if (fileCopyInfo.Project is not { } project)
            {
                return true;
            }

            var projectMatchers = matcher.ProjectMatchers;
            if (projectMatchers.Count == 0)
            {
                return true;
            }

            foreach (var includeMatcher in projectMatchers)
            {
                var matchResult = includeMatcher.IsMatch(project.Name, project.SourceFilePath);
                if (matchResult != null)
                {
                    return true;
                }
            }

            return false;
        }

        private void GetResults(FileData fileData, IList<SearchResult> resultSet, NodeQueryMatcher matcher, int maxResults, string matchText = null)
        {
            if (matchText == null)
            {
                matchText = fileData.FilePath;
            }

            foreach (var incoming in fileData.Incoming)
            {
                if (!FileCopyMatches(incoming, matcher))
                {
                    continue;
                }

                var message = incoming.FileCopyOperation.Message;
                var result = new SearchResult(message);
                result.AddMatch(message.Text, matchText);
                result.RootFolder = "Incoming";
                resultSet.Add(result);
                if (resultSet.Count >= maxResults)
                {
                    return;
                }
            }

            foreach (var outgoing in fileData.Outgoing)
            {
                if (!FileCopyMatches(outgoing, matcher))
                {
                    continue;
                }

                var message = outgoing.FileCopyOperation.Message;
                var result = new SearchResult(message);
                result.AddMatch(message.Text, matchText);
                result.RootFolder = "Outgoing";
                resultSet.Add(result);
                if (resultSet.Count >= maxResults)
                {
                    return;
                }
            }
        }

        private void GetResults(DirectoryData directoryData, IList<SearchResult> resultSet, NodeQueryMatcher matcher, int maxResults)
        {
            string directoryPath = directoryData.ToString();

            foreach (var fileData in directoryData.Files)
            {
                if (!FileMatches(fileData, matcher))
                {
                    continue;
                }

                GetResults(fileData, resultSet, matcher, maxResults, matchText: directoryPath);
                if (resultSet.Count >= maxResults)
                {
                    return;
                }
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

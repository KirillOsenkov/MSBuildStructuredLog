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

        private void AnalyzeCopyOperation(FileCopyOperation copyOperation, Task task = null, Target target = null)
        {
            var source = copyOperation.Source;
            var destination = copyOperation.Destination;

            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(destination))
            {
                return;
            }

            target ??= task.GetNearestParent<Target>();

            var project = target.GetNearestParent<Project>();

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
                Target = target,
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
                if (matcher.ProjectMatchers.Count > 0)
                {
                    TryGetFiles(text: null, resultSet, matcher, maxResults);
                    return true;
                }

                resultSet.Add(new SearchResult(new Note { Text = "Specify a directory or file path, a partial file name or a project() clause." }));

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

        private void TryExplainSingleFileCopy(Project project, string filePath, IList<SearchResult> resultSet)
        {
            var fileName = Path.GetFileName(filePath);

            var target = project.FindTarget("_GetCopyToOutputDirectoryItemsFromTransitiveProjectReferences");
            if (target != null)
            {
                var task = target.FindChild<MSBuildTask>();
                if (task != null)
                {
                    var outputItems = task.FindLastChild<Folder>(static f => f.Name == "OutputItems");
                    if (outputItems != null)
                    {
                        var addItem = outputItems.FindChild<AddItem>();
                        if (addItem != null)
                        {
                            var item = addItem.FindChild<Item>(filePath);
                            if (item != null)
                            {
                                var metadata = item.FindChild<Metadata>(static m => m.Name == "MSBuildSourceProjectFile");
                                if (metadata != null)
                                {
                                    var metadataValue = metadata.Value;
                                    resultSet.Add(new SearchResult(metadata));

                                    var referencedProject = task.FindChild<Project, string>(static (p, metadataValue) => p.Name.Equals(Path.GetFileName(metadataValue), StringComparison.OrdinalIgnoreCase), metadataValue);
                                    if (referencedProject != null)
                                    {
                                        var getCopyToOutputDirectoryItems = referencedProject.FindTarget("GetCopyToOutputDirectoryItems");
                                        if (getCopyToOutputDirectoryItems != null)
                                        {
                                            if (getCopyToOutputDirectoryItems.OriginalNode is Target original)
                                            {
                                                getCopyToOutputDirectoryItems = original;
                                                referencedProject = getCopyToOutputDirectoryItems.Project;
                                            }

                                            TryExplainSingleFileCopy(referencedProject, filePath, resultSet);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            target = project.FindTarget("ResolveAssemblyReferences");
            if (target != null)
            {
                var task = target.FindChild<ResolveAssemblyReferenceTask>();
                if (task != null)
                {
                    var outputItems = task.FindLastChild<Folder>(static f => f.Name == "OutputItems");
                    if (outputItems != null)
                    {
                        var addItem = outputItems.FindChild<AddItem>(static a => a.Name == "ReferenceCopyLocalPaths");
                        if (addItem != null)
                        {
                            var item = addItem.FindChild<Item>(filePath);
                            if (item != null)
                            {
                                var metadata = item.FindChild<Metadata>(static m => m.Name == "MSBuildSourceProjectFile");
                                if (metadata != null)
                                {
                                    var metadataValue = metadata.Value;
                                    resultSet.Add(new SearchResult(metadata));
                                }
                            }
                        }
                    }
                }
            }

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
            FindCopyToOutputDirectoryItem(resultSet, itemsFolder, fileName, "Compile");
            FindCopyToOutputDirectoryItem(resultSet, itemsFolder, fileName, "_CompileItemsToCopy");
            FindCopyToOutputDirectoryItem(resultSet, itemsFolder, fileName, "Content");
            FindCopyToOutputDirectoryItem(resultSet, itemsFolder, fileName, "EmbeddedResource");
        }

        private void TryExplainSingleFileCopy(FileData fileData, IList<SearchResult> resultSet)
        {
            var singleResult = resultSet.FirstOrDefault();

            var fileCopyInfo =
                singleResult.AssociatedFileCopy ??
                fileData.Incoming.FirstOrDefault() ??
                fileData.Outgoing.FirstOrDefault();

            var project = fileCopyInfo.Project;

            var sourceFilePath = fileData.FilePath;
            if (fileData.Incoming.Count > 0 &&
                fileData.Incoming
                    .Select(i => i.FileCopyOperation.Source)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count() == 1)
            {
                sourceFilePath = fileCopyInfo.FileCopyOperation.Source;
            }

            TryExplainSingleFileCopy(project, sourceFilePath, resultSet);
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
                        if (text == null || file.FilePath.IndexOf(text, StringComparison.OrdinalIgnoreCase) != -1)
                        {
                            if (!FileMatches(file, matcher))
                            {
                                continue;
                            }

                            string kind = null;
                            bool hasIncoming = file.Incoming.Any();
                            bool hasOutgoing = file.Outgoing.Any();
                            if (hasIncoming && hasOutgoing)
                            {
                                kind = "SourceAndDestination";
                            }
                            else if (hasIncoming)
                            {
                                kind = "Destination";
                            }
                            else if (hasOutgoing)
                            {
                                kind = "Source";
                            }

                            var item = new FileCopy { Name = file.FilePath, Kind = kind };
                            var result = new SearchResult(item);
                            if (text != null)
                            {
                                result.AddMatch(file.FilePath, text);
                            }

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

                var node = incoming.FileCopyOperation.Node;
                var result = new SearchResult(node);
                result.AssociatedFileCopy = incoming;
                result.AddMatch(node.Title, matchText);
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

                var node = outgoing.FileCopyOperation.Node;
                var result = new SearchResult(node);
                result.AssociatedFileCopy = outgoing;
                result.AddMatch(node.Title, matchText);
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

        public void AnalyzeTarget(Target target)
        {
            if (target.Name == "_CopyOutOfDateSourceItemsToOutputDirectory" && target.Skipped)
            {
                if (target.FindChild<Folder>(Strings.Inputs) is Folder inputFolder &&
                    target.FindChild<Folder>(Strings.Outputs) is Folder outputFolder)
                {
                    var inputs = inputFolder
                        .Children
                        .OfType<Item>()
                        .Select(i => i.Text)
                        .Where(s => s != null)
                        .OrderBy(s => Path.GetFileName(s), StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    var outputs = outputFolder
                        .Children
                        .OfType<Item>()
                        .Select(i => i.Text)
                        .Where(s => s != null)
                        .OrderBy(s => Path.GetFileName(s), StringComparer.OrdinalIgnoreCase)
                        .ToArray();

                    int imbalance = outputs.Length - inputs.Length;

                    // There's no accurate way to build a correlation between sources and destinations,
                    // so the crude algorithm below is just a heuristic that works in the common case.
                    // There can be more outputs than inputs and it's not clear which input goes into
                    // which output. We try to recover from some simple cases, such as 12 inputs going
                    // into 13 outputs, one input going into two places. But we can't recover from
                    // more difficult situations, but that's OK, we'll just under report some
                    // ephemeral copies (that didn't happen anyway because we're in an incremental
                    // build and the target got skipped anyway).
                    for (int i = 0, j = 0; i < inputs.Length && j < outputs.Length; i++, j++)
                    {
                        var input = inputs[i];
                        var output = outputs[j];
                        var inputName = Path.GetFileName(input);
                        var outputName = Path.GetFileName(output);
                        if (string.Equals(inputName, outputName, StringComparison.OrdinalIgnoreCase))
                        {
                            ReportCopy(target, input, output);
                        }
                        else
                        {
                            // we dyssynchronized, we can no longer accurately correlate inputs to outputs, so just keep going in the hope that we'll resync later
                            continue;
                        }

                        if (imbalance < 0 && i < inputs.Length - 1 && string.Equals(inputName, Path.GetFileName(inputs[i + 1]), StringComparison.OrdinalIgnoreCase))
                        {
                            j--;
                            imbalance++;
                            continue;
                        }

                        if (imbalance > 0 && j < outputs.Length - 1 && string.Equals(outputName, Path.GetFileName(outputs[j + 1]), StringComparison.OrdinalIgnoreCase))
                        {
                            i--;
                            imbalance--;
                            continue;
                        }
                    }
                }
            }
        }

        private void ReportCopy(Target target, string input, string output)
        {
            TreeNode node = target;

            var inputs = target.FindChild<Folder>(Strings.Inputs);
            if (inputs != null)
            {
                var item = inputs.FindChild<Item>(i => i.Text == input);
                if (item != null)
                {
                    node = item;
                }
            }

            if (input != null && output != null)
            {
                var operation = new FileCopyOperation
                {
                    Source = input,
                    Destination = output,
                    Copied = false,
                    Node = node
                };
                AnalyzeCopyOperation(operation, target: target);
            }
        }
    }
}

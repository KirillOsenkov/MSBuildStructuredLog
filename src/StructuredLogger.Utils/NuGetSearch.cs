using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NuGet.ProjectModel;
using StructuredLogViewer;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class AssetsFile
    {
        public string AssetsFilePath { get; set; }
        public string ProjectFilePath { get; set; }
        public string Text { get; set; }

        private LockFile lockFile;
        public LockFile LockFile
        {
            get
            {
                lock (this)
                {
                    lockFile ??= new LockFileFormat().Parse(Text, AssetsFilePath);
                }

                return lockFile;
            }
        }
    }

    public class NuGetSearch : ISearchExtension
    {
        public Build Build { get; }

        private List<AssetsFile> assetsFiles;

        public NuGetSearch(Build build)
        {
            Build = build;
        }

        public bool TryGetResults(NodeQueryMatcher matcher, IList<SearchResult> resultCollector, int maxResults)
        {
            if (!string.Equals(matcher.TypeKeyword, "nuget", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var underProjectMatcher = matcher.IncludeMatchers.FirstOrDefault(m => m.UnderProject);
            if (underProjectMatcher == null || underProjectMatcher.Terms.Count == 0)
            {
                resultCollector.Add(new SearchResult(new Error { Text = "Add a 'project(...)' clause to filter which project(s) to search." }));
                resultCollector.Add(new SearchResult(new Note { Text = "Specify '$nuget project(.csproj)' to search all projects (expensive)." }));
                return true;
            }

            PopulateAssetsFiles();

            var files = FindAssetsFiles(underProjectMatcher, maxResults);
            if (files.Count == 0)
            {
                resultCollector.Add(new SearchResult(new Error { Text = "No matching project.assets.json files found" }));
                return true;
            }

            foreach (var file in files)
            {
                var project = new Project
                {
                    ProjectFile = file.ProjectFilePath,
                    Name = Path.GetFileName(file.ProjectFilePath),
                    IsExpanded = true
                };

                PopulateProject(project, matcher, file);

                if (project.HasChildren)
                {
                    resultCollector.Add(new SearchResult(project));
                }
            }

            return true;
        }

        private void PopulateProject(Project project, NodeQueryMatcher matcher, AssetsFile file)
        {
            var lockFile = file.LockFile;

            bool expand = matcher.Terms.Count > 0;
            bool addedAnything = false;

            foreach (var framework in lockFile.ProjectFileDependencyGroups)
            {
                var target = lockFile.Targets.FirstOrDefault(t => t.Name == framework.FrameworkName);
                if (target == null)
                {
                    continue;
                }

                var libraries = target.Libraries.ToDictionary(l => l.Name);

                var frameworkNode = new Folder
                {
                    Name = framework.FrameworkName,
                    IsExpanded = true
                };

                HashSet<string> topLevel = new(framework.Dependencies.Select(d => ParsePackageId(d).name));

                var topLevelLibraries = new List<LockFileTargetLibrary>();

                foreach (var name in topLevel)
                {
                    if (!libraries.TryGetValue(name, out var topLibrary))
                    {
                        continue;
                    }

                    topLevelLibraries.Add(topLibrary);
                }

                foreach (var topLibrary in topLevelLibraries.OrderByDescending(l => l.Type))
                {
                    SearchResult match = matcher.IsMatch(topLibrary.Name, topLibrary.Version.ToString());
                    TreeNode topLevelNode = CreateNode(lockFile, topLibrary, expand, match);

                    bool added = AddDependencies(lockFile, topLibrary.Name, topLevelNode, libraries, topLevel, matcher);
                    if (match != null || added)
                    {
                        frameworkNode.AddChild(topLevelNode);
                        addedAnything = true;
                    }
                }

                if (addedAnything)
                {
                    project.AddChild(frameworkNode);
                }
            }

            if (!addedAnything)
            {
                return;
            }

            PopulatePackageFolders(project, lockFile);
            PopulateLogs(project, lockFile);
        }

        private void PopulateLogs(Project project, LockFile lockFile)
        {
            foreach (var logMessage in lockFile.LogMessages)
            {
                string text = logMessage.Message;

                TextNode node;
                if (logMessage.Level == NuGet.Common.LogLevel.Error)
                {
                    node = new Error
                    {
                        Code = logMessage.Code.ToString()
                    };
                }
                else if (logMessage.Level == NuGet.Common.LogLevel.Warning)
                {
                    node = new Warning
                    {
                        Code = logMessage.Code.ToString()
                    };
                }
                else
                {
                    node = new MessageWithLocation();
                    text = $"{logMessage.Code}: {text}";
                }

                node.Text = text;

                project.AddChild(node);
            }
        }

        private static void PopulatePackageFolders(Project project, LockFile lockFile)
        {
            var packageFoldersNode = new Folder { Name = "PackageFolders" };
            foreach (var packageFolder in lockFile.PackageFolders)
            {
                var item = new Item { Name = packageFolder.Path };
                packageFoldersNode.AddChild(item);
            }

            if (packageFoldersNode.HasChildren)
            {
                project.AddChild(packageFoldersNode);
            }
        }

        private TreeNode CreateNode(LockFile lockFile, LockFileTargetLibrary library, bool expand, SearchResult match)
        {
            string name = library.Name;
            TreeNode node;
            if (library.Type == "project")
            {
                var libraryInfo = lockFile.GetLibrary(library.Name, library.Version);
                node = new Project
                {
                    Name = Path.GetFileName(libraryInfo.MSBuildProject),
                    ProjectFile = libraryInfo.MSBuildProject,
                    IsExpanded = expand
                };
            }
            else
            {
                node = new Package
                {
                    Name = name,
                    Version = library.Version.ToString(),
                    IsExpanded = expand
                };
            }

            if (match != null && match != SearchResult.EmptyQueryMatch)
            {
                if (node is Package package)
                {
                    match.FieldsToDisplay = new List<string>
                    {
                        package.Name,
                        package.Version
                    };
                }

                var proxy = new ProxyNode();
                proxy.Original = node;
                proxy.Populate(match);
                proxy.Text = node.ToString();
                proxy.IsExpanded = expand;

                var children = node.Children.ToArray();
                node.Children.Clear();
                foreach (var child in children)
                {
                    proxy.AddChild(child);
                }

                node = proxy;
            }

            return node;
        }

        private (string name, string version) ParsePackageId(string dependency)
        {
            return dependency.GetFirstAndRest(' ');
        }

        private bool AddDependencies(
            LockFile lockFile,
            string id,
            TreeNode dependencyNode,
            Dictionary<string, LockFileTargetLibrary> libraries,
            HashSet<string> topLevel,
            NodeQueryMatcher matcher)
        {
            if (!libraries.TryGetValue(id, out var library))
            {
                return false;
            }

            bool result = false;
            bool expand = matcher.Terms.Count > 0;

            foreach (var dependency in library.Dependencies)
            {
                if (!libraries.TryGetValue(dependency.Id, out var dependencyLibrary))
                {
                    continue;
                }

                SearchResult match = matcher.IsMatch(dependencyLibrary.Name, dependencyLibrary.Version.ToString());
                var node = CreateNode(lockFile, dependencyLibrary, expand, match);

                bool added = false;
                if (!topLevel.Contains(dependency.Id))
                {
                    added = AddDependencies(lockFile, dependency.Id, node, libraries, topLevel, matcher);
                }

                if (match != null || added)
                {
                    dependencyNode.AddChild(node);
                    result = true;
                }
            }

            return result;
        }

        private IReadOnlyList<AssetsFile> FindAssetsFiles(NodeQueryMatcher underProjectMatcher, int maxResults)
        {
            var files = new List<AssetsFile>();
            foreach (var assetFile in assetsFiles)
            {
                foreach (var term in underProjectMatcher.Terms)
                {
                    if (term.IsMatch(assetFile.ProjectFilePath) ||
                        term.IsMatch(assetFile.AssetsFilePath))
                    {
                        files.Add(assetFile);
                        if (files.Count >= maxResults)
                        {
                            return files;
                        }
                    }
                }
            }

            return files;
        }

        private Regex projectFilePathRegex = new Regex(@"\""projectPath\""\: \""(?<Path>[^\""]+)\"",", RegexOptions.Compiled);

        private void PopulateAssetsFiles()
        {
            if (assetsFiles != null)
            {
                return;
            }

            assetsFiles = new List<AssetsFile>();

            if (Build.SourceFiles == null || Build.SourceFiles.Count == 0)
            {
                return;
            }

            foreach (var file in Build.SourceFiles)
            {
                if (!file.FullPath.EndsWith("project.assets.json", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var assetFile = new AssetsFile
                {
                    AssetsFilePath = file.FullPath,
                    Text = file.Text
                };

                var match = projectFilePathRegex.Match(file.Text);
                if (!match.Success)
                {
                    continue;
                }

                string projectFilePath = match.Groups["Path"].Value;
                if (string.IsNullOrEmpty(projectFilePath))
                {
                    continue;
                }

                projectFilePath = projectFilePath.Replace(@"\\", @"\");

                assetFile.ProjectFilePath = projectFilePath;

                assetsFiles.Add(assetFile);
            }
        }
    }
}
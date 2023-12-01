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
        public LockFile LockFile { get; set; }
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
                resultCollector.Add(new SearchResult(new Error { Text = "Add a 'project(...)' clause to filter which project(s) to search" }));
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
                    IsExpanded = matcher.Terms.Count > 0
                };
                resultCollector.Add(new SearchResult(project));

                PopulateProject(project, matcher, file);
            }

            return true;
        }

        private void PopulateProject(Project project, NodeQueryMatcher matcher, AssetsFile file)
        {
            var lockFile = file.LockFile ??= new LockFileFormat().Parse(file.Text, file.AssetsFilePath);

            var dependencies = new Dictionary<string, List<string>>();

            foreach (var target in lockFile.Targets)
            {
                foreach (var package in target.Libraries)
                {
                    if (package.Dependencies.Count == 0)
                    {
                        continue;
                    }

                    var list = new List<string>();
                    dependencies[$"{package.Name}"] = list;
                    foreach (var dependency in package.Dependencies)
                    {
                        list.Add(dependency.Id);
                    }
                }
            }

            bool expand = matcher.Terms.Count > 0;
            bool addedAnything = false;

            foreach (var framework in lockFile.ProjectFileDependencyGroups)
            {
                var frameworkNode = new Folder
                {
                    Name = framework.FrameworkName,
                    IsExpanded = expand
                };

                HashSet<string> topLevel = new(framework.Dependencies.Select(d => ParsePackageId(d).name));

                foreach (var dependency in framework.Dependencies)
                {
                    var (name, version) = ParsePackageId(dependency);
                    var dependencyNode = new Package { Name = name, Version = version, IsExpanded = expand };

                    bool added = AddDependencies(dependencyNode, dependencies, topLevel, matcher);
                    if (matcher.IsMatch(name) || added)
                    {
                        frameworkNode.AddChild(dependencyNode);
                        addedAnything = true;
                    }
                }

                if (addedAnything)
                {
                    project.AddChild(frameworkNode);
                }
            }
        }

        private (string name, string version) ParsePackageId(string dependency)
        {
            return dependency.GetFirstAndRest(' ');
        }

        private bool AddDependencies(
            Package dependencyNode,
            Dictionary<string, List<string>> dependencies,
            HashSet<string> topLevel,
            NodeQueryMatcher matcher)
        {
            if (!dependencies.TryGetValue(dependencyNode.Name, out var list))
            {
                return false;
            }

            bool result = false;
            bool expand = matcher.Terms.Count > 0;

            foreach (var dependency in list)
            {
                var node = new Package { Name = dependency, IsExpanded = expand };

                bool added = false;
                if (!topLevel.Contains(dependency))
                {
                    added = AddDependencies(node, dependencies, topLevel, matcher);
                }

                if (matcher.IsMatch(dependency) || added)
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
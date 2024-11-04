using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using StructuredLogViewer;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class ProjectReferenceGraph : ISearchExtension
    {
        private Dictionary<string, ICollection<string>> references = new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, int> projectHeights = new(StringComparer.OrdinalIgnoreCase);
        private List<IReadOnlyList<string>> circularities = new();
        private int maxProjectHeight;

        public ProjectReferenceGraph(Build build)
        {
            var evaluations = build.EvaluationFolder.Children.OfType<ProjectEvaluation>();
            foreach (var evaluation in evaluations)
            {
                string projectFile = evaluation.ProjectFile;
                string projectDirectory = Path.GetDirectoryName(projectFile);

                var items = evaluation.FindChild<Folder>(Strings.Items);
                if (items == null)
                {
                    continue;
                }

                if (!references.TryGetValue(projectFile, out var bucket))
                {
                    bucket = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    references[projectFile] = bucket;
                }

                var projectReferences = items.FindChild<AddItem>("ProjectReference");
                if (projectReferences == null)
                {
                    continue;
                }

                foreach (var projectReference in projectReferences.Children.OfType<Item>())
                {
                    var path = projectReference.Text;
                    var referencePath = Path.Combine(projectDirectory, path);
                    referencePath = TextUtilities.NormalizeFilePath(referencePath);

                    bucket.Add(referencePath);
                }
            }

            foreach (var kvp in references.ToArray())
            {
                var project = kvp.Key;
                references[project] = kvp.Value.OrderBy(s => s).ToArray();
                CalculateHeight(project, new List<string>());
            }

            if (projectHeights.Any())
            {
                maxProjectHeight = projectHeights.Values.Max();
            }

            if (circularities.Any())
            {
                var folder = build.GetOrCreateNodeWithName<Folder>("Circular Project References");
                foreach (var loop in circularities)
                {
                    var loopFolder = folder.GetOrCreateNodeWithName<Folder>(loop[0]);
                    foreach (var item in loop)
                    {
                        var node = new Item { Text = item };
                        loopFolder.AddChild(node);
                    }
                }
            }
        }

        private int CalculateHeight(string project, List<string> chain)
        {
            if (!projectHeights.TryGetValue(project, out int height))
            {
                if (references.TryGetValue(project, out var projectReferences))
                {
                    int index = chain.IndexOf(project);
                    if (index == -1)
                    {
                        chain.Add(project);
                        foreach (var reference in projectReferences)
                        {
                            int referenceHeight = CalculateHeight(reference, chain) + 1;
                            if (referenceHeight > height)
                            {
                                height = referenceHeight;
                            }
                        }

                        chain.Remove(project);
                    }
                    else
                    {
                        var loop = chain.Skip(index).ToArray();
                        circularities.Add(loop);
                    }
                }

                projectHeights[project] = height;
            }

            return height;
        }

        public bool TryGetResults(NodeQueryMatcher matcher, IList<SearchResult> resultSet, int maxResults)
        {
            bool isProjectHeightSearch =
                matcher.Height != -1 &&
                string.Equals(matcher.TypeKeyword, "project", StringComparison.OrdinalIgnoreCase);
            if (isProjectHeightSearch)
            {
                GetProjectHeightResults(matcher, resultSet, maxResults);
                return true;
            }

            if (!string.Equals(matcher.TypeKeyword, "projectreference", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (matcher.ProjectMatchers.Count != 1)
            {
                resultSet.Add(new SearchResult(new Note { Text = "Specify a project() clause to search the project reference graph of the matching project(s)" }));

                return false;
            }

            var visitedProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var projectMatcher = matcher.ProjectMatchers.FirstOrDefault();

            TreeNode PopulateReferences(string path)
            {
                var project = CreateProject(path);
                string name = project.Name;
                TreeNode node = project;

                bool hasTerms = matcher.Terms.Count > 0;
                if (hasTerms)
                {
                    var match = matcher.IsMatch(name);
                    if (match is SearchResult result && result != SearchResult.EmptyQueryMatch)
                    {
                        result.FieldsToDisplay = [name];

                        var proxy = new ProxyNode();
                        proxy.Original = project;
                        proxy.SearchResult = match;
                        proxy.Text = name;

                        node = proxy;
                    }
                }

                if (visitedProjects.Add(path) && references.TryGetValue(path, out var bucket))
                {
                    foreach (var referencedProjectPath in bucket)
                    {
                        var referencedProject = PopulateReferences(referencedProjectPath);
                        if (referencedProject != null)
                        {
                            node.AddChild(referencedProject);
                            if (referencedProject.IsExpanded || referencedProject is ProxyNode)
                            {
                                node.IsExpanded = true;
                            }
                        }
                    }
                }

                if (hasTerms && !node.HasChildren && node is not ProxyNode)
                {
                    return null;
                }

                return node;
            }

            foreach (var project in references.Keys)
            {
                var match = projectMatcher.IsMatch(project);
                if (match == null || match == SearchResult.EmptyQueryMatch)
                {
                    continue;
                }

                var projectResult = PopulateReferences(project);
                if (projectResult != null)
                {
                    projectResult.IsExpanded = true;
                }
                else
                {
                    projectResult = CreateProject(project);
                }

                var result = new SearchResult(projectResult);
                resultSet.Add(result);
            }

            if (resultSet.Count == 1)
            {
                var singleProject = resultSet[0].Node switch
                {
                    Project p => p.ProjectFile,
                    ProxyNode proxy => (proxy.Original as Project).ProjectFile,
                    _ => null
                };

                List<string> referencing = new();

                foreach (var project in references.Keys)
                {
                    if (references.TryGetValue(project, out var bucket))
                    {
                        if (bucket.Contains(singleProject))
                        {
                            referencing.Add(project);
                        }
                    }
                }

                if (referencing.Count > 0)
                {
                    var folder = new Folder() { Name = "Referencing projects" };

                    foreach (var referencingProject in referencing.OrderBy(s => s))
                    {
                        var node = CreateProject(referencingProject);
                        folder.AddChild(node);
                    }

                    if (folder.Children.Count < 10)
                    {
                        folder.IsExpanded = true;
                    }

                    resultSet.Add(new SearchResult(folder));
                }
            }

            return true;
        }

        private void GetProjectHeightResults(NodeQueryMatcher matcher, IList<SearchResult> resultSet, int maxResults)
        {
            int height = matcher.Height;
            if (height == int.MaxValue)
            {
                height = maxProjectHeight;
                resultSet.Add(new SearchResult(new Note { Text = $"Max = {maxProjectHeight}" }));
            }

            foreach (var p in references.Keys)
            {
                if (projectHeights.TryGetValue(p, out int thisProjectHeight))
                {
                    if (thisProjectHeight == height)
                    {
                        var projectWithHeight = CreateProject(p);
                        var searchResult = new SearchResult(projectWithHeight);
                        resultSet.Add(searchResult);
                        if (resultSet.Count >= maxResults)
                        {
                            return;
                        }
                    }
                }
            }
        }

        private bool GetProjectsOfHeight(int height)
        {
            throw new NotImplementedException();
        }

        private Project CreateProject(string project)
        {
            return new Project()
            {
                ProjectFile = project,
                Name = Path.GetFileNameWithoutExtension(project)
            };
        }
    }
}
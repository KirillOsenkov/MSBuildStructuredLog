using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using StructuredLogViewer;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class ProjectReferenceGraph : ISearchExtension
    {
        private int maxProjectHeight;

        public bool AugmentOtherResults => false;

        public Digraph Graph = new Digraph();

        public ProjectReferenceGraph(Build build)
        {
            Graph = new Digraph();

            var evaluations = build.EvaluationFolder.Children.OfType<ProjectEvaluation>();
            foreach (var evaluation in evaluations)
            {
                string projectFile = evaluation.ProjectFile;
                if (projectFile.EndsWith("_wpftmp.csproj", StringComparison.OrdinalIgnoreCase) ||
                    projectFile.EndsWith(".sln.metaproj", StringComparison.OrdinalIgnoreCase) ||
                    projectFile.EndsWith("\\dirs.proj", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var items = evaluation.FindChild<Folder>(Strings.Items);
                if (items == null)
                {
                    continue;
                }

                var node = GetNode(projectFile);

                var projectReferences = items.FindChild<AddItem>("ProjectReference");
                if (projectReferences == null)
                {
                    continue;
                }

                string projectDirectory = Path.GetDirectoryName(projectFile);
                foreach (var projectReference in projectReferences.Children.OfType<Item>())
                {
                    var path = projectReference.Text;
                    var referencePath = Path.Combine(projectDirectory, path);
                    referencePath = TextUtilities.NormalizeFilePath(referencePath);

                    var child = GetNode(referencePath);

                    node.AddChild(child);
                }
            }

            Vertex GetNode(string project)
            {
                return Graph.GetOrCreate(project, GetKey);
            }

            var cycles = Graph.RemoveCycles();
            Graph.CalculateHeight();
            Graph.CalculateDepth();

            maxProjectHeight = Graph.MaxHeight;

            if (cycles.Any())
            {
                var folder = build.GetOrCreateNodeWithName<Folder>("Circular Project References");
                foreach (var loop in cycles)
                {
                    var loopFolder = folder.GetOrCreateNodeWithName<Folder>(loop.First().Title);
                    foreach (var item in loop)
                    {
                        var node = new Item { Text = item.Value };
                        loopFolder.AddChild(node);
                    }
                }
            }
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

                if (visitedProjects.Add(path) && Graph.TryFindVertex(path) is Vertex vertex)
                {
                    if (vertex.Outgoing != null)
                    {
                        foreach (var referencedProjectPath in vertex.Outgoing)
                        {
                            var referencedProject = PopulateReferences(referencedProjectPath.Value);
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
                }

                if (hasTerms && !node.HasChildren && node is not ProxyNode)
                {
                    return null;
                }

                return node;
            }

            foreach (var vertex in Graph.Vertices)
            {
                var project = vertex.Value;
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

                var vertex = Graph.TryFindVertex(singleProject);
                if (vertex != null && vertex.Incoming != null)
                {
                    foreach (var incoming in vertex.Incoming)
                    {
                        referencing.Add(incoming.Value);
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

            foreach (var p in Graph.Vertices)
            {
                if (p.Height == height)
                {
                    var projectWithHeight = CreateProject(p.Value);
                    var searchResult = new SearchResult(projectWithHeight);
                    resultSet.Add(searchResult);
                    if (resultSet.Count >= maxResults)
                    {
                        return;
                    }
                }
            }
        }

        private Project CreateProject(string project)
        {
            return new Project()
            {
                ProjectFile = project,
                Name = Path.GetFileNameWithoutExtension(project)
            };
        }

        private Dictionary<string, string> keys = new(StringComparer.OrdinalIgnoreCase);

        private string GetKey(string filePath)
        {
            if (!keys.TryGetValue(filePath, out var key))
            {
                filePath = filePath.Replace("/", "\\");
                var parts = filePath.Split('\\');
                key = parts[parts.Length - 1];
                for (int i = parts.Length - 2; i >= 0; i--)
                {
                    var candidate = CleanupKey(key);
                    if (!keys.ContainsKey(candidate))
                    {
                        key = candidate;
                        break;
                    }

                    key = parts[i] + "\\" + key;
                    if (i == 0)
                    {
                        key = CleanupKey(key);
                    }
                }

                keys[filePath] = key;
                keys[key] = key;
            }

            return key;

            string CleanupKey(string key)
            {
                if (key.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                {
                    key = key.Substring(0, key.Length - ".csproj".Length);
                }

                if (key.Contains(".") || key.Contains("\\"))
                {
                    key = "\"" + key + "\"";
                }

                return key;
            }
        }
    }
}
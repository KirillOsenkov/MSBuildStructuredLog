using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using StructuredLogViewer;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class ProjectReferenceGraph : ISearchExtension
    {
        private Dictionary<string, ICollection<string>> references = new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, int> projectHeights = new(StringComparer.OrdinalIgnoreCase);
        private List<IReadOnlyList<string>> circularities = new();
        private int maxProjectHeight;

        public bool AugmentOtherResults => false;

        public Digraph Graph = new Digraph();

        public IEnumerable<Vertex> Vertices => Graph.Vertices;

        public ProjectReferenceGraph(Build build)
        {
            var evaluations = build.EvaluationFolder.Children.OfType<ProjectEvaluation>();
            foreach (var evaluation in evaluations)
            {
                string projectFile = evaluation.ProjectFile;
                if (projectFile.EndsWith("_wpftmp.csproj", StringComparison.OrdinalIgnoreCase) ||
                    projectFile.EndsWith(".sln.metaproj", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

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

            Graph = new Digraph();

            foreach (var kvp in references)
            {
                var node = GetNode(kvp.Key);
                foreach (var reference in kvp.Value)
                {
                    var childNode = GetNode(reference);
                    node.AddChild(childNode);
                }
            }

            Vertex GetNode(string project)
            {
                if (Graph.TryFindVertex(project) is not { } node)
                {
                    node = new Vertex { Value = project };
                    node.Key = GetKey(project);
                    Graph.Add(node);
                }

                return node;
            }

            var unreferenced = Graph.Sources.ToArray();

            var nodeList = new List<Vertex>();
            foreach (var key in unreferenced)
            {
                //RemoveTransitiveEdges(key, nodeList);
            }

            Graph.CalculateHeight();
            Graph.CalculateDepth();

            var list = new List<string>();
            foreach (var kvp in unreferenced)
            {
                CalculateHeight(kvp, list);
            }

            foreach (var kvp in references.ToArray())
            {
                references[kvp.Key] = kvp.Value.OrderBy(s => s).ToArray();
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

        private void RemoveTransitiveEdges(Vertex project, List<Vertex> chain)
        {
            if (project.IsProcessing)
            {
                return;
            }

            project.IsProcessing = true;

            for (int i = 0; i < chain.Count - 1; i++)
            {
                var parent = chain[i];
                if (parent.WasProcessed)
                {
                    break;
                }

                parent.Outgoing.Remove(project);
            }

            if (project.Outgoing != null && project.Outgoing.Count > 0)
            {
                chain.Add(project);

                foreach (var reference in project.Outgoing.ToArray())
                {
                    RemoveTransitiveEdges(reference, chain);
                }

                chain.RemoveAt(chain.Count - 1);
            }

            project.IsProcessing = false;
            project.WasProcessed = true;
        }

        private int CalculateHeight(Vertex vertex, List<string> chain)
        {
            string project = vertex.Value;
            if (!projectHeights.TryGetValue(project, out int height))
            {
                if (vertex.Outgoing != null)
                {
                    int index = chain.IndexOf(project);
                    if (index == -1)
                    {
                        chain.Add(project);
                        foreach (var reference in vertex.Outgoing)
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
                vertex.OutDegree = height;
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
                        if (bucket.Contains(singleProject, StringComparer.OrdinalIgnoreCase))
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
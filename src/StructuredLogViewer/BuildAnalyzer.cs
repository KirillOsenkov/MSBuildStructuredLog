using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class BuildAnalyzer
    {
        private Build build;

        public BuildAnalyzer(Build build)
        {
            this.build = build;
        }

        public static void AnalyzeBuild(Build build)
        {
            try
            {
                if (build.IsAnalyzed)
                {
                    Seal(build);
                    return;
                }

                var analyzer = new BuildAnalyzer(build);
                analyzer.Analyze();
                build.IsAnalyzed = true;

                Seal(build);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    "Error while analyzing build. Very sorry about that. Please Ctrl+C to copy this text and file an issue on https://github.com/KirillOsenkov/MSBuildStructuredLog \r\n" + ex.ToString());
            }
        }

        private static void Seal(Build build)
        {
            build.VisitAllChildren<TreeNode>(t => t.Seal());
        }

        private void Analyze()
        {
            Visit(build);

            if (!build.Succeeded)
            {
                build.AddChild(new Error { Text = "Build failed." });
            }
            else
            {
                build.AddChild(new Item { Text = "Build succeeded." });
            }

            AnalyzeDoubleWrites();
        }

        private void Visit(TreeNode node)
        {
            ProcessBeforeChildrenVisited(node);

            if (node.HasChildren)
            {
                foreach (var child in node.Children)
                {
                    var childNode = child as TreeNode;
                    if (childNode != null)
                    {
                        Visit(childNode);
                    }
                    else
                    {
                        ProcessTerminalNode(child);
                    }
                }
            }

            ProcessAfterChildrenVisited(node);
        }

        private void ProcessBeforeChildrenVisited(TreeNode node)
        {
            if (node is Task task)
            {
                AnalyzeTask(task);
            }
            else if (node is Target target)
            {
                AnalyzeTarget(target);
            }
            else if (node is Message message)
            {
                AnalyzeMessage(message);
            }
        }

        private void AnalyzeMessage(Message message)
        {
            if (message.Text != null && message.Text.StartsWith("Building with tools version"))
            {
                message.IsLowRelevance = true;
            }
        }

        private void ProcessAfterChildrenVisited(TreeNode node)
        {
            if (node is Project project)
            {
                PostAnalyzeProject(project);
            }
        }

        private void PostAnalyzeProject(Project project)
        {
            // if nothing in the project is important, mark the project as not important as well
            if (project.HasChildren)
            {
                bool allLowRelevance = true;
                foreach (var child in project.Children)
                {
                    if (child is IHasRelevance hasRelevance)
                    {
                        if (!hasRelevance.IsLowRelevance)
                        {
                            allLowRelevance = false;
                            break;
                        }
                    }
                    else
                    {
                        allLowRelevance = false;
                        break;
                    }
                }

                if (allLowRelevance)
                {
                    project.IsLowRelevance = true;
                }
            }
        }

        private void ProcessTerminalNode(object instance)
        {
        }

        private void AnalyzeTask(Task task)
        {
            if (!string.IsNullOrEmpty(task.CommandLineArguments))
            {
                task.AddChildAtBeginning(new Property { Name = "CommandLineArguments", Value = task.CommandLineArguments });
            }

            if (!string.IsNullOrEmpty(task.FromAssembly))
            {
                task.AddChildAtBeginning(new Property { Name = "Assembly", Value = task.FromAssembly });
            }

            if (task.Name == "ResolveAssemblyReference")
            {
                CopyLocalAnalyzer.AnalyzeResolveAssemblyReference(task);
            }

            if (task is CopyTask copyTask)
            {
                AnalyzeFileCopies(copyTask);
            }
        }

        private void AnalyzeDoubleWrites()
        {
            foreach (var bucket in fileCopySourcesForDestination)
            {
                if (IsDoubleWrite(bucket))
                {
                    var doubleWrites = build.GetOrCreateNodeWithName<Folder>("DoubleWrites");
                    var item = new Item { Text = bucket.Key };
                    doubleWrites.AddChild(item);
                    foreach (var source in bucket.Value)
                    {
                        item.AddChild(new Item { Text = source });
                    }
                }
            }
        }

        private static bool IsDoubleWrite(KeyValuePair<string, HashSet<string>> bucket)
        {
            if (bucket.Value.Count < 2)
            {
                return false;
            }

            if (bucket.Value
                .Select(f => new FileInfo(f))
                .Select(f => f.FullName)
                .Distinct()
                .Count() == 1)
            {
                return false;
            }

            return true;
        }

        private static readonly Dictionary<string, HashSet<string>> fileCopySourcesForDestination = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        private void AnalyzeFileCopies(CopyTask copyTask)
        {
            foreach (var copyOperation in copyTask.FileCopyOperations)
            {
                if (copyOperation.Copied)
                {
                    ProcessCopy(copyOperation.Source, copyOperation.Destination);
                }
            }
        }

        private static void ProcessCopy(string source, string destination)
        {
            HashSet<string> bucket = null;
            if (!fileCopySourcesForDestination.TryGetValue(destination, out bucket))
            {
                bucket = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                fileCopySourcesForDestination.Add(destination, bucket);
            }

            bucket.Add(source);
        }

        private void AnalyzeTarget(Target target)
        {
            MarkAsLowRelevanceIfNeeded(target);
            AddDependsOnTargets(target);
        }

        private static void AddDependsOnTargets(Target target)
        {
            if (!string.IsNullOrEmpty(target.DependsOnTargets))
            {
                var dependsOnTargets = new Parameter() { Name = "DependsOnTargets" };
                target.AddChildAtBeginning(dependsOnTargets);

                foreach (var dependsOnTarget in target.DependsOnTargets.Split(','))
                {
                    dependsOnTargets.AddChild(new Item { Text = dependsOnTarget });
                }
            }
        }

        private void MarkAsLowRelevanceIfNeeded(Target target)
        {
            if (!target.HasChildren || target.Children.All(c => c is Message))
            {
                target.IsLowRelevance = true;
                if (target.HasChildren)
                {
                    foreach (var child in target.Children.OfType<Message>())
                    {
                        child.IsLowRelevance = true;
                    }
                }
            }
        }

        public IEnumerable<Project> GetProjectsSortedTopologically(Build build)
        {
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var list = new List<Project>();

            if (build.HasChildren)
            {
                foreach (var project in build.Children.OfType<Project>())
                {
                    Visit(project, list, visited);
                }
            }

            return list;
        }

        private void Visit(Project project, List<Project> list, HashSet<string> visited)
        {
            if (visited.Add(project.ProjectFile))
            {
                if (project.HasChildren)
                {
                    foreach (var childProject in project.Children.OfType<Project>())
                    {
                        Visit(childProject, list, visited);
                    }
                }

                list.Add(project);
            }
        }
    }
}

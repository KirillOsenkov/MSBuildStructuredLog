﻿using StructuredLogViewer;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class BuildAnalyzer
    {
        private Build build;
        private DoubleWritesAnalyzer doubleWritesAnalyzer;

        public BuildAnalyzer(Build build)
        {
            this.build = build;
            doubleWritesAnalyzer = new DoubleWritesAnalyzer();
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
            }
            catch (Exception ex)
            {
                DialogService.ShowMessageBox(
                    "Error while analyzing build. Sorry about that. Please Ctrl+C to copy this text and file an issue on https://github.com/KirillOsenkov/MSBuildStructuredLog/issues/new \r\n" + ex.ToString());
            }
        }

        private static void Seal(Build build)
        {
            build.VisitAllChildren<TreeNode>(t => t.Seal());
        }

        private void Analyze()
        {
            Visit(build);
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
            node.Seal();
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
            else if (node is Build build)
            {
                PostAnalyzeBuild(build);
            }
        }

        private void PostAnalyzeBuild(Build build)
        {
            if (!build.Succeeded)
            {
                build.AddChild(new Error { Text = "Build failed." });
            }
            else
            {
                build.AddChild(new Item { Text = "Build succeeded." });
            }

            doubleWritesAnalyzer.AppendDoubleWritesFolder(build);
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
            else if (task is CopyTask copyTask)
            {
                doubleWritesAnalyzer.AnalyzeFileCopies(copyTask);
            }
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

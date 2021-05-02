﻿using System;
using System.Collections.Generic;
using System.Linq;
using StructuredLogViewer;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class BuildAnalyzer
    {
        private Build build;
        private DoubleWritesAnalyzer doubleWritesAnalyzer;
        private ResolveAssemblyReferenceAnalyzer resolveAssemblyReferenceAnalyzer;
        private int index;
        private Dictionary<string, TimeSpan> taskDurations = new Dictionary<string, TimeSpan>();
        private readonly List<Folder> analyzerReports = new List<Folder>();

        public BuildAnalyzer(Build build)
        {
            this.build = build;
            doubleWritesAnalyzer = new DoubleWritesAnalyzer();
            resolveAssemblyReferenceAnalyzer = new ResolveAssemblyReferenceAnalyzer();
        }

        public static void AnalyzeBuild(Build build)
        {
            try
            {
                if (build.IsAnalyzed)
                {
                    SealAndCalculateIndices(build);
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

        private static void SealAndCalculateIndices(Build build)
        {
            int index = 0;
            build.VisitAllChildren<TreeNode>(t =>
            {
                t.Seal();
                if (t is TimedNode timedNode)
                {
                    timedNode.Index = index;
                    index++;
                }
            });

            build.Statistics.TimedNodeCount = index;
        }

        private void Analyze()
        {
            Visit(build);
            build.Statistics.TimedNodeCount = index;
            foreach (var property in typeof(Strings)
                .GetProperties()
                .Where(p => p.PropertyType == typeof(string))
                .Select(p => p.GetValue(null) as string))
            {
                build.StringTable.Intern(property);
            }
        }

        private void Visit(TreeNode node)
        {
            ProcessBeforeChildrenVisited(node);

            if (node.HasChildren)
            {
                var children = node.Children;
                for (int i = 0; i < children.Count; i++)
                {
                    var child = children[i];
                    if (child is TreeNode childNode)
                    {
                        Visit(childNode);
                    }
                }
            }

            ProcessAfterChildrenVisited(node);
            node.Seal();
        }

        private void ProcessBeforeChildrenVisited(TreeNode node)
        {
            if (node is TimedNode timedNode)
            {
                timedNode.Index = index;
                index++;
            }

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
            else if (node is NamedNode folder)
            {
                if (folder.Name == Strings.Evaluation)
                {
                    folder.SortChildren();

                    AnalyzeEvaluation(folder);
                }
            }
        }

        private void AnalyzeEvaluation(NamedNode folder)
        {
            var evaluations = folder.Children.OfType<ProjectEvaluation>().ToArray();
            if (!evaluations.Any())
            {
                return;
            }

            var longestDuration = evaluations.Max(e => e.Duration.TotalMilliseconds);
            if (longestDuration == 0)
            {
                longestDuration = 1;
            }

            foreach (var projectEvaluation in evaluations)
            {
                var properties = projectEvaluation.FindChild<NamedNode>(Strings.PropertyReassignmentFolder);
                if (properties == null)
                {
                    continue;
                }

                properties.SortChildren();
                projectEvaluation.RelativeDuration = projectEvaluation.Duration.TotalMilliseconds * 100.0 / longestDuration;
            }
        }

        private void AnalyzeMessage(Message message)
        {
            if (message.Text != null && Strings.BuildingWithToolsVersionPrefix != null && Strings.BuildingWithToolsVersionPrefix.IsMatch(message.Text))
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
            string Intern(string text)
            {
#if DEBUG
                text = build.StringTable.Intern(text);
#endif
                return text;
            }

            if (!build.Succeeded)
            {
                build.AddChild(new Error { Text = Intern("Build failed.") });
            }
            else
            {
                build.AddChild(new Item { Text = Intern("Build succeeded.") });
            }

            build.AddChild(new Property { Name = Intern(Strings.Duration), Value = Intern(build.DurationText) });

            doubleWritesAnalyzer.AppendDoubleWritesFolder(build);
            resolveAssemblyReferenceAnalyzer.AppendFinalReport(build);

            if (build.LogFilePath != null)
            {
                build.AddChildAtBeginning(new Item { Text = Intern(build.LogFilePath) });
            }

            var durations = taskDurations
                .OrderByDescending(kvp => kvp.Value)
                .Where(kvp => // no need to include MSBuild and CallTarget tasks as they are not "terminal leaf" tasks
                    !string.Equals(kvp.Key, "MSBuild", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(kvp.Key, "CallTarget", StringComparison.OrdinalIgnoreCase))
                .Take(10)
                .ToArray();

            if (durations.Length > 0)
            {
                string folderName = $"Top {durations.Count()} most expensive tasks";
                folderName = Intern(folderName);
                var top10Tasks = build.GetOrCreateNodeWithName<Folder>(folderName);
                foreach (var kvp in durations)
                {
                    top10Tasks.AddChild(new Item
                    {
                        Name = Intern(kvp.Key),
                        Text = Intern(TextUtilities.DisplayDuration(kvp.Value))
                    });
                }
            }

            if (analyzerReports.Count > 0)
            {
                var analyzerReportSummary = build.GetOrCreateNodeWithName<Folder>(Intern($"Analyzer Summary"));
                CscTaskAnalyzer.CreateMergedReport(analyzerReportSummary, analyzerReports.ToArray());
            }
        }

        private void PostAnalyzeProject(Project project)
        {
            // if nothing in the project is important, mark the project as not important as well
            if (project.HasChildren)
            {
                bool allLowRelevance = true;

                var entryTargets = project.FindChild<Folder>(Strings.EntryTargets);
                if (entryTargets != null)
                {
                    if (entryTargets.Children.OfType<IHasRelevance>().All(c => c.IsLowRelevance))
                    {
                        entryTargets.IsLowRelevance = true;
                    }
                }

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

            if (string.IsNullOrEmpty(project.TargetFramework))
            {
                var evaluation = build.FindEvaluation(project.EvaluationId);
                if (evaluation != null)
                {
                    project.TargetFramework = evaluation.TargetFramework;
                    if (!string.IsNullOrEmpty(project.TargetFramework))
                    {
                        var text = $"Properties and items are available at evaluation id:{project.EvaluationId}. Use the hyperlink above or the new 'Properties and items' tab.";
#if DEBUG
                        text = build.StringTable.Intern(text);
#endif
                        project.AddChildAtBeginning(new Note
                        {
                            Text = text
                        });
                    }
                }
            }
        }

        private void AnalyzeTask(Task task)
        {
            if (!string.IsNullOrEmpty(task.CommandLineArguments))
            {
                task.AddChildAtBeginning(new Property { Name = Strings.CommandLineArguments, Value = task.CommandLineArguments });
            }

            if (!string.IsNullOrEmpty(task.FromAssembly))
            {
                task.AddChildAtBeginning(new Property { Name = Strings.Assembly, Value = task.FromAssembly });
                build.RegisterTask(task);
            }

            taskDurations.TryGetValue(task.Name, out var duration);
            duration += task.Duration;
            taskDurations[task.Name] = duration;

            if (task.Name == "ResolveAssemblyReference")
            {
                resolveAssemblyReferenceAnalyzer.AnalyzeResolveAssemblyReference(task);
            }
            else if (task.Name == "Message")
            {
                MessageTaskAnalyzer.Analyze(task);
            }
            else if (task.Name == "Csc")
            {
                var analyzerReport = CscTaskAnalyzer.Analyze(task);
                if (analyzerReport is not null)
                {
                    analyzerReports.Add(analyzerReport);
                }
            }

            doubleWritesAnalyzer.AnalyzeTask(task);
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

using System;
using System.Collections.Generic;
using System.Linq;
using StructuredLogViewer;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class BuildAnalyzer
    {
        private class NodeStatistic
        {
            public int Count = 0;
            public TimeSpan Duration;
        }

        private class TaskStatistic : NodeStatistic
        {
            public Dictionary<string, NodeStatistic> ChildNodes = new();
        }

        private readonly Build build;
        private readonly DoubleWritesAnalyzer doubleWritesAnalyzer;
        private readonly FileCopyMap fileCopyMap;
        private readonly ProjectReferenceGraph projectReferenceGraph;
        private readonly ResolveAssemblyReferenceAnalyzer resolveAssemblyReferenceAnalyzer;
        private readonly CppAnalyzer cppAnalyzer;
        private readonly Dictionary<string, TaskStatistic> taskDurations = new();
        private readonly List<Folder> analyzerReports = new List<Folder>();
        private readonly List<Folder> generatorReports = new List<Folder>();
        private int index;

        public BuildAnalyzer(Build build)
        {
            this.build = build;
            doubleWritesAnalyzer = new DoubleWritesAnalyzer();
            resolveAssemblyReferenceAnalyzer = new ResolveAssemblyReferenceAnalyzer();
            cppAnalyzer = new CppAnalyzer();
            fileCopyMap = new FileCopyMap();
            projectReferenceGraph = new ProjectReferenceGraph(build);
            build.FileCopyMap = fileCopyMap;
            build.ProjectReferenceGraph = projectReferenceGraph;
            build.SearchExtensions.Add(fileCopyMap);

            if (build.EvaluationFolder != null)
            {
                build.SearchExtensions.Add(projectReferenceGraph);
            }
        }

        public static void AnalyzeBuild(Build build)
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

        private static void SealAndCalculateIndices(Build build)
        {
            int index = 0;
            build.VisitAllChildren<TreeNode>(t =>
            {
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
            var evaluation = build.EvaluationFolder;
            if (evaluation != null)
            {
                evaluation.SortChildren();
                AnalyzeEvaluation(evaluation);
            }

            var environment = build.EnvironmentFolder;
            if (environment != null)
            {
                AnalyzeEnvironment(environment);
            }

            Visit(build);

            PostAnalyzeBuild(build);

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
            // items only have metadata, nothing to analyze.
            // they also constitute the majority of nodes
            if (node is AddItem or RemoveItem or Item)
            {
                return;
            }

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
        }

        private void ProcessBeforeChildrenVisited(TreeNode node)
        {
            if (node is not TimedNode timedNode)
            {
                return;
            }

            timedNode.Index = index;
            index++;

            if (node is Task task)
            {
                AnalyzeTask(task);
            }
            else if (node is Target target)
            {
                AnalyzeTarget(target);
            }
        }

        private void ProcessAfterChildrenVisited(TreeNode node)
        {
            if (node is Project project)
            {
                PostAnalyzeProject(project);
            }
        }

        private void AnalyzeEnvironment(NamedNode folder)
        {
            cppAnalyzer.AnalyzeEnvironment(folder);
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
                build.AddChild(new BuildError
                {
                    Text = Intern("Build failed.")
                });
            }
            else
            {
                build.AddChild(new Item { Text = Intern("Build succeeded.") });
            }

            build.AddChild(new Property { Name = Intern(Strings.Duration), Value = Intern(build.DurationText) });

            doubleWritesAnalyzer.AppendDoubleWritesFolder(build);
            resolveAssemblyReferenceAnalyzer.AppendFinalReport(build);
            cppAnalyzer.AppendCppAnalyzer(build);

            if (build.LogFilePath != null)
            {
                build.AddChildAtBeginning(new Item { Text = Intern(build.LogFilePath) });
            }

            var durations = taskDurations
                .OrderByDescending(kvp => kvp.Value.Duration)
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
                    var taskItem = new SearchableItem
                    {
                        Text = Intern(kvp.Key) + " = " + Intern($"{TextUtilities.DisplayDuration(kvp.Value.Duration)}, {kvp.Value.Count} calls."),
                        SearchText = $@"$task ""{kvp.Key}""",
                    };
                    var childNodes = kvp.Value.ChildNodes.OrderByDescending(kv => kv.Value.Duration).Take(10);
                    foreach (var durationNodes in childNodes)
                    {
                        taskItem.AddChild(new SearchableItem
                        {
                            Text = Intern(durationNodes.Key) + " = " + Intern($"{TextUtilities.DisplayDuration(durationNodes.Value.Duration)}, {durationNodes.Value.Count} calls."),
                            SearchText = $@"$target ""{durationNodes.Key}""",
                        });
                    }
                    top10Tasks.AddChild(taskItem);
                }
            }

            if (analyzerReports.Count > 0)
            {
                var analyzerReportSummary = build.GetOrCreateNodeWithName<Folder>(Intern($"Analyzer Summary"));
                CscTaskAnalyzer.CreateMergedReport(analyzerReportSummary, analyzerReports.ToArray());
            }

            if (generatorReports.Count > 0)
            {
                var generatorReportSummary = build.GetOrCreateNodeWithName<Folder>(Intern($"Generator Summary"));
                CscTaskAnalyzer.CreateMergedReport(generatorReportSummary, generatorReports.ToArray());
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

            if (string.IsNullOrEmpty(project.TargetFramework) || string.IsNullOrEmpty(project.Platform) || string.IsNullOrEmpty(project.Configuration))
            {
                var evaluation = build.FindEvaluation(project.EvaluationId);
                if (evaluation != null)
                {
                    project.TargetFramework = evaluation.TargetFramework;
                    project.Platform = evaluation.Platform;
                    project.Configuration = evaluation.Configuration;

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
            if (!string.IsNullOrEmpty(task.FromAssembly))
            {
                task.AddChildAtBeginning(new Property { Name = Strings.Assembly, Value = task.FromAssembly });
                build.RegisterTask(task);
            }

            UpdateTaskDurations(task);

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
                var (analyzerReport, generatorReport) = CscTaskAnalyzer.Analyze(task);
                if (analyzerReport is not null)
                {
                    analyzerReports.Add(analyzerReport);
                }

                if (generatorReport is not null)
                {
                    generatorReports.Add(generatorReport);
                }
            }
            else if (task is CppAnalyzer.CppTask cppTask)
            {
                cppAnalyzer.AnalyzeTask(cppTask);
            }

            fileCopyMap.AnalyzeTask(task);
            doubleWritesAnalyzer.AnalyzeTask(task);

            CollapseMessagesToSubfolder(task);
        }

        private void CollapseMessagesToSubfolder(Task task)
        {
            var messages = task.Children.OfType<Message>().ToArray();
            if (messages.Length <= 10)
            {
                return;
            }

            for (int i = task.Children.Count - 1; i >= 0; i--)
            {
                if (task.Children[i] is Message)
                {
                    task.Children.RemoveAt(i);
                }
            }

            var subfolder = task.GetOrCreateNodeWithName<Folder>(Strings.Messages);
            foreach (var message in messages)
            {
                subfolder.AddChild(message);
            }
        }

        private void UpdateTaskDurations(Task task)
        {
            var parentName =
                (task.Parent is Task parentTask) ? parentTask.Name :
                (task.Parent is Target parentTarget) ? parentTarget.Name :
                "???";

            if (!taskDurations.TryGetValue(task.Name, out var durationTuple))
            {
                durationTuple = new TaskStatistic();
            }

            durationTuple.Duration += task.Duration;
            durationTuple.Count++;

            if (durationTuple.ChildNodes.TryGetValue(parentName, out var parentDuration))
            {
                parentDuration.Count++;
                parentDuration.Duration += task.Duration;
            }
            else
            {
                durationTuple.ChildNodes[parentName] = new NodeStatistic() { Count = 1, Duration = task.Duration };
            }

            taskDurations[task.Name] = durationTuple;
        }

        private void AnalyzeTarget(Target target)
        {
            MarkAsLowRelevanceIfNeeded(target);
            AddDependsOnTargets(target);

            if (target.Name == "_CopyOutOfDateSourceItemsToOutputDirectory" && target.Skipped)
            {
                fileCopyMap.AnalyzeTarget(target);
            }
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
            if (!target.HasChildren)
            {
                target.IsLowRelevance = true;
                return;
            }

            if (target.Children.All(c => c is Message))
            {
                target.IsLowRelevance = true;
                foreach (var child in target.Children.OfType<Message>())
                {
                    child.IsLowRelevance = true;
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

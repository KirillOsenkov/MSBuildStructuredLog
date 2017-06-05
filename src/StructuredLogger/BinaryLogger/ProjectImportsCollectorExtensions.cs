using Microsoft.Build.Framework;

namespace Microsoft.Build.Logging
{
    // This functionality is separate from SourceFileCollector to avoid having SourceFileCollector
    // depend on MSBuild. It seems like a more standalone and useful utility and it feels better to
    // have the population from BuildEventArgs in a separate file.
    internal static class ProjectImportsCollectorExtensions
    {
        public static void IncludeSourceFiles(this ProjectImportsCollector projectImportsCollector, BuildEventArgs e)
        {
            if (e is TaskStartedEventArgs)
            {
                var taskArgs = (TaskStartedEventArgs)e;
                projectImportsCollector.AddFile(taskArgs.TaskFile);
            }
            else if (e is TargetStartedEventArgs)
            {
                var targetArgs = (TargetStartedEventArgs)e;
                projectImportsCollector.AddFile(targetArgs.TargetFile);
            }
            else if (e is ProjectStartedEventArgs)
            {
                var projectStarted = (ProjectStartedEventArgs)e;
                projectImportsCollector.AddFile(projectStarted.ProjectFile);
            }
        }
    }
}

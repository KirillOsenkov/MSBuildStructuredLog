using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Logging.StructuredLogger;

namespace BinlogTool
{
    public class ListTools : BinlogToolCommandBase
    {
        public void Run(string binLogFilePath)
        {
            var build = this.ReadBuild(binLogFilePath);
            BuildAnalyzer.AnalyzeBuild(build);
            var strings = build.StringTable.Instances.OrderBy(s => s).ToArray();

            Console.WriteLine($"MSBuild version = {build.MSBuildVersion}");

            var commitSha = GetSourceCommitId(build);
            if (commitSha != null)
            {
                Console.WriteLine($"Git commit: {commitSha}");
            }

            var tools = new HashSet<string>();

            var tasks = build.FindChildrenRecursive<Task>();
            foreach (var task in tasks)
            {
                var toolInfo = GetToolInfo(task);
                if (toolInfo != null)
                {
                    tools.Add(toolInfo);
                }
            }

            foreach (var tool in tools.OrderBy(t => t))
            {
                Console.WriteLine(tool);
            }
        }

        private string GetSourceCommitId(Build build)
        {
            var environment = build.FindChild<Folder>("Environment");
            if (environment == null)
            {
                return null;
            }

            var sourceCommitId = environment.FindChild<Property>(p => p.Name == "SOURCECOMMITID");
            if (sourceCommitId != null)
            {
                return sourceCommitId.Value;
            }

            return null;
        }

        private static HashSet<string> ignoreTasks = new HashSet<string>()
        {
            "GetRestoreProjectStyleTask",
            "GetRestoreSettingsTask",
            "GetRestoreDotnetCliToolsTask",
            "GetProjectTargetFrameworksTask",
            "GetRestoreProjectReferencesTask",
            "GetRestorePackageReferencesTask",
            "GetRestorePackageDownloadsTask",
            "GetRestoreFrameworkReferencesTask",
            "RestoreTask",
            "GetReferenceNearestTargetFrameworkTask",
            "ResolvePackageFileConflicts",
            "Touch",
            "UnpackLibraryResources"
        };

        private string GetToolInfo(Task task)
        {
            if (ignoreTasks.Contains(task.Name))
            {
                return null;
            }

            if (task.Name == "Exec")
            {
                var args = task.FindChild<Property>(p => p.Name == "CommandLineArguments");
                string arguments = null;

                if (args == null)
                {
                    var parameters = task.FindChild<Folder>("Parameters");
                    if (parameters != null)
                    {
                        var command = parameters.FindChild<Property>(p => p.Name == "Command");
                        if (command != null)
                        {
                            arguments = command.Value;
                        }
                    }
                }
                else
                {
                    arguments = args.Value as string;
                }

                if (arguments != null)
                {
                    arguments = arguments.TrimStart('\'', '"');
                    int space = arguments.IndexOf(' ');
                    if (space > 0)
                    {
                        arguments = arguments.Substring(0, space);
                    }

                    arguments = arguments.TrimEnd('\'', '"');
                    if (arguments == "chmod")
                    {
                        return null;
                    }

                    return arguments;
                }

                return null;
            }

            var assembly = task.FindChild<Property>(p => p.Name == "Assembly");
            if (assembly == null)
            {
                return null;
            }

            string result = assembly.Value;
            if (string.IsNullOrEmpty(result))
            {
                return null;
            }

            if (result.StartsWith("Microsoft.Build.Tasks.Core"))
            {
                return null;
            }

            var versionMessage = task.FindChild<Message>(m => m.Text is string message &&
                message.Length < 200 &&
                !message.Contains("\n") &&
                !message.Contains("Leaving it untouched") &&
                !message.StartsWith("ILLink:") &&
                    (message.StartsWith("Using Xcode") ||
                    message.Contains("version", StringComparison.OrdinalIgnoreCase)));
            if (versionMessage != null)
            {
                return versionMessage.Text;
            }

            if (result.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                result = NormalizePath(result);
                return result;
            }

            return result;
        }

        public static string NormalizePath(string path)
        {
            var parts = path.Split('/').ToList();
            for (int i = 0; i < parts.Count; i++)
            {
                if (i > 0 && parts[i] == "..")
                {
                    parts.RemoveRange(i - 1, 2);
                    i -= 2;
                }
            }

            return string.Join('/', parts);
        }
    }
}

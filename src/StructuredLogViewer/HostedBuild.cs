using System;
using System.Threading.Tasks;
using Microsoft.Build.CommandLine;
using Microsoft.Build.Logging.StructuredLogger;
using Microsoft.Build.Utilities;

namespace StructuredLogViewer
{
    public class HostedBuild
    {
        private readonly string customArguments;
        private readonly string projectFilePath;

        public HostedBuild(string projectFilePath, string customArguments)
        {
            this.projectFilePath = projectFilePath;
            this.customArguments = customArguments ?? "";
        }

        public static string GetPrefixArguments(string projectFilePath)
        {
            var msbuildExe = ToolLocationHelper.GetPathToBuildToolsFile("msbuild.exe", ToolLocationHelper.CurrentToolsVersion);
            return $@"""{msbuildExe}"" ""{projectFilePath}""";
        }

        public static string GetPostfixArguments()
        {
            var loggerDll = typeof(StructuredLogger).Assembly.Location;
            return $@"/v:diag /noconlog /logger:{nameof(StructuredLogger)},""{loggerDll}"";BuildLog.xml";
        }

        public Task<Build> BuildAndGetResult(BuildProgress progress)
        {
            var prefixArguments = GetPrefixArguments(projectFilePath);
            var postfixArguments = GetPostfixArguments();
            var commandLine = $@"{prefixArguments} {customArguments} {postfixArguments}";

            progress.MSBuildCommandLine = commandLine;
            StructuredLogger.SaveLogToDisk = false;

            return System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    var exitType = MSBuildApp.Execute(commandLine);
                    var result = StructuredLogger.CurrentBuild;
                    if (result == null)
                    {
                        result = new Build();
                        result.Succeeded = false;
                        result.AddChild(new Message() { Text = "Build failed with exitType = " + exitType.ToString() });
                    }

                    return result;
                }
                catch (Exception ex)
                {
                    var build = new Build();
                    build.Succeeded = false;
                    build.AddChild(new Message() { Text = "Exception occurred during build:" });
                    build.AddChild(new Error() { Text = ex.ToString() });
                    return build;
                }
            });
        }
    }
}

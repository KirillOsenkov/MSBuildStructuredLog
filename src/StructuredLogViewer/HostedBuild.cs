using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Build.CommandLine;
using Microsoft.Build.Logging.StructuredLogger;
using Microsoft.Build.Utilities;

namespace StructuredLogViewer
{
    public class HostedBuild
    {
        private string projectFilePath;

        public HostedBuild(string projectFilePath)
        {
            this.projectFilePath = projectFilePath;
        }

        public Task<Build> BuildAndGetResult(BuildProgress progress)
        {
            var msbuildExe = ToolLocationHelper.GetPathToBuildToolsFile("msbuild.exe", ToolLocationHelper.CurrentToolsVersion);
            var loggerDll = typeof(StructuredLogger).Assembly.Location;
            var commandLine = $@"""{msbuildExe}"" ""{projectFilePath}"" /t:Rebuild /noconlog /logger:{nameof(StructuredLogger)},""{loggerDll}"";BuildLog.xml";
            progress.MSBuildCommandLine = commandLine;
            StructuredLogger.SaveLogToDisk = false;

            return System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    var result = MSBuildApp.Execute(commandLine);
                    return StructuredLogger.CurrentBuild;
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

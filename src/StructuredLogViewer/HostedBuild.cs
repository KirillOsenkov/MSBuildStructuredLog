using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Build.CommandLine;
using Microsoft.Build.Logging.StructuredLogger;

namespace StructuredLogViewer
{
    public class HostedBuild
    {
        private string projectFilePath;

        public HostedBuild(string projectFilePath)
        {
            this.projectFilePath = projectFilePath;
        }

        public Task<Build> BuildAndGetResult()
        {
            var msbuildExe = typeof(MSBuildApp).Assembly.Location;
            var loggerDll = typeof(StructuredLogger).Assembly.Location;
            var commandLine = $@"""{msbuildExe}"" ""{projectFilePath}"" /t:Rebuild /noconlog /logger:{nameof(StructuredLogger)},""{loggerDll}"";StructuredBuildLog.xml";
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

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
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
            var msbuildExe = SettingsService.GetMSBuildExe();
            return $@"""{msbuildExe}"" ""{projectFilePath}""";
        }

        private static readonly string logFilePath = Path.Combine(Path.GetTempPath(), $"MSBuildStructuredLog-{Process.GetCurrentProcess().Id}.buildlog");

        public static string GetPostfixArguments()
        {
            var loggerDll = typeof(StructuredLogger).Assembly.Location;
            return $@"/v:diag /nologo /noconlog /logger:{nameof(StructuredLogger)},""{loggerDll}"";""{logFilePath}""";
        }

        public Task<Build> BuildAndGetResult(BuildProgress progress)
        {
            var msbuildExe = SettingsService.GetMSBuildExe();
            var prefixArguments = GetPrefixArguments(projectFilePath);
            var postfixArguments = GetPostfixArguments();

            // the command line we display to the user should contain the full path to msbuild.exe
            var commandLine = $@"{prefixArguments} {customArguments} {postfixArguments}";
            progress.MSBuildCommandLine = commandLine;

            // the command line we pass to Process.Start doesn't need msbuild.exe
            commandLine = $@"""{projectFilePath}"" {customArguments} {postfixArguments}";

            return System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    var arguments = commandLine;
                    var processStartInfo = new ProcessStartInfo(msbuildExe, arguments);
                    processStartInfo.WorkingDirectory = Path.GetDirectoryName(projectFilePath);
                    var process = Process.Start(processStartInfo);
                    process.WaitForExit();

                    var build = Serialization.Read(logFilePath);
                    File.Delete(logFilePath);
                    return build;
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

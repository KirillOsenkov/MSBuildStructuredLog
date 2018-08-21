﻿using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

using Microsoft.Build.Logging.StructuredLogger;

namespace StructuredLogViewer.Core
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

        private static readonly string logFilePath = Path.Combine(Path.GetTempPath(), $"MSBuildStructuredLog-{Process.GetCurrentProcess().Id}.buildlog");

        public static string GetPostfixArguments()
        {
            var loggerDll = typeof(StructuredLogger).Assembly.Location;
            return $@"/v:diag /nologo /noconlog /logger:{nameof(StructuredLogger)},""{loggerDll}"";""{logFilePath}""";
        }

        public Task<Build> BuildAndGetResult(BuildProgress progress, IMSBuildLocator locator)
        {
            var msbuildExe = SettingsService.GetMSBuildExe(locator);
            var postfixArguments = GetPostfixArguments();

            // the command line we pass to Process.Start doesn't need msbuild.exe
            var commandLine = $"{projectFilePath.QuoteIfNeeded()} {customArguments} {postfixArguments}";

            // the command line we display to the user should contain the full path to msbuild.exe
            progress.MSBuildCommandLine = $"{msbuildExe.QuoteIfNeeded()} {commandLine}";

            return System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    var arguments = commandLine;
                    var processStartInfo = new ProcessStartInfo(msbuildExe, arguments) {
                            WorkingDirectory = Path.GetDirectoryName(projectFilePath)
                        };
                    var process = Process.Start(processStartInfo);
                    process.WaitForExit();

                    var build = Serialization.Read(logFilePath);
                    File.Delete(logFilePath);

                    var projectImportsZip = Path.ChangeExtension(logFilePath, ".ProjectImports.zip");
                    if (File.Exists(projectImportsZip))
                    {
                        File.Delete(projectImportsZip);
                    }

                    return build;
                }
                catch (Exception ex)
                {
                    ex = ExceptionHandler.Unwrap(ex);
                    var build = new Build {
                        Succeeded = false
                    };
                    build.AddChild(new Message { Text = "Exception occurred during build:" });
                    build.AddChild(new Error { Text = ex.ToString() });
                    return build;
                }
            });
        }
    }
}

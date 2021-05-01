using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

#nullable enable

namespace StructuredLogViewer.Core
{
    public static class DotnetUtilities
    {
        /// <summary>
        /// Get a collection of paths to MSBuild.dll from .NET SDK.
        /// </summary>
        public static IEnumerable<string> GetMsBuildPathCollection()
        {
            var (process, lines) = CreateListSdksCollector();
            if (process is null)
                return Array.Empty<string>();

            process.WaitForExit();

            return lines.Select(TransformToMsBuildPath);
        }

        private static (Process? process, List<string> lines) CreateListSdksCollector()
        {
            Process? process;
            try
            {
                process = Start("--list-sdks");
            }
            catch
            {
                return (null, null!);
            }

            if (process is null || process.HasExited)
            {
                return (null, null!);
            }

            var lines = new List<string>();
            process.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    lines.Add(e.Data);
                }
            };

            process.BeginOutputReadLine();

            return (process, lines);
        }

        private static Process? Start(string arguments)
        {
            var startInfo = new ProcessStartInfo("dotnet", arguments)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            RemoveMsBuildEnvironmentVariables(startInfo.Environment);

            return Process.Start(startInfo);
        }

        /// <summary>
        /// Remove various MSBuild environment variables set by OmniSharp to ensure that
        /// the .NET CLI is not launched with the wrong values.
        /// </summary>
        private static void RemoveMsBuildEnvironmentVariables(IDictionary<string, string?> environment)
        {
            environment.Remove("MSBUILD_EXE_PATH");
            environment.Remove("MSBuildExtensionsPath");
        }

        private static string TransformToMsBuildPath(string line)
        {
            var r = line.Split('[');
            var version = r[0].TrimEnd();
            var pathSdks = r[1].TrimEnd(']');
            return Path.Combine(pathSdks, version, "MSBuild.dll");
        }
    }
}
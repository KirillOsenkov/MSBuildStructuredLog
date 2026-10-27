using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace StructuredLogViewer
{
    internal static class DotnetHostEnvironment
    {
        public static bool IsMacOS() => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        public static string GetProcessPath()
        {
            try
            {
                return Process.GetCurrentProcess().MainModule?.FileName;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Set the .NET host path for MSBuild child tasks when starting MSBuild.dll from the
        /// macOS viewer. The viewer runs as a framework-dependent .NET application, so
        /// Process.GetCurrentProcess().MainModule identifies its dotnet host.
        /// </summary>
        public static void ConfigureForMSBuild(ProcessStartInfo processStartInfo, string dotnetHostPath, bool isMacOS)
        {
            if (!isMacOS || string.IsNullOrEmpty(dotnetHostPath) ||
                (processStartInfo.Environment.TryGetValue("DOTNET_HOST_PATH", out var existingHostPath) &&
                 !string.IsNullOrEmpty(existingHostPath)))
            {
                return;
            }

            var fileName = Path.GetFileNameWithoutExtension(dotnetHostPath);
            if (string.Equals(fileName, "dotnet", StringComparison.OrdinalIgnoreCase))
            {
                processStartInfo.Environment["DOTNET_HOST_PATH"] = dotnetHostPath;
            }
        }
    }
}

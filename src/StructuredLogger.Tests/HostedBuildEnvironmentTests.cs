using System;
using System.Diagnostics;
using System.IO;
using StructuredLogViewer;
using Xunit;

namespace StructuredLogger.Tests
{
    public class HostedBuildEnvironmentTests
    {
        [Fact]
        public void MacOSDotnetHostPathIsPassedToChildProcess()
        {
            var expectedHostPath = Path.Combine(Path.GetTempPath(), "dotnet");
            var previousHostPath = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
            Environment.SetEnvironmentVariable("DOTNET_HOST_PATH", null);
            try
            {
                var startInfo = new ProcessStartInfo("cmd.exe", "/c echo %DOTNET_HOST_PATH%")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                };

                ConfigureDotnetHostPath(startInfo, expectedHostPath, isMacOS: true);

                using var process = Process.Start(startInfo);
                var childHostPath = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();
                Assert.Equal(expectedHostPath, childHostPath);
            }
            finally
            {
                Environment.SetEnvironmentVariable("DOTNET_HOST_PATH", previousHostPath);
            }
        }

        [Fact]
        public void ExistingDotnetHostPathIsPreserved()
        {
            var startInfo = new ProcessStartInfo("cmd.exe", "/c exit 0")
            {
                UseShellExecute = false,
            };
            startInfo.Environment["DOTNET_HOST_PATH"] = "user-provided-host";

            ConfigureDotnetHostPath(startInfo, "C:\\dotnet.exe", isMacOS: true);

            Assert.Equal("user-provided-host", startInfo.Environment["DOTNET_HOST_PATH"]);
        }

        [Fact]
        public void NonMacOSAndNonDotnetExecutablesAreNotUsed()
        {
            var startInfo = new ProcessStartInfo("cmd.exe", "/c exit 0")
            {
                UseShellExecute = false,
            };

            startInfo.Environment.Remove("DOTNET_HOST_PATH");
            ConfigureDotnetHostPath(startInfo, "C:\\dotnet.exe", isMacOS: false);
            Assert.False(startInfo.Environment.ContainsKey("DOTNET_HOST_PATH"));

            ConfigureDotnetHostPath(startInfo, "C:\\viewer.exe", isMacOS: true);
            Assert.False(startInfo.Environment.ContainsKey("DOTNET_HOST_PATH"));
        }

        private static void ConfigureDotnetHostPath(ProcessStartInfo processStartInfo, string dotnetHostPath, bool isMacOS)
        {
            DotnetHostEnvironment.ConfigureForMSBuild(processStartInfo, dotnetHostPath, isMacOS);
        }
    }
}

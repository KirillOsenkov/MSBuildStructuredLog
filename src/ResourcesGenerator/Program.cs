using System;
using System.IO;
using System.Linq;

namespace ResourcesGenerator
{
    class Program
    {
        /// <summary>
        /// To regenerate Strings.json, add the strings you want to consume from MSBuild to <see cref="ResourceCreator.ResourceNames"/>
        /// and run this program
        /// </summary>
        static void Main()
        {
            var options = new Microsoft.Build.Locator.VisualStudioInstanceQueryOptions()
            {
                DiscoveryTypes = Microsoft.Build.Locator.DiscoveryType.VisualStudioSetup | Microsoft.Build.Locator.DiscoveryType.DotNetSdk
            };
            var instances = Microsoft.Build.Locator.MSBuildLocator.QueryVisualStudioInstances(options)
                .OrderByDescending(i => i.Version).ToArray();
            var instance =
                instances.FirstOrDefault(i => !i.MSBuildPath.Contains("Preview")) ??
                instances.FirstOrDefault();

            var candidates = new[]
            {
                @"C:\msbuild\artifacts\bin\bootstrap\net472\MSBuild\Current\Bin\amd64",
                instance?.MSBuildPath,
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "Microsoft Visual Studio", "2022", "Enterprise", "MSBuild", "Current", "Bin"),
            };

            var msbuildPath = candidates.FirstOrDefault(c => c != null && Directory.Exists(c));

            if (Directory.Exists(msbuildPath))
            {
                ResourceCreator.CreateResourceFile(msbuildPath);
            }
            else
            {
                Console.Error.WriteLine("Couldn't find MSBuild at " + msbuildPath);
            }
        }
    }
}

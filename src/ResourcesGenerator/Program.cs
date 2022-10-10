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
            var msbuildPath = instance?.MSBuildPath;

            var otherCandidates = new[]
            {
                @"C:\msbuild\artifacts\bin\bootstrap\net472\MSBuild\Current\Bin",
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "Microsoft Visual Studio", "2022", "Enterprise", "MSBuild", "Current", "Bin"),
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    "Microsoft Visual Studio", "2019", "Enterprise", "MSBuild", "Current", "Bin")
            };

            if (msbuildPath == null)
            {
                msbuildPath = otherCandidates.FirstOrDefault(Directory.Exists);
            }

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

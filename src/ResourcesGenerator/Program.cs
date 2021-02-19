using System;
using System.IO;
using System.Linq;

namespace ResourcesGenerator
{
    class Program
    {
        static void Main(string[] args)
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

            if (msbuildPath == null)
            {
                string defaultMSBuild = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    "Microsoft Visual Studio", "2019", "Enterprise", "MSBuild", "Current", "Bin");
                if (Directory.Exists(defaultMSBuild))
                {
                    msbuildPath = defaultMSBuild;
                }
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

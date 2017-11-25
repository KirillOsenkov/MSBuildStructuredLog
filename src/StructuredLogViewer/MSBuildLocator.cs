using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace StructuredLogViewer
{
    public class MSBuildLocator
    {
        public static string[] GetMSBuildLocations()
        {
            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

            var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                Path.Combine(programFilesX86, @"Microsoft Visual Studio\2017\Enterprise\MSBuild\15.0\Bin\MSBuild.exe"),
                Path.Combine(programFilesX86, @"Microsoft Visual Studio\2017\Enterprise\MSBuild\15.0\Bin\amd64\MSBuild.exe"),
                Path.Combine(programFilesX86, @"Microsoft Visual Studio\2017\Professional\MSBuild\15.0\Bin\MSBuild.exe"),
                Path.Combine(programFilesX86, @"Microsoft Visual Studio\2017\Professional\MSBuild\15.0\Bin\amd64\MSBuild.exe"),
                Path.Combine(programFilesX86, @"Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\MSBuild.exe"),
                Path.Combine(programFilesX86, @"Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\amd64\MSBuild.exe"),
                Path.Combine(programFilesX86, @"MSBuild\14.0\Bin\MSBuild.exe"),
                Path.Combine(programFilesX86, @"MSBuild\14.0\Bin\amd64\MSBuild.exe"),
                Path.Combine(programFilesX86, @"MSBuild\12.0\Bin\MSBuild.exe"),
                Path.Combine(programFilesX86, @"MSBuild\12.0\Bin\amd64\MSBuild.exe"),
                Path.Combine(windows, @"Microsoft.NET\Framework\v4.0.30319\MSBuild.exe"),
                Path.Combine(windows, @"Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe"),
            };

            var vs15Locations = GetVS15Locations();
            candidates.UnionWith(vs15Locations.Select(l => Path.Combine(l, "MSBuild", "15.0", "Bin", "MSBuild.exe")));
            candidates.UnionWith(vs15Locations.Select(l => Path.Combine(l, "MSBuild", "15.0", "Bin", "amd64", "MSBuild.exe")));

            var finalResults = candidates.Where(File.Exists).OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToArray();
            return finalResults;
        }

        public static string[] GetVS15Locations()
        {
            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            var installer = Path.Combine(programFilesX86, "Microsoft Visual Studio", "Installer");
            var vswhere = Path.Combine(installer, "vswhere.exe");
            if (!File.Exists(vswhere))
            {
                return Array.Empty<string>();
            }

            var args = "-prerelease -format value -property installationPath -nologo";
            var startInfo = new ProcessStartInfo
            {
                Arguments = args,
                CreateNoWindow = true,
                FileName = vswhere,
                UseShellExecute = false,
                RedirectStandardOutput = true
            };

            var resultList = new List<string>();

            var process = Process.Start(startInfo);
            var output = process.StandardOutput.ReadToEnd();
            resultList.AddRange(output.GetLines().Where(Directory.Exists));

            process.WaitForExit(3000);

            if (process.ExitCode != 0)
            {
                return Array.Empty<string>();
            }

            return resultList.ToArray();
        }

        public static void BrowseForMSBuildExe()
        {
            var openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "MSBuild.exe|MSBuild.exe";
            openFileDialog.Title = "Select MSBuild.exe location";
            openFileDialog.CheckFileExists = true;
            var result = openFileDialog.ShowDialog();
            if (result != true)
            {
                return;
            }

            SettingsService.AddRecentMSBuildLocation(openFileDialog.FileName);
        }
    }
}
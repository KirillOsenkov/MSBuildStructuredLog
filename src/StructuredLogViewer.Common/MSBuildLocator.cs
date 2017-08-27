using System;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using System.Collections.Generic;

namespace StructuredLogViewer
{
    public class MSBuildLocator
    {
        public static string[] GetMSBuildLocations()
        {
            // TODO: xplat

            var programFilesX86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
            if (string.IsNullOrEmpty(programFilesX86))
            {
                programFilesX86 = Environment.GetEnvironmentVariable("ProgramFiles");
            }
            
            var locations = new List<string>
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
            };

            var windows = Environment.GetEnvironmentVariable("WINDIR");
            if (!string.IsNullOrEmpty(windows))
            {
                locations.Add(Path.Combine(windows, @"Microsoft.NET\Framework\v4.0.30319\MSBuild.exe"));
                locations.Add(Path.Combine(windows, @"Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe"));
            }

            return locations.Where(File.Exists).ToArray();
        }
    }
}
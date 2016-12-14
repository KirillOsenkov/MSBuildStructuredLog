using System;
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

            return new[]
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
            }.Where(File.Exists).ToArray();
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
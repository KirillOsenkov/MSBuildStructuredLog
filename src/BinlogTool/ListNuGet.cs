using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Logging.StructuredLogger;

namespace BinlogTool
{
    public class ListNuget : BinlogToolCommandBase
    {
        public ListNuget(string[] args)
        {
        }

        public int Run(string binlog, string outputFilePath)
        {
            Build build = this.ReadBuild(binlog, false);
            if (build == null)
            {
                return -1;
            }

            if (outputFilePath != null)
            {
                outputFilePath = Path.GetFullPath(outputFilePath);
                var outputDirectory = Path.GetDirectoryName(outputFilePath);
                if (!Directory.Exists(outputDirectory))
                {
                    Directory.CreateDirectory(outputDirectory);
                }
            }

            var nuget = new NuGetSearch(build);

            var report = nuget.ListAllPackages();
            if (report == null || report.Packages == null)
            {
                return -2;
            }

            var lines = new List<string>();
            foreach (var package in report.Packages)
            {
                lines.Add(package.ToString());
            }

            if (outputFilePath == null)
            {
                foreach (var line in lines)
                {
                    Console.WriteLine(line);
                }
            }
            else
            {
                if (File.Exists(outputFilePath))
                {
                    var oldLines = File.ReadAllLines(outputFilePath).Where(l => !string.IsNullOrEmpty(l)).ToArray();
                    if (Enumerable.SequenceEqual(oldLines, lines))
                    {
                        return 0;
                    }
                    else
                    {
                        Error($"The list of NuGet packages is different from the baseline file contents: {outputFilePath}");

                        var newPackages = lines.Except(oldLines).ToArray();
                        if (newPackages.Any())
                        {
                            Error("New packages not in the baseline:");
                            foreach (var newPackage in newPackages)
                            {
                                Error(newPackage);
                            }
                        }

                        var oldPackages = oldLines.Except(lines).ToArray();
                        if (oldPackages.Any())
                        {
                            Error("Packages in the baseline that are not found:");
                            foreach (var oldPackage in oldPackages)
                            {
                                Error(oldPackage);
                            }
                        }

                        return -10;
                    }
                }

                File.WriteAllLines(outputFilePath, lines);
            }

            return 0;
        }

        private void Error(string text)
        {
            Console.Error.WriteLine(text);
        }
    }
}

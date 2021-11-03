using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Build.Logging.StructuredLogger;

namespace BinlogTool
{
    public class SaveFiles
    {
        private string[] args;

        public SaveFiles(string[] args)
        {
            this.args = args;
        }

        public void Run(string binlog, string outputDirectory)
        {
            if (string.IsNullOrEmpty(binlog) || !File.Exists(binlog))
            {
                return;
            }

            outputDirectory = Path.GetFullPath(outputDirectory);
            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            binlog = Path.GetFullPath(binlog);

            var build = BinaryLog.ReadBuild(binlog);
            SaveFilesFrom(build, outputDirectory);

            GenerateSources(build, outputDirectory);
        }

        private void GenerateSources(Build build, string outputDirectory)
        {
            var compilerInvocations = CompilerInvocationsReader.ReadInvocations(build);
            foreach (var invocation in compilerInvocations)
            {
                GenerateSourcesForProject(invocation, outputDirectory);
            }
        }

        private void GenerateSourcesForProject(CompilerInvocation invocation, string outputDirectory)
        {
            var virtualProjectFilePath = invocation.ProjectFilePath;
            virtualProjectFilePath = ArchiveFile.CalculateArchivePath(virtualProjectFilePath);
            var physicalProjectPath = GetPhysicalPath(outputDirectory, virtualProjectFilePath);
            var physicalProjectDirectory = Path.GetDirectoryName(physicalProjectPath);
            if (!Directory.Exists(physicalProjectDirectory))
            {
                return;
            }

            var arguments = invocation.CommandLineArguments.Tokenize();
            foreach (var argument in arguments)
            {
                if (argument.StartsWith("/"))
                {
                    var reference = TryGetReference(argument);
                    if (reference != null)
                    {
                        reference = ArchiveFile.CalculateArchivePath(reference);
                        var physicalReferencePath = GetPhysicalPath(outputDirectory, reference);
                        WriteEmptyAssembly(physicalReferencePath);
                    }
                }
                else
                {
                    var sourceRelativePath = argument.TrimQuotes();
                    var physicalSourcePath = Path.Combine(physicalProjectDirectory, sourceRelativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar));
                    physicalSourcePath = Path.GetFullPath(physicalSourcePath);
                    WriteFile(physicalSourcePath, "");
                }
            }
        }

        private void WriteEmptyAssembly(string physicalReferencePath)
        {
            var directory = Path.GetDirectoryName(physicalReferencePath);
            if (File.Exists(physicalReferencePath))
            {
                return;
            }

            Directory.CreateDirectory(directory);

            var csc = FindCsc();
            if (!File.Exists(csc))
            {
                return;
            }

            var args = new ProcessStartInfo(csc, "/nologo /nowarn:CS2008 /t:library /out:" + Path.GetFileName(physicalReferencePath));
            args.WorkingDirectory = directory;
            var process = Process.Start(args);
            process.WaitForExit();
        }

        private string TryGetReference(string argument)
        {
            string reference;
            if (argument.StartsWith("/r:"))
            {
                reference = argument.Substring(3);
            }
            else if (argument.StartsWith("/reference:"))
            {
                reference = argument.Substring(11);
            }
            else
            {
                return null;
            }

            if (reference.Length >= 2)
            {
                // Remove enclosing quotes if present
                if ((reference.StartsWith('\'') && reference.EndsWith('\'')) ||
                    (reference.StartsWith('"') && reference.EndsWith('"')))
                {
                    return reference.Substring(1, reference.Length - 2);
                }
            }
            return reference;
        }

        private void SaveFilesFrom(Build build, string outputDirectory)
        {
            foreach (var file in build.SourceFiles.Values.OrderBy(f => f.FullPath))
            {
                var filePath = file.FullPath;
                if (filePath.EndsWith(".metaproj", StringComparison.OrdinalIgnoreCase) ||
                    filePath.EndsWith(".metaproj.tmp", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    string pathOnDisk = GetPhysicalPath(outputDirectory, filePath);
                    string text = file.Text;
                    text = ProcessProjectFileText(outputDirectory, filePath, pathOnDisk, text);
                    WriteFile(pathOnDisk, text);
                }
                catch
                {
                }
            }
        }

        private string ProcessProjectFileText(string outputDirectory, string virtualPath, string physicalPath, string text)
        {
            if (virtualPath.EndsWith("proj.nuget.g.props", StringComparison.OrdinalIgnoreCase))
            {
                text = ProcessElementValue(text, nugetPackageRoot =>
                {
                    int numberOfUp = virtualPath.Count(c => c == '\\') - 1;
                    string ups = string.Concat(Enumerable.Range(1, numberOfUp).Select(i => "..\\"));
                    return $@"$([System.IO.Path]::GetFullPath($([System.IO.Path]::Combine($(MSBuildThisFileDirectory), ""{ups}"", ""{nugetPackageRoot.TrimStart('/')}""))))";
                });
            }

            return text;
        }

        private string ProcessElementValue(string text, Func<string, string> processor)
        {
            var document = XDocument.Parse(text, LoadOptions.PreserveWhitespace);
            var root = document.Root;
            var propertyGroup = GetElement(root, "PropertyGroup");

            var nugetPackageRoot = GetElement(propertyGroup, "NuGetPackageRoot");
            ReplaceElementValue(nugetPackageRoot, processor);

            var nugetPackageFolders = GetElement(propertyGroup, "NuGetPackageFolders");
            ReplaceElementValue(nugetPackageFolders, processor);

            return document.ToString();
        }

        private void ReplaceElementValue(XElement element, Func<string, string> processor)
        {
            var value = element.Value;
            value = processor(value);
            element.Value = value;
        }

        private XElement GetElement(XElement parent, string name)
        {
            return parent.Elements().FirstOrDefault(e => e.Name.LocalName == name);
        }

        public static string GetPhysicalPath(string outputDirectory, string archiveFilePath)
        {
            var relativePath = archiveFilePath
                .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
                .TrimStart(Path.DirectorySeparatorChar);
            var pathOnDisk = Path.Combine(outputDirectory, relativePath);
            pathOnDisk = Path.GetFullPath(pathOnDisk);
            return pathOnDisk;
        }

        private void WriteFile(string filePath, string text)
        {
            try
            {
                var directoryOnDisk = Path.GetDirectoryName(filePath);
                Directory.CreateDirectory(directoryOnDisk);

                if (File.Exists(filePath))
                {
                    var oldText = File.ReadAllText(filePath);
                    if (text == oldText)
                    {
                        return;
                    }
                }

                File.WriteAllText(filePath, text);
            }
            catch
            {
            }
        }

        private static string cscFullPath;
        public static string FindCsc()
        {
            if (cscFullPath != null)
            {
                return cscFullPath;
            }

            string[] candidates = new[]
            {
                @"C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
            };

            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                candidates = new[]
                {
                    @"/Library/Frameworks/Mono.framework/Versions/Current/Commands/csc"
                };
            }

            cscFullPath = candidates.FirstOrDefault(File.Exists);
            return cscFullPath;
        }
    }
}
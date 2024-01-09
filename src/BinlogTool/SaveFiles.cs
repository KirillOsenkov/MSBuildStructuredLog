using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Build.Logging.StructuredLogger;

namespace BinlogTool
{
    public class SaveFiles : BinlogToolCommandBase
    {
        private string[] args;

        public SaveFiles(string[] args)
        {
            this.args = args;
        }

        public void Run(string binlog, string outputDirectory, bool reconstruct = false)
        {
            Build build = this.ReadBuild(binlog, false);
            if (build == null)
            {
                return;
            }

            outputDirectory = Path.GetFullPath(outputDirectory);
            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            SaveFilesFrom(build, outputDirectory);

            if (reconstruct)
            {
                GenerateSources(build, outputDirectory);
            }
        }

        private void GenerateSources(Build build, string outputDirectory)
        {
            var compilerInvocations = CompilerInvocationsReader.ReadInvocations(build);
            foreach (var invocation in compilerInvocations)
            {
                try
                {
                    GenerateSourcesForProject(invocation, outputDirectory);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex.ToString());
                }
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

            var commandLineArgumentText = invocation.CommandLineArguments;

            var arguments = commandLineArgumentText.Tokenize();
            foreach (var argument in arguments)
            {
                if (argument.StartsWith("/"))
                {
                    var references = TryGetReferences(argument);
                    foreach (var referenceText in references)
                    {
                        var reference = ArchiveFile.CalculateArchivePath(referenceText);
                        var physicalReferencePath = GetPhysicalPath(outputDirectory, reference);

                        try
                        {
                            WriteEmptyAssembly(physicalReferencePath);
                        }
                        catch (Exception ex)
                        {
                            Console.Error.WriteLine(ex.ToString());
                        }
                    }
                }
                else
                {
                    var sourceRelativePath = argument.TrimQuotes();
                    var physicalSourcePath = Path.Combine(physicalProjectDirectory, sourceRelativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar));
                    physicalSourcePath = Path.GetFullPath(physicalSourcePath);

                    if (!File.Exists(physicalSourcePath))
                    {
                        WriteFile(physicalSourcePath, "");
                    }
                }
            }
        }

        private string EmptyDllFile;

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

            if (EmptyDllFile == null || !File.Exists(EmptyDllFile))
            {
                var args = new ProcessStartInfo(csc, "/nologo /nowarn:CS2008 /t:library /out:" + Path.GetFileName(physicalReferencePath));
                args.WorkingDirectory = directory;
                var process = Process.Start(args);
                process.WaitForExit();
                EmptyDllFile = physicalReferencePath;
            }
            else
            {
                File.Copy(EmptyDllFile, physicalReferencePath);
            }
        }

        private string[] TryGetReferences(string argument)
        {
            if (argument.StartsWith("/r:"))
            {
                argument = argument.Substring(3);
            }
            else if (argument.StartsWith("/reference:"))
            {
                argument = argument.Substring(11);
            }
            else
            {
                return Array.Empty<string>();
            }

            string[] result = argument.Split(',');

            for (int i = 0; i < result.Length; i++)
            {
                var reference = result[i];

                if (reference.Length >= 2)
                {
                    // Remove enclosing quotes if present
                    if ((reference.StartsWith('\'') || reference.StartsWith('"')) &&
                        (reference.EndsWith('\'') || reference.EndsWith('"')))
                    {
                        reference = reference.Substring(1, reference.Length - 2);
                    }
                }

                int equals = reference.IndexOf('=');
                if (equals >= 0 && equals < reference.Length - 1)
                {
                    reference = reference.Substring(equals + 1);
                }

                result[i] = reference;
            }

            return result;
        }

        private void SaveFilesFrom(Build build, string outputDirectory)
        {
            var files = build.SourceFiles;
            if (files == null)
            {
                return;
            }

            foreach (var file in files.OrderBy(f => f.FullPath))
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
                    if (File.Exists(pathOnDisk))
                    {
                        continue;
                    }

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

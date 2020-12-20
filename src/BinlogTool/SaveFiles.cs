using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

            var build = Serialization.Read(binlog);
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
                if (argument.StartsWith('/'))
                {
                    var reference = TryGetReference(argument);
                    if (reference != null)
                    {
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
            var assemblyName = Path.GetFileNameWithoutExtension(physicalReferencePath);
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
            if (argument.StartsWith("/r:"))
            {
                return argument.Substring(3);
            }
            else if (argument.StartsWith("/reference:"))
            {
                return argument.Substring(11);
            }

            return null;
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
                    WriteFile(pathOnDisk, file.Text);
                }
                catch
                {
                }
            }
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
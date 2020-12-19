using System;
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
            //SaveFilesFrom(build, outputDirectory);

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
            var virtualProjectDirectory = invocation.ProjectDirectory;
            virtualProjectDirectory = ArchiveFile.CalculateArchivePath(virtualProjectDirectory);
            var physicalProjectPath = GetPhysicalPath(outputDirectory, virtualProjectDirectory);
        }

        private void SaveFilesFrom(Build build, string outputDirectory)
        {
            foreach (var file in build.SourceFiles.Values.OrderBy(f => f.FullPath))
            {
                try
                {
                    string pathOnDisk = GetPhysicalPath(outputDirectory, file.FullPath);
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
    }
}
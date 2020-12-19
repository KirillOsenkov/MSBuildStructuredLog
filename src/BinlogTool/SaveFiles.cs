using System;
using System.IO;
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
        }

        private void SaveFilesFrom(Build build, string outputDirectory)
        {
        }
    }
}
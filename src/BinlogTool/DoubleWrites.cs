using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.Logging.StructuredLogger;

namespace BinlogTool
{
    public class DoubleWrites : BinlogToolCommandBase
    {
        public void Run(string binLogFilePath, string outputFilePath)
        {
            var build = this.ReadBuild(binLogFilePath);
            BuildAnalyzer.AnalyzeBuild(build);
            var doubleWrites = DoubleWritesAnalyzer.GetDoubleWrites(build);
            var sb = new StringBuilder();
            foreach (var doubleWrite in doubleWrites.OrderBy(s => s.Key))
            {
                sb.AppendLine(doubleWrite.Key);
                foreach (var source in doubleWrite.Value.OrderBy(s => s))
                {
                    sb.AppendLine($"  {source}");
                }
            }

            if (!string.IsNullOrEmpty(outputFilePath))
            {
                File.WriteAllText(outputFilePath, sb.ToString());
            }
            else
            {
                Console.WriteLine(sb.ToString());
            }
        }
    }
}

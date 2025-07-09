using System;
using System.Linq;
using Microsoft.Build.Logging.StructuredLogger;

namespace BinlogTool
{
    public class TargetFrameworkEvaluation : BinlogToolCommandBase
    {
        public void Run(string binLogFilePath)
        {
            var build = this.ReadBuild(binLogFilePath);
            BuildAnalyzer.AnalyzeBuild(build);

            var evaluation = build.FindChild<TimedNode>("Evaluation");

            foreach (ProjectEvaluation child in evaluation.Children.Cast<ProjectEvaluation>())
            {
                Console.WriteLine(child.SourceFilePath + " " + child.TargetFramework);
            }
        }
    }
}

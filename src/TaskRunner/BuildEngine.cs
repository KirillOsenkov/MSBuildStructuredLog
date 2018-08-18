using System;
using System.Collections;
using Microsoft.Build.Framework;

namespace TaskRunner
{
    public class BuildEngine : IBuildEngine
    {
        public int ColumnNumberOfTaskNode => 0;

        public bool ContinueOnError => true;

        public int LineNumberOfTaskNode => 0;

        public string ProjectFileOfTaskNode => null;

        public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs)
        {
            throw new NotImplementedException();
        }

        public void LogCustomEvent(CustomBuildEventArgs e)
        {
            Console.WriteLine(e.Message);
        }

        public void LogErrorEvent(BuildErrorEventArgs e)
        {
            Console.WriteLine(e.Message);
        }

        public void LogMessageEvent(BuildMessageEventArgs e)
        {
            Console.WriteLine(e.Message);
        }

        public void LogWarningEvent(BuildWarningEventArgs e)
        {
            Console.WriteLine(e.Message);
        }
    }
}

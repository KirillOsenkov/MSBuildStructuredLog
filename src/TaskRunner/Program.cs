using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Build.Logging.StructuredLogger;
using StructuredLogViewer;

namespace TaskRunner
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length < 2 || args.Length > 4)
            {
                PrintHelp();
                return;
            }

            var binlog = args[0];
            if (!File.Exists(binlog))
            {
                Console.Error.WriteLine("File not found: " + binlog);
                return;
            }

            string taskName = null;
            if (!int.TryParse(args[1], out int index))
            {
                taskName = args[1];
                index = -1;
            }

            bool debug = false;
            bool pause = false;

            for (int i = 2; i < args.Length; i++)
            {
                if (args[i] == "debug")
                {
                    debug = true;
                }
                else if (args[i] == "pause")
                {
                    pause = true;
                }
                else
                {
                    Console.Error.WriteLine("Unknown argument: " + args[i]);
                    return;
                }
            }

            if (debug)
            {
                Debugger.Launch();
            }

            Microsoft.Build.Locator.MSBuildLocator.RegisterDefaults();

            new Program().Run(binlog, index, taskName);

            if (pause)
            {
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
            }
        }

        private static void PrintHelp()
        {
            Console.WriteLine("Usage: TaskRunner.exe <msbuild.binlog> (42|Csc) [debug]");
            Console.WriteLine("    where 42 is the index of the task in the .binlog to execute,");
            Console.WriteLine("    or Csc is the name of the task. If the task name is specified,");
            Console.WriteLine("    the first task with that name is run.");
            Console.WriteLine("    Specify 'debug' as the third argument to trigger Debugger attach prompt.");
        }

        private void Run(string binlog, int index, string taskName)
        {
            var build = Serialization.Read(binlog);

            // Need to analyze build here to fully emulate what the viewer does when opening a .binlog.
            // Since the analyzers will mutate the tree it affects indices of TimedNodes. To properly 
            // calculate the right index we need to have the same tree as in the viewer.
            BuildAnalyzer.AnalyzeBuild(build);

            Task task = null;
            if (index > -1)
            {
                task = build.FindDescendant(index) as Task;
            }
            else
            {
                task = build.FindFirstDescendant<Task>(t => t.Name == taskName);
            }

            if (task == null)
            {
                if (index > -1)
                {
                    Console.Error.WriteLine($"Task number {index} not found in {binlog}");
                }
                else
                {
                    Console.Error.WriteLine($"Task {taskName} not found in {binlog}");
                }
                
                return;
            }

            Executor.Execute(task);
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class CompilerInvocation
    {
        public const string CSharp = "C#";
        public const string FSharp = "F#";
        public const string TypeScript = "TypeScript";
        public const string VisualBasic = "Visual Basic";

        public string Language { get; set; }
        public string CommandLineArguments { get; set; }
        public string ProjectFilePath { get; set; }

        public string ProjectDirectory => ProjectFilePath == null ? "" : Path.GetDirectoryName(ProjectFilePath);

        public override string ToString()
        {
            return $"{ProjectFilePath} ({Language}): {CommandLineArguments}";
        }
    }

    public class CompilerInvocationsReader
    {
        public static IEnumerable<CompilerInvocation> ReadInvocations(string binLogFilePath)
        {
            return new CompilerInvocationsReader().Read(binLogFilePath);
        }

        public static IEnumerable<CompilerInvocation> ReadInvocations(Build build)
        {
            return new CompilerInvocationsReader().Read(build);
        }

        public IEnumerable<CompilerInvocation> Read(string binLogFilePath)
        {
            binLogFilePath = Path.GetFullPath(binLogFilePath);

            if (!File.Exists(binLogFilePath))
            {
                throw new FileNotFoundException(binLogFilePath);
            }

            if (binLogFilePath.EndsWith(".buildlog", StringComparison.OrdinalIgnoreCase))
            {
                return ReadBuildLogFormat(binLogFilePath);
            }

            var invocations = new List<CompilerInvocation>();
            var reader = new BinLogReader();
            var taskIdToInvocationMap = new Dictionary<(int, int), CompilerInvocation>();

            void TryGetInvocationFromEvent(object sender, BuildEventArgs args)
            {
                var invocation = TryGetInvocationFromRecord(args, taskIdToInvocationMap);
                if (invocation != null)
                {
                    invocations.Add(invocation);
                }
            }

            reader.TargetStarted += TryGetInvocationFromEvent;
            reader.MessageRaised += TryGetInvocationFromEvent;

            reader.Replay(binLogFilePath);

            return invocations;
        }

        public IEnumerable<CompilerInvocation> Read(Build build)
        {
            var invocations = new List<CompilerInvocation>();
            build.VisitAllChildren<Task>(t =>
            {
                var invocation = TryGetInvocationFromTask(t);
                if (invocation != null)
                {
                    invocations.Add(invocation);
                }
            });

            return invocations;
        }

        public IEnumerable<CompilerInvocation> ReadBuildLogFormat(string buildLogFilePath)
        {
            var build = Serialization.Read(buildLogFilePath);
            return Read(build);
        }

        public CompilerInvocation TryGetInvocationFromRecord(BuildEventArgs args, Dictionary<(int, int), CompilerInvocation> taskIdToInvocationMap)
        {
            int targetId = args.BuildEventContext?.TargetId ?? -1;
            int projectId = args.BuildEventContext?.ProjectInstanceId ?? -1;
            if (targetId < 0)
            {
                return null;
            }

            var targetStarted = args as TargetStartedEventArgs;
            if (targetStarted != null && string.Equals(targetStarted.TargetName, "CoreCompile", StringComparison.OrdinalIgnoreCase))
            {
                var invocation = new CompilerInvocation();
                taskIdToInvocationMap[(targetId, projectId)] = invocation;
                invocation.ProjectFilePath = targetStarted.ProjectFile;
                return null;
            }

            if (!(args is TaskCommandLineEventArgs taskCommandLineEventArgs))
            {
                return null;
            }

            var commandLine = GetCommandLineFromEventArgs(taskCommandLineEventArgs, out var language);
            if (commandLine == null)
            {
                return null;
            }

            if (taskIdToInvocationMap.TryGetValue((targetId, projectId), out CompilerInvocation compilerInvocation))
            {
                compilerInvocation.Language = language;
                compilerInvocation.CommandLineArguments = commandLine;
                taskIdToInvocationMap.Remove((targetId, projectId));
            }

            return compilerInvocation;
        }

        public string GetCommandLineFromEventArgs(TaskCommandLineEventArgs task, out string language)
        {
            string name = task.TaskName;
            language = GetLanguageFromTaskName(name);
            if (language == null)
            {
                return null;
            }

            var commandLine = task.CommandLine;
            commandLine = TrimCompilerExeFromCommandLine(commandLine, language);
            return commandLine;
        }

        public CompilerInvocation TryGetInvocationFromTask(Task task)
        {
            var name = task.Name;
            string language = GetLanguageFromTaskName(name);

            if (language == null || !string.Equals((task.Parent as Target)?.Name, "CoreCompile", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var commandLine = task.CommandLineArguments;
            if (commandLine == null)
            {
                // Fsc seems to have it null
                return null;
            }

            commandLine = TrimCompilerExeFromCommandLine(commandLine, language);

            return new CompilerInvocation
            {
                Language = language,
                CommandLineArguments = commandLine,
                ProjectFilePath = task.GetNearestParent<Project>()?.ProjectFile
            };
        }

        public static string TrimCompilerExeFromCommandLine(string commandLine, string language)
        {
            int occurrence;
            int length = 8;

            var compilerName = language switch
            {
                CompilerInvocation.CSharp => "csc",
                CompilerInvocation.VisualBasic => "vbc",
                CompilerInvocation.FSharp => "fsc",
                CompilerInvocation.TypeScript => "tsc",
                _ => null
            };

            if (compilerName == null)
            {
                return commandLine;
            }

            if (TryParse(compilerName))
            {
                commandLine = commandLine.Substring(occurrence + length);
            }

            bool TryParse(string compilerName)
            {
                occurrence = commandLine.IndexOf($"{compilerName}.exe ", StringComparison.OrdinalIgnoreCase);
                if (occurrence == -1)
                {
                    occurrence = commandLine.IndexOf($"{compilerName}.dll ", StringComparison.OrdinalIgnoreCase);
                }

                if (occurrence == -1)
                {
                    occurrence = commandLine.IndexOf($"{compilerName}.exe\" ", StringComparison.OrdinalIgnoreCase);
                    length = 9;
                }

                if (occurrence == -1)
                {
                    occurrence = commandLine.IndexOf($"{compilerName}.dll\" ", StringComparison.OrdinalIgnoreCase);
                    length = 9;
                }

                return occurrence >= 0;
            }

            return commandLine;
        }

        public static string GetLanguageFromTaskName(string name)
        {
            if (string.Equals(name, "Csc", StringComparison.OrdinalIgnoreCase))
            {
                return CompilerInvocation.CSharp;
            }
            else if (string.Equals(name, "Vbc", StringComparison.OrdinalIgnoreCase))
            {
                return CompilerInvocation.VisualBasic;
            }
            else if (string.Equals(name, "Fsc", StringComparison.OrdinalIgnoreCase))
            {
                return CompilerInvocation.FSharp;
            }
            else if (string.Equals(name, "Tsc", StringComparison.OrdinalIgnoreCase))
            {
                return CompilerInvocation.TypeScript;
            }
            else
            {
                return null;
            }
        }
    }
}
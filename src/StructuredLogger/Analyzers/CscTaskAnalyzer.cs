using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class CscTaskAnalyzer
    {
        public static (Folder Analyzers, Folder Generators) Analyze(Task task)
        {
            Folder analyzerReport = null;
            Folder generatorReport = null;
            Folder currentReport = null;
            Folder parent = null;

            foreach (var message in task.GetMessages().ToArray())
            {
                var text = message.Text;
                if (text.StartsWith(Strings.TotalAnalyzerExecutionTime, StringComparison.Ordinal))
                {
                    analyzerReport = new Folder();
                    analyzerReport.Name = Strings.AnalyzerReport;
                    task.AddChild(analyzerReport);
                    parent = analyzerReport;
                    currentReport = analyzerReport;
                }
                else if (text.StartsWith(Strings.TotalGeneratorExecutionTime, StringComparison.Ordinal))
                {
                    generatorReport = new Folder();
                    generatorReport.Name = Strings.GeneratorReport;
                    task.AddChild(generatorReport);
                    parent = generatorReport;
                    currentReport = generatorReport;
                }
                else if (text.Contains(", Version=") && currentReport != null)
                {
                    var lastAssembly = new Folder();
                    lastAssembly.Name = text;
                    currentReport.AddChild(lastAssembly);
                    parent = lastAssembly;

                    // Remove the message since we are already using the same text for the containing folder
                    message.Parent.Children.Remove(message);
                    continue;
                }
                else if (text.StartsWith("CompilerServer:", StringComparison.Ordinal))
                {
                    // The C# / VB compiler server emits diagnostic messages from the main build task. These 
                    // are not related to the analyzer performance summary and should not be included in this view
                    continue;
                }

                if (parent != null)
                {
                    message.Parent.Children.Remove(message);
                    parent.AddChild(message);
                }
            }

            return (analyzerReport, generatorReport);
        }

        public static void CreateMergedReport(Folder destination, Folder[] analyzerReports)
        {
            var assemblyData = new Dictionary<string, AnalyzerAssemblyData>();
            foreach (var report in analyzerReports)
            {
                foreach (var folder in report.Children.OfType<Folder>())
                {
                    var data = AnalyzerAssemblyData.FromFolder(folder);
                    if (assemblyData.TryGetValue(data.Name, out var existingData))
                    {
                        existingData.CombineWith(data);
                    }
                    else
                    {
                        assemblyData.Add(data.Name, data);
                    }
                }
            }

            foreach (var data in assemblyData.OrderByDescending(data => data.Value.TotalTime))
            {
                var folder = new Folder
                {
                    Name = $"{TextUtilities.DisplayDuration(data.Value.TotalTime, showZero: true)}   {data.Value.Name}"
                };

                foreach (var analyzer in data.Value.AnalyzerTimes
                    .OrderByDescending(analyzer => analyzer.Value)
                    .ThenBy(analyzer => analyzer.Key, StringComparer.OrdinalIgnoreCase))
                {
                    folder.AddChild(new Item
                    {
                        Text = analyzer.Key + " = " + TextUtilities.DisplayDuration(analyzer.Value, showZero: true)
                    });
                }

                destination.AddChild(folder);
            }
        }

        private sealed class AnalyzerAssemblyData
        {
            public readonly string Name;
            public TimeSpan TotalTime;
            public readonly Dictionary<string, TimeSpan> AnalyzerTimes = new Dictionary<string, TimeSpan>();

            public AnalyzerAssemblyData(string name, TimeSpan totalTime)
            {
                Name = name;
                TotalTime = totalTime;
            }

            public void CombineWith(AnalyzerAssemblyData other)
            {
                Debug.Assert(Name == other.Name);
                TotalTime += other.TotalTime;
                foreach (var pair in other.AnalyzerTimes)
                {
                    _ = AnalyzerTimes.TryGetValue(pair.Key, out var existingTime);
                    AnalyzerTimes[pair.Key] = existingTime + pair.Value;
                }
            }

            public static AnalyzerAssemblyData FromFolder(Folder folder)
            {
                var (assemblyName, assemblyTime) = ParseLine(folder.Name);
                var data = new AnalyzerAssemblyData(assemblyName, assemblyTime);
                foreach (var message in folder.Children.OfType<Message>())
                {
                    var (analyzerName, analyzerTime) = ParseLine(message.Text);
                    data.AnalyzerTimes[analyzerName] = analyzerTime;
                }

                return data;

                static (string name, TimeSpan time) ParseLine(string line)
                {
                    var columns = line.Split(twoSpaces, StringSplitOptions.RemoveEmptyEntries);
                    if (columns.Length != 3)
                    {
                        // The string wasn't in the format we expect
                        return (line, TimeSpan.Zero);
                    }

                    if (!double.TryParse(columns[0].Trim(), NumberStyles.Number, CultureInfo.GetCultureInfo(Strings.ResourceSet.Culture), out var totalTimeSeconds))
                    {
                        totalTimeSeconds = 0;
                    }

                    return (columns[2].Trim(), TimeSpan.FromSeconds(totalTimeSeconds));
                }
            }

            private static readonly string[] twoSpaces = new[] { "  " };
        }
    }
}

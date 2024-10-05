using System;
using System.Collections.Generic;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class BuildStatistics
    {
        public int Tasks;

        public Dictionary<string, List<string>> TaskParameterMessagesByTask = new();
        public Dictionary<string, List<string>> OutputItemMessagesByTask = new();

        public int TimedNodeCount { get; set; }

        public void ReportTaskParameterMessage(Task task, string message)
        {
            Add(task.Name, message, TaskParameterMessagesByTask);
        }

        public void ReportOutputItemMessage(Task task, string message)
        {
            Add(task.Name, message, OutputItemMessagesByTask);
        }

        public void Add(string key, string value, Dictionary<string, List<string>> dictionary)
        {
            if (!dictionary.TryGetValue(key, out var bucket))
            {
                bucket = new List<string>();
                dictionary[key] = bucket;
            }

            bucket.Add(value);
        }
    }
}

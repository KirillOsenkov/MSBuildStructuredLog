using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.Build.Logging.StructuredLogger;

namespace BinlogTool
{
    public class ListProperties : BinlogToolCommandBase
    {
        private Dictionary<string, HashSet<string>> propertyValues = new(StringComparer.OrdinalIgnoreCase);

        public void Run(string binLogFilePath)
        {
            var records = BinaryLog.ReadRecords(binLogFilePath);
            foreach (var record in records)
            {
                var args = record.Args;
                if (args is ProjectEvaluationFinishedEventArgs evaluationFinished)
                {
                    VisitProperties(record, evaluationFinished.Properties);
                }
                else if (args is ProjectStartedEventArgs projectStarted)
                {
                    VisitProperties(record, projectStarted.Properties);
                }
            }

            Print();
        }

        private void Print()
        {
            foreach (var property in propertyValues.OrderBy(kvp => kvp.Key))
            {
                var values = property.Value;
                foreach (var value in values.OrderBy(s => s))
                {
                    Console.WriteLine($"{property.Key}={value}");
                }
            }
        }

        private void VisitProperties(Record record, IEnumerable properties)
        {
            if (properties == null)
            {
                return;
            }

            foreach (var kvp in properties)
            {
                if (kvp is KeyValuePair<string, string> keyValuePair)
                {
                    VisitProperty(record, keyValuePair.Key, keyValuePair.Value);
                }
                else if (kvp is DictionaryEntry entry)
                {
                    VisitProperty(record, Convert.ToString(entry.Key), Convert.ToString(entry.Value));
                }
            }
        }

        private void VisitProperty(Record record, string key, string value)
        {
            if (!propertyValues.TryGetValue(key, out var bucket))
            {
                bucket = [];
                propertyValues[key] = bucket;
            }

            bucket.Add(value);
        }
    }
}

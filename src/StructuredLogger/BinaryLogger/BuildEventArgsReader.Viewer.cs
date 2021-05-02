using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Collections;

namespace Microsoft.Build.Logging.StructuredLogger
{
    partial class BuildEventArgsReader
    {
        public event Action<string> OnStringRead;

        public event Action<IDictionary<string, string>> OnNameValueListRead;

        public IEnumerable<string> GetStrings()
        {
            return stringRecords.OfType<string>().ToArray();
        }

        private struct NameValueRecord
        {
            public (int keyIndex, int valueIndex)[] Array;
            public IDictionary<string, string> Dictionary;
        }

        private IDictionary<string, string> CreateDictionary((int keyIndex, int valueIndex)[] list)
        {
            var dictionary = new ArrayDictionary<string, string>(list.Length);
            for (int i = 0; i < list.Length; i++)
            {
                string key = GetStringFromRecord(list[i].keyIndex);
                string value = GetStringFromRecord(list[i].valueIndex);
                if (key != null)
                {
                    dictionary.Add(key, value);
                }
            }

            return dictionary;
        }

        private string GetProjectStartedMessage(string projectFile, string targetNames)
        {
            string projectFilePath = Path.GetFileName(projectFile);

            // Check to see if the there are any specific target names to be built.
            // If targetNames is null or empty then we will be building with the 
            // default targets.
            if (!string.IsNullOrEmpty(targetNames))
            {
                return FormatResourceStringIgnoreCodeAndKeyword("Project \"{0}\" ({1} target(s)):", projectFilePath, targetNames);
            }
            else
            {
                return FormatResourceStringIgnoreCodeAndKeyword("Project \"{0}\" (default targets):", projectFilePath);
            }
        }

        private string GetProjectFinishedMessage(bool succeeded, string projectFile)
        {
            return FormatResourceStringIgnoreCodeAndKeyword(succeeded ? "Done building project \"{0}\"." : "Done building project \"{0}\" -- FAILED.", Path.GetFileName(projectFile));
        }

        private string GetPropertyReassignmentMessage(string propertyName, string newValue, string previousValue, string location)
        {
            return FormatResourceStringIgnoreCodeAndKeyword("Property reassignment: $({0})=\"{1}\" (previous value: \"{2}\") at {3}", propertyName, newValue, previousValue, location);
        }

        private string GetTargetStartedMessage(string projectFile, string targetFile, string parentTarget, string targetName)
        {
            if (string.Equals(projectFile, targetFile, StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(parentTarget))
                {
                    return FormatResourceStringIgnoreCodeAndKeyword("Target \"{0}\" in project \"{1}\" (target \"{2}\" depends on it):", targetName, projectFile, parentTarget);
                }
                else
                {
                    return FormatResourceStringIgnoreCodeAndKeyword("Target \"{0}\" in project \"{1}\" (entry point):", targetName, projectFile);
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(parentTarget))
                {
                    return FormatResourceStringIgnoreCodeAndKeyword("Target \"{0}\" in file \"{1}\" from project \"{2}\" (target \"{3}\" depends on it):", targetName, targetFile, projectFile, parentTarget);
                }
                else
                {
                    return FormatResourceStringIgnoreCodeAndKeyword("Target \"{0}\" in file \"{1}\" from project \"{2}\" (entry point):", targetName, targetFile, projectFile);
                }
            }
        }

        private string GetTargetFinishedMessage(string projectFile, string targetName, bool succeeded)
        {
            return FormatResourceStringIgnoreCodeAndKeyword(
                succeeded ?
                "Done building target \"{0}\" in project \"{1}\"." : "Done building target \"{0}\" in project \"{1}\" -- FAILED.",
                targetName,
                Path.GetFileName(projectFile));
        }

        private string GetTargetSkippedMessage(string targetName, string condition, string evaluatedCondition, bool originallySucceeded)
        {
            if (condition != null)
            {
                return FormatResourceStringIgnoreCodeAndKeyword(
                    "Target \"{0}\" skipped, due to false condition; ({1}) was evaluated as ({2}).",
                    targetName,
                    condition,
                    evaluatedCondition);
            }
            else
            {
                return FormatResourceStringIgnoreCodeAndKeyword(
                    originallySucceeded
                    ? "Target \"{0}\" skipped. Previously built successfully."
                    : "Target \"{0}\" skipped. Previously built unsuccessfully.",
                    targetName);
            }
        }

        private string GetTaskStartedMessage(string taskName)
        {
            return FormatResourceStringIgnoreCodeAndKeyword("Task \"{0}\"", taskName);
        }

        private string GetTaskFinishedMessage(bool succeeded, string taskName)
        {
            return FormatResourceStringIgnoreCodeAndKeyword(succeeded ? "Done executing task \"{0}\"." : "Done executing task \"{0}\" -- FAILED.", taskName);
        }

        internal static string FormatResourceStringIgnoreCodeAndKeyword(string resource, params string[] arguments)
        {
            return string.Format(resource, arguments);
        }
    }
}
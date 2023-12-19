using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Collections;
using Microsoft.Build.Framework;

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
            public IDictionary<string, string> Dictionary;
        }

        private IDictionary<string, string> CreateDictionary(List<(int keyIndex, int valueIndex)> list)
        {
            var dictionary = new ArrayDictionary<string, string>(list.Count);
            for (int i = 0; i < list.Count; i++)
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

        private readonly Dictionary<(string, string, string, string), string> propertyReassignmentCache =
            new Dictionary<(string, string, string, string), string>();

        private string GetPropertyReassignmentMessage(string propertyName, string newValue, string previousValue, string location)
        {
            var key = (propertyName, newValue, previousValue, location);
            if (!propertyReassignmentCache.TryGetValue(key, out var result))
            {
                result = FormatResourceStringIgnoreCodeAndKeyword(Strings.PropertyReassignment, propertyName, newValue, previousValue, location);
                propertyReassignmentCache[key] = result;
            }

            return result;
        }

        private BuildEventArgs SynthesizePropertyReassignment(BuildEventArgsFields fields)
        {
            string propertyName = fields.Arguments[0] as string;
            string newValue = fields.Arguments[1] as string;
            string previousValue = fields.Arguments[2] as string;
            string location = fields.Arguments[3] as string;
            string message = GetPropertyReassignmentMessage(propertyName, newValue, previousValue, location);

            var e = new PropertyReassignmentEventArgs(
                propertyName,
                previousValue,
                newValue,
                location,
                message,
                fields.HelpKeyword,
                fields.SenderName,
                fields.Importance);
            SetCommonFields(e, fields);

            return e;
        }

        private bool sawCulture;

        private void OnMessageRead(BuildMessageEventArgs args)
        {
            if (sawCulture)
            {
                return;
            }

            if (args.SenderName == "BinaryLogger" &&
                args.Message is string message &&
                message.StartsWith("CurrentUICulture", StringComparison.Ordinal))
            {
                sawCulture = true;
                var kvp = TextUtilities.ParseNameValue(message);
                string culture = kvp.Value;
                if (!string.IsNullOrEmpty(culture))
                {
                    Strings.Initialize(culture);
                }
            }
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

        private string GetTargetSkippedMessage(TargetSkipReason skipReason, string targetName, string condition, string evaluatedCondition, bool originallySucceeded)
        {
            return skipReason switch
            {
                TargetSkipReason.PreviouslyBuiltSuccessfully or TargetSkipReason.PreviouslyBuiltUnsuccessfully =>
                    FormatResourceStringIgnoreCodeAndKeyword(
                        originallySucceeded
                        ? "Target \"{0}\" skipped. Previously built successfully."
                        : "Target \"{0}\" skipped. Previously built unsuccessfully.",
                        targetName),

                TargetSkipReason.ConditionWasFalse =>
                    FormatResourceStringIgnoreCodeAndKeyword(
                        "Target \"{0}\" skipped, due to false condition; ({1}) was evaluated as ({2}).",
                        targetName,
                        condition,
                        evaluatedCondition),

                TargetSkipReason.OutputsUpToDate =>
                    FormatResourceStringIgnoreCodeAndKeyword(
                        "Skipping target \"{0}\" because all output files are up-to-date with respect to the input files.",
                        targetName),

                _ => $"Target {targetName} was skipped for unknown reason: {skipReason}"
            };
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
using System;
using System.Collections.Generic;
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

        private string GetTargetSkippedMessage(string targetName, string condition, string evaluatedCondition, bool originallySucceeded)
        {
            if (condition != null)
            {
                return FormatResourceStringIgnoreCodeAndKeyword(
                    Strings.TargetSkippedFalseCondition,
                    targetName,
                    condition,
                    evaluatedCondition);
            }
            else
            {
                return FormatResourceStringIgnoreCodeAndKeyword(
                    originallySucceeded
                    ? Strings.TargetAlreadyCompleteSuccess
                    : Strings.TargetAlreadyCompleteFailure,
                    targetName);
            }
        }

        internal static string FormatResourceStringIgnoreCodeAndKeyword(string resource, params string[] arguments)
        {
            return string.Format(resource, arguments);
        }
    }
}
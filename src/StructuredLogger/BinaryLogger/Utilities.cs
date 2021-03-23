using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Build.Framework;

namespace Microsoft.Build.BackEnd
{
    internal class ItemGroupLoggingHelper
    {
        internal static TaskParameterEventArgs CreateTaskParameterEventArgs(
            BuildEventContext buildEventContext,
            TaskParameterMessageKind messageKind,
            string itemType,
            IList items,
            bool logItemMetadata,
            DateTime timestamp)
        {
            var args = new TaskParameterEventArgs(
                messageKind,
                itemType,
                items,
                logItemMetadata,
                timestamp);
            args.BuildEventContext = buildEventContext;
            return args;
        }
    }
}

namespace Microsoft.Build.Internal
{
    internal static class Utilities
    {
        public static void EnumerateProperties(IEnumerable properties, Action<KeyValuePair<string, string>> callback)
        {
            if (properties == null)
            {
                return;
            }

            //if (properties is PropertyDictionary<ProjectPropertyInstance> propertyDictionary)
            //{
            //    propertyDictionary.Enumerate((key, value) =>
            //    {
            //        callback(new KeyValuePair<string, string>(key, value));
            //    });
            //}
            //else
            {
                foreach (var item in properties)
                {
                    if (item is DictionaryEntry dictionaryEntry && dictionaryEntry.Key is string key && !string.IsNullOrEmpty(key))
                    {
                        callback(new KeyValuePair<string, string>(key, dictionaryEntry.Value as string ?? string.Empty));
                    }
                    else if (item is KeyValuePair<string, string> kvp)
                    {
                        callback(kvp);
                    }
                    else
                    {
                    }
                }
            }
        }

        public static void EnumerateItems(IEnumerable items, Action<DictionaryEntry> callback)
        {
            foreach (var item in items)
            {
                string itemType = default;
                object itemValue = null;

                if (item is DictionaryEntry dictionaryEntry)
                {
                    itemType = dictionaryEntry.Key as string;
                    itemValue = dictionaryEntry.Value;
                }

                if (string.IsNullOrEmpty(itemType))
                {
                    continue;
                }

                callback(new DictionaryEntry(itemType, itemValue));
            }
        }

        public static IEnumerable<KeyValuePair<string, string>> EnumerateMetadata(this ITaskItem taskItem)
        {
            // This runs if ITaskItem is Microsoft.Build.Utilities.TaskItem from Microsoft.Build.Utilities.v4.0.dll
            // that is loaded from the GAC.
            IDictionary customMetadata = taskItem.CloneCustomMetadata();
            if (customMetadata is IEnumerable<KeyValuePair<string, string>> enumerableMetadata)
            {
                return enumerableMetadata;
            }

            // In theory this should never be reachable.
            var list = new KeyValuePair<string, string>[customMetadata.Count];
            int i = 0;

            foreach (string metadataName in customMetadata.Keys)
            {
                string valueOrError;

                try
                {
                    valueOrError = taskItem.GetMetadata(metadataName);
                }
                // Temporarily try catch all to mitigate frequent NullReferenceExceptions in
                // the logging code until CopyOnWritePropertyDictionary is replaced with
                // ImmutableDictionary. Calling into Debug.Fail to crash the process in case
                // the exception occurres in Debug builds.
                catch (Exception e)
                {
                    valueOrError = e.Message;
                }

                list[i] = new KeyValuePair<string, string>(metadataName, valueOrError);
                i += 1;
            }

            return list;
        }
    }
}
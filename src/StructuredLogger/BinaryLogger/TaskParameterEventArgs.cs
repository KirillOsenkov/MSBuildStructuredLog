// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Build.Logging.StructuredLogger;

namespace Microsoft.Build.Framework
{
    public enum TaskParameterMessageKind
    {
        TaskInput,
        TaskOutput,
        AddItem,
        RemoveItem,
    }

    /// <summary>
    /// This class is used by tasks to log their parameters (input, output).
    /// The intrinsic ItemGroupIntrinsicTask to add or remove items also
    /// uses this class.
    /// </summary>
    public class TaskParameterEventArgs : BuildMessageEventArgs
    {
        /// <summary>
        /// Default (family) constructor.
        /// </summary>
        protected TaskParameterEventArgs()
            : base()
        {
        }

        /// <summary>
        /// Creates an instance of this class for the given task parameter.
        /// </summary>
        public TaskParameterEventArgs
        (
            TaskParameterMessageKind kind,
            string itemName,
            IList items,
            bool logItemMetadata,
            DateTime eventTimestamp,
            Func<TaskParameterEventArgs, string> messageGetter
        )
            : base("", null, "", MessageImportance.Low, eventTimestamp)
        {
            Kind = kind;
            ItemName = itemName;
            Items = items;
            LogItemMetadata = logItemMetadata;
            this.messageGetter = messageGetter;
        }

        public TaskParameterMessageKind Kind { get; private set; }
        public string ItemName { get; private set; }
        public IList Items { get; private set; }
        public bool LogItemMetadata { get; private set; }

        private readonly Func<TaskParameterEventArgs, string> messageGetter;

        private IList ReadItems(BinaryReader reader)
        {
            var list = new ArrayList();

            int count = reader.Read7BitEncodedInt();
            for (int i = 0; i < count; i++)
            {
                var item = ReadItem(reader);
                list.Add(item);
            }

            return list;
        }

        private object ReadItem(BinaryReader reader)
        {
            string itemSpec = reader.ReadString();
            int metadataCount = reader.Read7BitEncodedInt();
            if (metadataCount == 0)
            {
                return new TaskItemData(itemSpec, metadata: null);
            }

            var metadata = new Dictionary<string, string>();
            for (int i = 0; i < metadataCount; i++)
            {
                string key = reader.ReadString();
                string value = reader.ReadString();
                if (key != null)
                {
                    metadata[key] = value;
                }
            }

            var taskItem = new TaskItemData(itemSpec, metadata);
            return taskItem;
        }

        private void WriteItems(BinaryWriter writer, IList items)
        {
            if (items == null)
            {
                writer.Write7BitEncodedInt(0);
                return;
            }

            int count = items.Count;
            writer.Write7BitEncodedInt(count);

            for (int i = 0; i < count; i++)
            {
                var item = items[i];
                WriteItem(writer, item);
            }
        }

        private void WriteItem(BinaryWriter writer, object item)
        {
            if (item is ITaskItem taskItem)
            {
                writer.Write(taskItem.ItemSpec);
                WriteMetadata(writer, taskItem.CloneCustomMetadata());
            }
            else // string or ValueType
            {
                writer.Write(item?.ToString() ?? "");
                writer.Write7BitEncodedInt(0);
            }
        }

        private void WriteMetadata(BinaryWriter writer, IDictionary metadata)
        {
            writer.Write7BitEncodedInt(metadata.Count);
            if (metadata.Count == 0)
            {
                return;
            }

            foreach (var key in metadata.Keys)
            {
                string valueOrError;

                try
                {
                    valueOrError = metadata[key] as string;
                }
                // Temporarily try catch all to mitigate frequent NullReferenceExceptions in
                // the logging code until CopyOnWritePropertyDictionary is replaced with
                // ImmutableDictionary. Calling into Debug.Fail to crash the process in case
                // the exception occures in Debug builds.
                catch (Exception e)
                {
                    valueOrError = e.Message;
                    Debug.Fail(e.ToString());
                }

                if (key is string keyString && valueOrError is string value)
                {
                    writer.Write(keyString);
                    writer.Write(value);
                }
                else
                {
                    writer.Write("");
                    writer.Write("");
                }
            }
        }

        public override string Message
        {
            get
            {
                lock (this)
                {
                    if (base.Message == null)
                    {
                        base.Message = messageGetter(this);
                    }

                    return base.Message;
                }
            }
        }
    }
}

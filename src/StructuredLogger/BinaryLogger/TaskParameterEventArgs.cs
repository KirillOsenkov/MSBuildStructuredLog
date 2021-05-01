// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Build.Framework
{
    public enum TaskParameterMessageKind
    {
        TaskInput,
        TaskOutput,
        AddItem,
        RemoveItem,
        SkippedTargetInputs,
        SkippedTargetOutputs
    }

    /// <summary>
    /// This class is used by tasks to log their parameters (input, output).
    /// The intrinsic ItemGroupIntrinsicTask to add or remove items also
    /// uses this class.
    /// </summary>
    public class TaskParameterEventArgs : BuildMessageEventArgs
    {
        /// <summary>
        /// Creates an instance of this class for the given task parameter.
        /// </summary>
        public TaskParameterEventArgs
        (
            TaskParameterMessageKind kind,
            string itemType,
            IList items,
            bool logItemMetadata,
            DateTime eventTimestamp,
            int line,
            int column
        )
            : base(
                  subcategory: null,
                  code: null,
                  file: null,
                  lineNumber: line,
                  columnNumber: column,
                  endLineNumber: 0,
                  endColumnNumber: 0,
                  message: null,
                  helpKeyword: null,
                  senderName: null,
                  MessageImportance.Low,
                  eventTimestamp)
        {
            Kind = kind;
            ItemType = itemType;
            Items = items;
            LogItemMetadata = logItemMetadata;
        }

        public TaskParameterMessageKind Kind { get; private set; }
        public string ItemType { get; private set; }
        public IList Items { get; private set; }
        public bool LogItemMetadata { get; private set; }

        /// <summary>
        /// The <see cref="TaskParameterEventArgs"/> type is declared in Microsoft.Build.Framework.dll
        /// which is a declarations assembly. The logic to realize the Message is in Microsoft.Build.dll
        /// which is an implementations assembly. This seems like the easiest way to inject the
        /// implementation for realizing the Message.
        /// </summary>
        /// <remarks>
        /// Note that the current implementation never runs and is provided merely
        /// as a safeguard in case MessageGetter isn't set for some reason.
        /// </remarks>
        internal static Func<TaskParameterEventArgs, string> MessageGetter = args =>
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{args.Kind}: {args.ItemType}");
            foreach (var item in args.Items)
            {
                sb.AppendLine(item.ToString());
            }

            return sb.ToString();
        };

        /// <summary>
        /// Provides a way for Microsoft.Build.dll to provide a more efficient dictionary factory
        /// (using ArrayDictionary`2). Since that is an implementation detail, it is not included
        /// in Microsoft.Build.Framework.dll so we need this extensibility point here.
        /// </summary>
        internal static Func<int, IDictionary<string, string>> DictionaryFactory = capacity => new Dictionary<string, string>(capacity);

        public override string Message
        {
            get
            {
                lock (this)
                {
                    if (base.Message == null)
                    {
                        base.Message = MessageGetter(this);
                    }

                    return base.Message;
                }
            }
        }
    }
}

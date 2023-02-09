﻿using System;
using System.Text.RegularExpressions;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class TimedMessage : Message
    {
        /// <summary>
        /// Timestamp of the message
        /// </summary>
        public override DateTime Timestamp { get; set; }
    }

    public class Message : TextNode, IHasRelevance, IHasSourceFile, IHasLineNumber
    {
        /// <summary>
        /// Let's see if we even need timestamp, it's just eating memory for now
        /// </summary>
        public virtual DateTime Timestamp { get { return DateTime.MinValue; } set { } }

        public override string TypeName => nameof(Message);

        public override string LookupKey => Text;

        public bool IsLowRelevance
        {
            get => HasFlag(NodeFlags.LowRelevance) && !IsSelected;
            set => SetFlag(NodeFlags.LowRelevance, value);
        }

        public string SourceFilePath
        {
            get
            {
                var match = GetSourceFileMatch();
                if (match != null && match.Success)
                {
                    return match.Groups["File"].Value;
                }

                return null;
            }
        }

        private Match GetSourceFileMatch()
        {
            var match = Strings.PropertyReassignmentRegex.Match(Text);
            if (match.Success)
            {
                return match;
            }

            match = Strings.MessageIncludedResponseFile.Match(Text);
            if (match.Success)
            {
                return match;
            }

            return null;
        }

        // These are recalculated and not stored because storage in this class is incredibly expensive
        // There are millions of Message instances in a decent size log
        public int? LineNumber
        {
            get
            {
                var match = GetSourceFileMatch();
                if (match != null && match.Success)
                {
                    var value = match.Groups["Line"].Value;
                    if (int.TryParse(value, out int result))
                    {
                        return result;
                    }
                }

                return null;
            }
        }
    }
}

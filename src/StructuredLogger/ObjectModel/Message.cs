using System;
using System.Text.RegularExpressions;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class Message : TextNode, IHasRelevance, IHasSourceFile, IHasLineNumber
    {
        /// <summary>
        /// Let's see if we even need timestamp, it's just eating memory for now
        /// </summary>
        public DateTime Timestamp { get { return DateTime.MinValue; } set { } }

        public override string LookupKey => Text;

        private bool isLowRelevance = false;
        public bool IsLowRelevance
        {
            get
            {
                return isLowRelevance && !IsSelected;
            }

            set
            {
                if (isLowRelevance == value)
                {
                    return;
                }

                isLowRelevance = value;
                RaisePropertyChanged();
            }
        }

        private static readonly Regex propertyReassignment = new Regex(@"^Property reassignment: \$\(\w+\)=.+ \(previous value: .*\) at (?<File>.*) \((?<Line>\d+),(\d+)\)$", RegexOptions.Compiled);

        public string SourceFilePath
        {
            get
            {
                var match = GetSourceFileMatch();
                if (match.Success)
                {
                    return match.Groups["File"].Value;
                }

                return null;
            }
        }

        private Match GetSourceFileMatch()
        {
            return propertyReassignment.Match(Text);
        }

        // These are recalculated and not stored because storage in this class is incredibly expensive
        // There are millions of Message instances in a decent size log
        public int? LineNumber
        {
            get
            {
                var match = GetSourceFileMatch();
                if (match.Success)
                {
                    return int.Parse(match.Groups["Line"].Value);
                }

                return null;
            }
        }
    }
}

using System.IO;
using Microsoft.Build.Framework.Profiler;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class EvaluationProfileEntry : TreeNode, IHasSourceFile, IHasLineNumber
    {
        public string ElementName { get; set; }
        public string ElementDescription { get; set; }
        public string EvaluationPassDescription { get; set; }
        public EvaluationLocationKind Kind { get; set; }
        public string SourceFilePath { get; set; }
        public int? LineNumber { get; set; }
        public string NumberOfHits => ProfiledLocation.NumberOfHits > 0 ? ProfiledLocation.NumberOfHits.ToString() : "";

        public override string TypeName => nameof(EvaluationProfileEntry);

        public ProfiledLocation ProfiledLocation { get; private set; }

        public void AddEntry(ProfiledLocation result)
        {
            ProfiledLocation = result;
        }

        public string DurationText
        {
            get
            {
                if (ProfiledLocation.InclusiveTime.TotalMilliseconds == 0)
                {
                    return "";
                }

                var result = TextUtilities.DisplayDuration(ProfiledLocation.InclusiveTime);

                var hits = ProfiledLocation.NumberOfHits;
                if (hits == 1)
                {
                    result += " (1 hit)";
                }
                else if (hits > 1)
                {
                    result += " (" + hits + " hits)";
                }

                return result;
            }
        }

        public string FileName => Path.GetFileName(SourceFilePath);

        public override string Title
        {
            get
            {
                if (SourceFilePath == null)
                {
                    return EvaluationPassDescription;
                }

                if (Kind == EvaluationLocationKind.Element)
                {
                    return $"{FileName}:{LineNumber}";
                }

                return $"{FileName}:{LineNumber} {Kind}";
            }
        }

        public string ShortenedElementDescription => TextUtilities.ShortenValue(ElementDescription, "...", maxChars: 80);

        public double Value { get; set; }

        public override string ToString()
        {
            return $"{Title}";
        }
    }
}

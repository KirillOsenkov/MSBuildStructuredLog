using System.Collections.Generic;
using StructuredLogViewer;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class ProxyNode : TextNode
    {
        public object Original { get; set; }

        public SearchResult SearchResult { get; set; }

        private List<object> highlights;
        public List<object> Highlights
        {
            get
            {
                if (highlights == null)
                {
                    highlights = new List<object>();
                    Populate(SearchResult);
                }

                return highlights;
            }
        }

        public void Populate(SearchResult result)
        {
            if (result == null)
            {
                return;
            }

            if (result.MatchedByType && result.WordsInFields.Count == 0)
            {
                Highlights.Add(new HighlightedText { Text = OriginalType });
                Highlights.Add(" " + TextUtilities.ShortenValue(result.Node.ToString(), "..."));

                AddDuration(result);

                return;
            }

            Highlights.Add(OriginalType);

            foreach (var kvp in result.WordsInFields)
            {
                Highlights.Add(" ");

                var fieldText = kvp.Key;
                fieldText = TextUtilities.ShortenValue(fieldText, "...");

                var highlightSpans = TextUtilities.GetHighlightedSpansInText(fieldText, kvp.Value);
                int index = 0;
                foreach (var span in highlightSpans)
                {
                    if (span.Start > index)
                    {
                        Highlights.Add(fieldText.Substring(index, span.Start - index));
                    }

                    Highlights.Add(new HighlightedText { Text = fieldText.Substring(span.Start, span.Length) });
                    index = span.End;
                }

                if (index < fieldText.Length)
                {
                    Highlights.Add(fieldText.Substring(index, fieldText.Length - index));
                }
            }

            AddDuration(result);
        }

        private void AddDuration(SearchResult result)
        {
            if (result.Duration != default)
            {
                Highlights.Add(new HighlightedText { Text = TextUtilities.DisplayDuration(result.Duration) });
            }
        }

        public string OriginalType => Original.GetType().Name;
        public string ProjectExtension => Original is Project ? GetProjectFileExtension() : null;

        private string GetProjectFileExtension()
        {
            var result = ((Project)Original).ProjectFileExtension;
            if (result != ".sln" && result != ".csproj")
            {
                result = "other";
            }

            return result;
        }

        public override string TypeName => nameof(ProxyNode);

        public override string ToString() => Original.ToString();
    }
}

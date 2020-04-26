using System.Collections.Generic;
using System.Linq;
using StructuredLogViewer;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class ProxyNode : TextNode
    {
        public BaseNode Original { get; set; }

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

            foreach (var wordsInField in result.WordsInFields.GroupBy(t => t.field, t => t.match))
            {
                Highlights.Add(" ");

                var fieldText = wordsInField.Key;
                
                //NameValueNode is speial case: have to show name=value when seached only in one (name or value)
                var named = SearchResult.Node as NameValueNode;
                if (named != null && wordsInField.Key.Equals(named.Value))
                {                    
                        Highlights.Add(named.Name + " = ");
                }

                fieldText = TextUtilities.ShortenValue(fieldText, "...");

                var highlightSpans = TextUtilities.GetHighlightedSpansInText(fieldText, wordsInField);
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

                //NameValueNode is speial case: have to show name=value when seached only in one (name or value)
                if (named != null && wordsInField.Key.Equals(named.Name))
                {
                    Highlights.Add(" = " + TextUtilities.ShortenValue(named.Value, "..."));
                }
            }

            AddDuration(result);
        }

        private void AddDuration(SearchResult result)
        {
            if (result.Duration != default)
            {
                Highlights.Add(new HighlightedText { Text = " " + TextUtilities.DisplayDuration(result.Duration) });
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

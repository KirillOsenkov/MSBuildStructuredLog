using System.Collections.Generic;
using System.Linq;
using System.Text;
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

        public string GetNodeText(BaseNode node)
        {
            if (node is Target t)
            {
                return t.Name;
            }

            return node.ToString();
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
                Highlights.Add(" " + TextUtilities.ShortenValue(GetNodeText(result.Node), "..."));

                AddDuration(result);

                return;
            }

            Highlights.Add(OriginalType);

            // NameValueNode is special case: have to show name=value when searched only in one (name or value)
            var named = SearchResult.Node as NameValueNode;
            bool nameFound = false;
            bool valueFound = false;
            if (named != null)
            {
                foreach (var fieldText in result.WordsInFields.GroupBy(t => t.field))
                {
                    if (fieldText.Key.Equals(named.Name))
                    {
                        nameFound = true;
                    }

                    if (fieldText.Key.Equals(named.Value))
                    {
                        valueFound = true;
                    }
                }
            }

            foreach (var wordsInField in result.WordsInFields.GroupBy(t => t.field, t => t.match))
            {
                Highlights.Add(" ");

                var fieldText = wordsInField.Key;

                if (named != null && wordsInField.Key.Equals(named.Value) && !nameFound)
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

                if (named != null && wordsInField.Key.Equals(named.Name))
                {
                    if (!valueFound)
                    {
                        Highlights.Add(" = " + TextUtilities.ShortenValue(named.Value, "..."));
                    }
                    else
                    {
                        Highlights.Add(" = ");
                    }
                }
            }

            AddDuration(result);
        }

        private void AddDuration(SearchResult result)
        {
            if (result.StartTime != default)
            {
                Highlights.Add(new HighlightedText { Text = " " + TextUtilities.Display(result.StartTime), Style = "time" });
            }

            if (result.EndTime != default)
            {
                Highlights.Add(new HighlightedText { Text = " " + TextUtilities.Display(result.EndTime), Style = "time" });
            }

            if (result.Duration != default)
            {
                Highlights.Add(new HighlightedText { Text = " " + TextUtilities.DisplayDuration(result.Duration), Style = "time" });
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

        public override string ToString()
        {
            var sb = new StringBuilder();

            foreach (var highlight in Highlights)
            {
                sb.Append(highlight.ToString());
            }

            return sb.ToString();
        }
    }
}

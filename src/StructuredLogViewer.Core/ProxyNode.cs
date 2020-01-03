using System.Collections.Generic;
using StructuredLogViewer;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class ProxyNode : TextNode
    {
        public object Original { get; set; }

        public List<object> Highlights { get; set; } = new List<object>();

        public void Populate(SearchResult result)
        {
            if (result.MatchedByType && result.Before == null)
            {
                Highlights.Add(new HighlightedText { Text = OriginalType });
                Highlights.Add(" " + TextUtilities.ShortenValue(result.Node.ToString(), "..."));

                AddDuration(result);

                return;
            }

            Highlights.Add(OriginalType + " ");

            Highlights.Add(result.Before);

            if (result.Highlighted != null)
            {
                Highlights.Add(new HighlightedText { Text = result.Highlighted });
            }

            if (result.After != null)
            {
                Highlights.Add(result.After);
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

        public override string ToString() => Original.ToString();
    }
}

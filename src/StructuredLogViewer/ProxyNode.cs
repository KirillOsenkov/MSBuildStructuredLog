using System.Collections.Generic;
using StructuredLogViewer;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class ProxyNode : TextNode
    {
        private object original;
        public object Original
        {
            get
            {
                return original;
            }

            set
            {
                original = value;
            }
        }

        public List<object> Highlights { get; set; } = new List<object>();

        public void Populate(SearchResult result)
        {
            if (result.MatchedByType && result.Before == null)
            {
                Highlights.Add(new HighlightedText { Text = OriginalType });
                Highlights.Add(" " + Utilities.ShortenValue(result.Node.ToString(), "..."));
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

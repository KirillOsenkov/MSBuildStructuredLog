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
                Name = OriginalType;
                Text = Original.ToString();
            }
        }

        public List<object> Highlights { get; set; }

        public void Populate(SearchResult result)
        {
            Highlights = new List<object>();

            Highlights.Add(result.Before);
            Highlights.Add(new HighlightedText { Text = result.Highlighted });
            Highlights.Add(result.After);
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
    }
}

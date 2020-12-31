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
            else if (node is Project project)
            {
                return $"{project.Name}{project.TargetsDisplayText}";
            }
            else if (node is ProjectEvaluation evaluation)
            {
                return $"{evaluation.Name}";
            }

            return node.ToString();
        }

        public void Populate(SearchResult result)
        {
            if (result == null)
            {
                return;
            }

            var node = result.Node;

            if (result.MatchedByType && result.WordsInFields.Count == 0)
            {
                Highlights.Add(new HighlightedText { Text = OriginalType });
                Highlights.Add(" " + TextUtilities.ShortenValue(GetNodeText(node), "..."));

                AddDuration(result);

                return;
            }

            Highlights.Add(OriginalType);

            // NameValueNode is special case: have to show name=value when searched only in one (name or value)
            var nameValueNode = node as NameValueNode;
            bool nameFound = false;
            bool valueFound = false;
            if (nameValueNode != null)
            {
                foreach (var fieldText in result.WordsInFields)
                {
                    if (fieldText.field.Equals(nameValueNode.Name))
                    {
                        nameFound = true;
                    }

                    if (fieldText.field.Equals(nameValueNode.Value))
                    {
                        valueFound = true;
                    }
                }
            }

            foreach (var wordsInField in result.WordsInFields.GroupBy(t => t.field, t => t.match))
            {
                var fieldText = wordsInField.Key;
                if (fieldText == OriginalType)
                {
                    // OriginalType already added above
                    continue;
                }

                Highlights.Add(" ");

                if (nameValueNode != null && fieldText.Equals(nameValueNode.Value) && !nameFound)
                {
                    Highlights.Add(nameValueNode.Name + " = ");
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

                if (nameValueNode != null && wordsInField.Key.Equals(nameValueNode.Name))
                {
                    if (!valueFound)
                    {
                        Highlights.Add(" = " + TextUtilities.ShortenValue(nameValueNode.Value, "..."));
                    }
                    else
                    {
                        Highlights.Add(" = ");
                    }
                }

                if (nameValueNode == null && node is NamedNode named && named.Name == wordsInField.Key)
                {
                    var differentiator = GetNodeDifferentiator(node);
                    if (differentiator != null)
                    {
                        Highlights.Add(differentiator);
                    }
                }
            }

            AddDuration(result);
        }

        private object GetNodeDifferentiator(BaseNode node)
        {
            if (node is Project project)
            {
                var result = "";

                if (!string.IsNullOrEmpty(project.TargetFramework))
                {
                    result += " " + project.TargetFramework;
                }

                if (!string.IsNullOrEmpty(project.TargetsDisplayText))
                {
                    result += " " + project.TargetsDisplayText;
                }

                if (!string.IsNullOrEmpty(result))
                {
                    return result;
                }
            }

            return null;
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

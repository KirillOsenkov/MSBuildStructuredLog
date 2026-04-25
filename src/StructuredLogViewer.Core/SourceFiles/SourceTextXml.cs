using System.Linq;
using Microsoft.Language.Xml;

namespace StructuredLogViewer
{
    public static class SourceTextXml
    {
        // Lazily parses the SourceText as XML and caches the (workaround-applied)
        // root element in SourceText.SyntaxTree. Returns false only when the
        // input is null.
        public static bool TryGetXml(SourceText text, out IXmlElement root)
        {
            if (text == null)
            {
                root = null;
                return false;
            }

            if (text.SyntaxTree is IXmlElement cached)
            {
                root = cached;
                return true;
            }

            var document = Parser.ParseText(text.Text);
            IXmlElement parsed = document.Root;

            // work around a bug in Xml Parser where a virtual parent is created around the root element
            // when the root element is preceded by trivia (comment)
            if (parsed.Name == null && parsed.Elements.FirstOrDefault() is IXmlElement firstElement && firstElement.Name == "Project")
            {
                parsed = firstElement;
            }

            text.SyntaxTree = parsed;
            root = parsed;
            return true;
        }
    }
}

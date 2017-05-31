using System.Collections.Generic;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class StringCache
    {
        private Dictionary<string, string> deduplicationMap = new Dictionary<string, string>();

        public IEnumerable<string> Instances => deduplicationMap.Keys;

        public string Intern(string text)
        {
            if (text == null)
            {
                return null;
            }

            if (text.Length == 0)
            {
                return string.Empty;
            }

            // if it has line breaks, save some more space
            text = text.Replace("\r\n", "\n");
            text = text.Replace("\r", "\n");

            string existing;
            if (deduplicationMap.TryGetValue(text, out existing))
            {
                return existing;
            }

            deduplicationMap[text] = text;

            return text;
        }
    }
}

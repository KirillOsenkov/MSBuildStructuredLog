using System.Collections.Generic;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class StringTable
    {
        private Dictionary<string, string> deduplicationMap = new Dictionary<string, string>();

        public string Intern(string text)
        {
            if (text == null)
            {
                return null;
            }

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

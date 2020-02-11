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

        public IDictionary<string, string> InternStringDictionary(IDictionary<string, string> inputDictionary)
        {
            if (inputDictionary == null)
            {
                return null;
            }

            if (inputDictionary.Count == 0)
            {
                return inputDictionary;
            }

            var outputDictionary = new Dictionary<string, string>(inputDictionary.Count);

            foreach (var kvp in inputDictionary)
            {
                outputDictionary[Intern(kvp.Key)] = Intern(kvp.Value);
            }

            return outputDictionary;
        }

        public IReadOnlyList<string> InternList(IReadOnlyList<string> inputList)
        {
            if (inputList == null)
            {
                return null;
            }

            if (inputList.Count == 0)
            {
                return inputList;
            }

            var outputList = new List<string>(inputList.Count);

            foreach (var element in inputList)
            {
                outputList.Add(Intern(element));
            }

            return outputList;
        }
    }
}

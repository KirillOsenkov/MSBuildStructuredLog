using System.Collections.Generic;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class StringCache
    {
        private Dictionary<string, string> deduplicationMap = new Dictionary<string, string>();

        public IEnumerable<string> Instances { get; set; }

        public StringCache()
        {
            Instances = deduplicationMap.Keys;
        }

        /// <summary>
        /// Already deduplicated list of strings can be provided externally,
        /// in which case we should turn off deduplication and just use this
        /// set of strings
        /// </summary>
        public void SetStrings(IEnumerable<string> strings)
        {
            Instances = strings;
            DisableDeduplication = true;
        }

        public bool DisableDeduplication { get; set; }

        public string Intern(string text)
        {
            if (DisableDeduplication)
            {
                return text;
            }

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
            if (DisableDeduplication)
            {
                return inputDictionary;
            }

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
            if (DisableDeduplication)
            {
                return inputList;
            }

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

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

        public void Seal()
        {
            int stringCount = deduplicationMap.Count + 1;
            var strings = new string[stringCount];

            strings[0] = "";
            deduplicationMap.Keys.CopyTo(strings, 1);

            deduplicationMap = null;

            Instances = strings;
            DisableDeduplication = true;
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

        public void Intern(IEnumerable<string> strings)
        {
            foreach (var text in strings)
            {
                Intern(text);
            }
        }

        public bool DisableDeduplication { get; set; }
        public bool NormalizeLineEndings { get; set; } = true;
        public bool HasDeduplicatedStrings { get; set; }

        public string SoftIntern(string text)
        {
            if (HasDeduplicatedStrings)
            {
                return text;
            }

            return Intern(text);
        }

        public string Intern(string text)
        {
            if (string.IsNullOrEmpty(text) || DisableDeduplication)
            {
                return text;
            }

            if (NormalizeLineEndings)
            {
                // if it has line breaks, save some more space
                text = text.NormalizeLineBreaks();
            }

            lock (deduplicationMap)
            {
                if (deduplicationMap.TryGetValue(text, out string existing))
                {
                    return existing;
                }

                deduplicationMap[text] = text;
            }

            return text;
        }

        public bool Contains(string text)
        {
            lock (deduplicationMap)
            {
                return deduplicationMap.ContainsKey(text);
            }
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

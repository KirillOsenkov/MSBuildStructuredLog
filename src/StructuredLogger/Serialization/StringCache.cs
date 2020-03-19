using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public interface IStringCache
    {
        string Intern(string text);
        IEnumerable<string> Instances { get; }
    }

    public class StringCache : IStringCache
    {
        private Dictionary<string, string> deduplicationMap = new Dictionary<string, string>(new Comparer());

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

    public class LengthAwareStringCache : IStringCache
    {
        private Dictionary<int, Dictionary<string, string>> mapsByLength = new Dictionary<int, Dictionary<string, string>>();

        public IEnumerable<string> Instances => mapsByLength.SelectMany(kvp => kvp.Value.Keys);

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
            if (!mapsByLength.TryGetValue(text.Length, out var bucket))
            {
                bucket = new Dictionary<string, string>();
                mapsByLength[text.Length] = bucket;
            }
            else if (bucket.TryGetValue(text, out existing))
            {
                return existing;
            }

            bucket[text] = text;

            return text;
        }
    }

    public static class StringCacheExtensions
    {
        public static IDictionary<string, string> InternStringDictionary(this IStringCache stringCache, IDictionary<string, string> inputDictionary)
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
                outputDictionary[stringCache.Intern(kvp.Key)] = stringCache.Intern(kvp.Value);
            }

            return outputDictionary;
        }

        public static IReadOnlyList<string> InternList(this IStringCache stringCache, IReadOnlyList<string> inputList)
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
                outputList.Add(stringCache.Intern(element));
            }

            return outputList;
        }
    }
}

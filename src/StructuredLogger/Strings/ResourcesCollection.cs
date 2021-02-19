using System.Collections.Generic;
using System.IO;
using ResourcesDictionary = System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, string>>;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class StringsSet
    {
        private Dictionary<string, string> currentSet;
        public string Culture { get; set; }

        public StringsSet(string culture)
        {
            Culture = culture;
            currentSet = ResourcesCollection[culture];
        }

        private static ResourcesDictionary resourcesCollection;
        public static ResourcesDictionary ResourcesCollection
        {
            get
            {
                if (resourcesCollection == null)
                {
                    var assembly = typeof(StructuredLogger).Assembly;
                    var stream = assembly.GetManifestResourceStream(@"Strings.json");

                    var reader = new StreamReader(stream);
                    var text = reader.ReadToEnd();
                    resourcesCollection = TinyJson.JSONParser.FromJson<ResourcesDictionary>(text);
                }

                return resourcesCollection;
            }
        }

        public string GetString(string key)
        {
            if (currentSet.TryGetValue(key, out var value))
            {
                return value;
            }

            return string.Empty;
        }
    }
}

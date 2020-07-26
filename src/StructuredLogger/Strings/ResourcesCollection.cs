using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.Serialization.Json;
using ResourcesDictionary = System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, string>>;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class StringsSet
    {
        private static Dictionary<string, string> CurrentSet;
        private static ResourcesDictionary ResourcesCollection;

        public void InitializeCollection(CultureInfo cultureInfo)
        {
            if (ResourcesCollection == null)
            {
                var assembly = typeof(StructuredLogger).Assembly;
                var stream = assembly.GetManifestResourceStream(@"Strings.json");

                var settings = new DataContractJsonSerializerSettings() { UseSimpleDictionaryFormat = true };
                var d = new DataContractJsonSerializer(typeof(ResourcesDictionary), settings);
                ResourcesCollection = (ResourcesDictionary)d.ReadObject(stream);
            }

            if (ResourcesCollection.ContainsKey(cultureInfo.Name))
            {
                CurrentSet = ResourcesCollection[cultureInfo.Name];
            }
            else
            {
                CurrentSet = ResourcesCollection["en-US"];
            }
        }

        public string GetString(string key)
        {
            if (CurrentSet.ContainsKey(key))
            {
                return CurrentSet[key];
            }
            else
            {
                return String.Empty;
            }
        }
    }
}

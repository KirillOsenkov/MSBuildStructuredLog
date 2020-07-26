using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.Serialization.Json;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class ResourcesCollection
    {
        public Dictionary<string, Dictionary<string, string>> CultureResources { get; set; }
    }

    public class StringsSet
    {
        private static Dictionary<string, string> CurrentSet;
        private static ResourcesCollection ResourcesCollection;

        public void InitializeCollection(CultureInfo cultureInfo)
        {
            if (ResourcesCollection == null)
            {
                var assembly = typeof(StructuredLogger).Assembly;
                var stream = assembly.GetManifestResourceStream(@"Strings.json");

                var settings = new DataContractJsonSerializerSettings() { UseSimpleDictionaryFormat = true };
                var d = new DataContractJsonSerializer(typeof(ResourcesCollection), settings);
                ResourcesCollection = (ResourcesCollection)d.ReadObject(stream);
            }

            if (ResourcesCollection.CultureResources.ContainsKey(cultureInfo.Name))
            {
                CurrentSet = ResourcesCollection.CultureResources[cultureInfo.Name];
            }
            else
            {
                CurrentSet = ResourcesCollection.CultureResources["en-US"];
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

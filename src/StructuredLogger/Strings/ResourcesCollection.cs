using System.Collections.Generic;
using System.IO;
using ResourcesDictionary = System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, string>>;
#if NET8_0_OR_GREATER
using System.Text.Json;
using System.Text.Json.Serialization;
#endif

namespace Microsoft.Build.Logging.StructuredLogger
{
    public partial class StringsSet
    {
        private Dictionary<string, string> currentSet;
        public string Culture { get; set; }

        public StringsSet(string culture)
        {
            Culture = culture;
            currentSet = ResourcesCollection[culture];
        }

        private static readonly object lockObject = new object();

        private static ResourcesDictionary resourcesCollection;
        public static ResourcesDictionary ResourcesCollection
        {
            get
            {
                if (resourcesCollection == null)
                {
                    lock (lockObject)
                    {
                        if (resourcesCollection == null)
                        {
                            var assembly = typeof(StructuredLogger).Assembly;
                            using var stream = assembly.GetManifestResourceStream(@"Strings.json");
#if NET8_0_OR_GREATER
                            resourcesCollection = JsonSerializer.Deserialize(stream, JsonSourceGenerationContext.Default.ResourcesDictionary);
#else
                            using var reader = new StreamReader(stream);
                            var text = reader.ReadToEnd();
                            resourcesCollection = TinyJson.JSONParser.FromJson<ResourcesDictionary>(text);
#endif
                        }
                    }
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

#if NET8_0_OR_GREATER
        [JsonSerializable(typeof(ResourcesDictionary), TypeInfoPropertyName = "ResourcesDictionary")]
        partial class JsonSourceGenerationContext : JsonSerializerContext
        {
        }
#endif
    }
}

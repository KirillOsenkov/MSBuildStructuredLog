using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Resources;

namespace ResourcesGenerator
{
    public class ResourceCreator
    {
        private static string[] msBuildDlls = new string[] { "Microsoft.Build.dll", "Microsoft.Build.Tasks.Core.dll", "Microsoft.Build.Utilities.Core.dll" };
     
        public static void CreateResourceFile(string msbuildPath, string resourcesFolder)
        {
            ResourcesCollection resourcesCollection = new ResourcesCollection();

            foreach (KeyValuePair<string, string> cultur in ConstantCollection.CultureList)
            {
                Dictionary<string, string> resourceByCulture = new Dictionary<string, string>();
                resourcesCollection.CultureResources.Add(cultur.Value, resourceByCulture);

                foreach (string dll in msBuildDlls)
                {
                    var asm = System.Reflection.Assembly.LoadFrom(msbuildPath + @"\" + dll);
                    string[] strings = asm.GetManifestResourceNames();

                    foreach (var s in strings)
                    {
                        if (s.IndexOf(".resource") > -1)
                        {
                            var resourceManager = new ResourceManager(s.Substring(0, s.IndexOf(".resource")), asm);
                            var myResourceSet = resourceManager.GetResourceSet(CultureInfo.GetCultureInfo(cultur.Value), true, true);

                            if (myResourceSet != null)
                            {
                                foreach (DictionaryEntry res in myResourceSet)
                                {
                                    string resstr = res.Value as string;
                                    if (resstr != null && ConstantCollection.ResourcesNameList.Contains(res.Key.ToString()))
                                    {
                                        if (!resourceByCulture.ContainsKey(res.Key.ToString()))
                                            resourceByCulture.Add(res.Key.ToString(), resstr);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            Save(resourcesCollection);
        }

        private static void Save(ResourcesCollection collection)
        {
            var path = Assembly.GetExecutingAssembly().Location;
            path = path.Replace(@"bin\ResourcesGenerator\Debug\netcoreapp3.1\ResourcesGenerator.dll", @"src\StructuredLogger\Strings\");

            using (FileStream fileStream = File.Create(path + @"Strings.json"))
            using (StreamWriter writer = new StreamWriter(fileStream))
            {
                JsonSerializer ser = new JsonSerializer();
                ser.Serialize(writer, collection);         
            }
        }
    }
}

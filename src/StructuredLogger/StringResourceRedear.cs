using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.Resources;
using System.Globalization;
using System.Collections;

namespace StructuredLogger
{
    public class StringResourceRedear
    {
        public static Dictionary<object, string> ResourceStrings { get; private set; } = new Dictionary<object, string>();
        private const string msBuildDll = "Microsoft.Build.dll";
        private const string msbuild = "MSBuild";

        public static void ReadResources()
        {
            string path = msBuildDll;
            if (Environment.GetEnvironmentVariables().Contains("PATH"))
            {
                string stringpath = (String)Environment.GetEnvironmentVariables()["PATH"];
                string[] pathes = stringpath.Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string st in pathes)
                {
                    if (st.Contains(msbuild))
                    {
                        path = st + @"\"  + msBuildDll;
                        break;
                    }
                }
            }

            var asm = System.Reflection.Assembly.LoadFrom(path);
            if (asm != null)
            {
                string[] strings = asm.GetManifestResourceNames();

                foreach (var s in strings)
                {
                    var resourceManager = new ResourceManager(s.Substring(0, s.IndexOf(".resource")), asm);
                    var myResourceSet = resourceManager.GetResourceSet(CultureInfo.CurrentCulture, true, true);

                    if (myResourceSet != null)
                    {
                        foreach (DictionaryEntry res in myResourceSet)
                        {
                            string resstr = res.Value as string;
                            if (resstr != null)
                            {
                                if (!ResourceStrings.ContainsKey(res.Key))
                                ResourceStrings.Add(res.Key, resstr);
                            }
                        }
                    }
                }
            }
        }
    }
}

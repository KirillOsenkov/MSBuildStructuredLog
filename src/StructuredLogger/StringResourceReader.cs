using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.Resources;
using System.Globalization;
using System.Collections;
using System.IO;
using System.CodeDom;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Build;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Tasks;


namespace StructuredLogger
{
    public class StringResourceReader
    {
        public static Dictionary<object, string> ResourceStrings { get; private set; } = new Dictionary<object, string>();
        private static string[] msBuildDlls = new string[] { "Microsoft.Build.dll", "Microsoft.Build.Tasks.Core.dll", "Microsoft.Build.Utilities.Core.dll" };
        private const string msbuild = "MSBuild";
        private static List<Version> vsVersions = new List<Version>() { new Version("16.0"), new Version("15.0"), new Version("14.0"), new Version("13.0"), new Version("12.0"), new Version("11.0"), new Version("9.0"), new Version("8.0"), new Version("7.0"), new Version("6.0") };


        public static void ReadResources()
        {
            string path ="";

            var env = Environment.GetEnvironmentVariables();

            if (Environment.GetEnvironmentVariables().Contains("Path"))
            {
                string stringpath = (String)Environment.GetEnvironmentVariables()["Path"];
                string[] pathes = stringpath.Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string st in pathes)
                {
                    if (st.Contains(msbuild))
                    {
                        path = st;
                        break;
                    }
                }
            }

            List<string> stringsneeded = new List<string>();
            string line = "";
            using (System.IO.StreamReader file = new System.IO.StreamReader(@"resource_en_text.txt"))
            {
                while ((line = file.ReadLine()) != null)
                {
                    stringsneeded.Add(line.Trim());
                }
            }


            foreach (string dll in msBuildDlls)
            {
                var asm = System.Reflection.Assembly.LoadFrom(path + @"\" + dll);
                string[] strings = asm.GetManifestResourceNames();

                foreach (var s in strings)
                {
                    if (s.IndexOf(".resource") > -1)
                    {
                        
                        var resourceManager = new ResourceManager(s.Substring(0, s.IndexOf(".resource")), asm);
                        var myResourceSet = resourceManager.GetResourceSet(CultureInfo.GetCultureInfo("pl-PL"), true, true);

                        if (myResourceSet != null)
                        {
                            foreach (DictionaryEntry res in myResourceSet)
                            {
                                string resstr = res.Value as string;
                                if (resstr != null && stringsneeded.Contains(res.Key.ToString()))
                                {
                                    if (!ResourceStrings.ContainsKey(res.Key))
                                        ResourceStrings.Add(res.Key, resstr);
                                }
                            }
                        }
                    }
                }
            }
            using (System.IO.StreamWriter file = new System.IO.StreamWriter(@"resources.us", false, Encoding.UTF8))
            {
                foreach (KeyValuePair<object, string> line1 in ResourceStrings)
                {
                    file.WriteLine(@"<data name=""" + line1.Key.ToString() + @""" xml:space=""preserve"">");
                    file.WriteLine(@"<value>" + line1.Value +  "</value>");
                    file.WriteLine(@"</data>");
                }
            }
           
        }

    }
}

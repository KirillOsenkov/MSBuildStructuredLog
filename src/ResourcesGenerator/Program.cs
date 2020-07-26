using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Text;

namespace ResourcesGenerator
{
    class Program
    {
        static void Main(string[] args)
        {
            var arguments = new List<string>();
            string path;
            foreach (var item in Environment.GetCommandLineArgs())
            {
                try
                {
                    if (item.Equals("?"))
                    {
                        Console.WriteLine("please add path to msbuild.exe file location: 'msbuild folder path'");
                    }
                    else if (item.Contains(@"ResourcesGenerator.dll"))
                    {
                        continue;
                    }
                    else
                    {
                        path = item.Replace(@"""", "");
                        if (!Directory.Exists(path))
                        {
                            Console.WriteLine("MSBuild folder not found: " + path);
                        }
                        else
                        {
                            arguments.Add(path);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    Console.WriteLine(" ----------------------------- ");
                    Console.WriteLine("please add path to msbuild.exe file location: 'msbuild folder path'");
                }
            }

            if (arguments.Count > 0)
            {
               ResourceCreator.CreateResourceFile(arguments[0], @"d:\temp\");
            }
            else
            {
                Console.WriteLine("please add path to msbuild.exe file location: 'msbuild folder path'");
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Build.Logging.StructuredLogger
{
    /// <summary>
    /// Adds the value of Private metadata if present to source items of dependencies output by RAR
    /// (I know, I need a longer explanation here)
    /// TODO: link to article or blog post explaining this
    /// </summary>
    public class CopyLocalAnalyzer
    {
        public static void AnalyzeResolveAssemblyReference(Task rar)
        {
            var results = rar.FindChild<Folder>(c => c.Name == "Results");
            if (results != null)
            {
                foreach (var reference in results.Children.OfType<Parameter>())
                {
                    if (reference.Name.StartsWith("Dependency ") || reference.Name.StartsWith("Unified Dependency "))
                    {
                        bool foundNotCopyLocalBecauseMetadata = false;
                        var requiredBy = new List<Item>();
                        foreach (var message in reference.Children.OfType<Item>())
                        {
                            string text = message.Text;
                            if (text.StartsWith("Required by \""))
                            {
                                requiredBy.Add(message);
                            }
                            else if (text == @"This reference is not ""CopyLocal"" because at least one source item had ""Private"" set to ""false"" and no source items had ""Private"" set to ""true"".")
                            {
                                foundNotCopyLocalBecauseMetadata = true;
                            }
                        }

                        if (foundNotCopyLocalBecauseMetadata)
                        {
                            var assemblies = rar.FindChild<Folder>("Parameters")?.FindChild<Parameter>("Assemblies");
                            if (assemblies != null)
                            {
                                var dictionary = assemblies.Children.OfType<Item>().ToDictionary(a => a.Text, StringComparer.OrdinalIgnoreCase);

                                foreach (var sourceItem in requiredBy)
                                {
                                    int prefixLength = "Required by \"".Length;
                                    string text = sourceItem.Text;
                                    var referenceName = text.Substring(prefixLength, text.Length - prefixLength - 2);
                                    Item foundSourceItem;
                                    if (dictionary.TryGetValue(referenceName, out foundSourceItem))
                                    {
                                        foreach (var metadata in foundSourceItem.Children.OfType<Metadata>())
                                        {
                                            if (metadata.Name == "Private")
                                            {
                                                sourceItem.AddChild(new Metadata() { Name = metadata.Name, Value = metadata.Value });
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}

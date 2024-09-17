using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Build.Logging.StructuredLogger
{
    /// <summary>
    /// Adds the value of Private metadata if present to source items of dependencies output by RAR
    /// https://github.com/dotnet/msbuild/blob/main/documentation/wiki/ResolveAssemblyReference.md
    /// </summary>
    public class ResolveAssemblyReferenceAnalyzer
    {
        public TimeSpan TotalRARDuration = TimeSpan.Zero;
        private StringCache stringTable;

        public HashSet<string> UsedLocations { get; } = new HashSet<string>();
        public HashSet<string> UnusedLocations { get; } = new HashSet<string>();
        private readonly HashSet<string> currentUsedLocations = new HashSet<string>();

        public void AnalyzeResolveAssemblyReference(Task rar)
        {
            stringTable = rar.GetNearestParent<Build>()?.StringTable;

            currentUsedLocations.Clear();

            var results = rar.FindChild<Folder>(static c => c.Name == Strings.Results);
            var parameters = rar.FindChild<Folder>(static c => c.Name == Strings.Parameters);

            TotalRARDuration += rar.Duration;

            IList<string> searchPaths = null;
            if (parameters != null)
            {
                var searchPathsNode = parameters.FindChild<NamedNode>(static c => c.Name == Strings.SearchPaths);
                if (searchPathsNode != null)
                {
                    searchPaths = searchPathsNode.Children.Select(c => c.ToString()).ToArray();
                }
            }

            if (results != null)
            {
                results.SortChildren();

                foreach (var reference in results.Children.OfType<Parameter>())
                {
                    const string ResolvedFilePathIs = "Resolved file path is \"";
                    string resolvedFilePath = null;
                    var resolvedFilePathNode = reference.FindChild<Item>(static i => i.ToString().StartsWith(ResolvedFilePathIs, StringComparison.Ordinal));
                    if (resolvedFilePathNode != null)
                    {
                        var text = resolvedFilePathNode.ToString();
                        resolvedFilePath = text.Substring(ResolvedFilePathIs.Length, text.Length - ResolvedFilePathIs.Length - 2);
                    }

                    const string ReferenceFoundAt = "Reference found at search path location \"";
                    var foundAtLocation = reference.FindChild<Item>(static i => i.ToString().StartsWith(ReferenceFoundAt, StringComparison.Ordinal));
                    if (foundAtLocation != null)
                    {
                        var text = foundAtLocation.ToString();
                        var location = text.Substring(ReferenceFoundAt.Length, text.Length - ReferenceFoundAt.Length - 2);

                        // filter out the case where the assembly is resolved from the AssemblyFiles parameter
                        // In this case the location matches the resolved file path.
                        if (resolvedFilePath == null || resolvedFilePath != location)
                        {
                            UsedLocations.Add(location);
                            currentUsedLocations.Add(location);
                        }
                    }

                    var thisReferenceName = ParseReferenceName(reference.Name);

                    if (reference.Name.StartsWith("Dependency ", StringComparison.Ordinal) || reference.Name.StartsWith("Unified Dependency ", StringComparison.Ordinal))
                    {
                        bool foundNotCopyLocalBecauseMetadata = false;
                        var requiredBy = new List<Item>();
                        Item notCopyLocalMessage = null;

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
                                notCopyLocalMessage = message;
                            }
                        }

                        if (foundNotCopyLocalBecauseMetadata)
                        {
                            var assemblies = rar.FindChild<Folder>(Strings.Parameters)?.FindChild<Parameter>(Strings.Assemblies);
                            if (assemblies != null)
                            {
                                var dictionary = assemblies.Children
                                    .OfType<Item>()
                                    .GroupBy(i => i.Text, StringComparer.OrdinalIgnoreCase)
                                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

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
                                                if (notCopyLocalMessage != null)
                                                {
                                                    var message = $"{foundSourceItem} has {metadata.Name} set to {metadata.Value}";
                                                    message = stringTable?.Intern(message);
                                                    notCopyLocalMessage.AddChild(new Message
                                                    {
                                                        Text = message
                                                    });
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

            if (searchPaths != null)
            {
                foreach (var searchPath in searchPaths)
                {
                    if (currentUsedLocations.Contains(searchPath))
                    {
                        var usedLocations = rar.GetOrCreateNodeWithName<Folder>(Strings.UsedLocations);
                        usedLocations.AddChild(new Item { Text = searchPath });
                        UnusedLocations.Remove(searchPath);
                    }
                    else
                    {
                        var unusedLocations = rar.GetOrCreateNodeWithName<Folder>(Strings.UnusedLocations);
                        unusedLocations.AddChild(new Item { Text = searchPath });
                        if (!UsedLocations.Contains(searchPath))
                        {
                            UnusedLocations.Add(searchPath);
                        }
                        else
                        {
                            UnusedLocations.Remove(searchPath);
                        }
                    }
                }
            }
        }

        private string ParseReferenceName(string name)
        {
            var quote = name.IndexOf('"');
            if (quote == -1)
            {
                return null;
            }

            name = name.Substring(quote + 1);
            var comma = name.IndexOf(',');
            if (comma == -1)
            {
                quote = name.IndexOf('"');
                if (quote == -1)
                {
                    return null;
                }

                return name.Substring(0, quote);
            }

            return name.Substring(0, comma);
        }

        public void AppendFinalReport(Build build)
        {
            if (UsedLocations.Any())
            {
                var usedLocationsNode = build.GetOrCreateNodeWithName<Folder>(Strings.UsedAssemblySearchPathsLocations);
                foreach (var location in UsedLocations.OrderBy(s => s))
                {
                    usedLocationsNode.AddChild(new Item { Text = location });
                }
            }

            if (UnusedLocations.Any())
            {
                var unusedLocationsNode = build.GetOrCreateNodeWithName<Folder>(Strings.UnusedAssemblySearchPathsLocations);
                foreach (var location in UnusedLocations.OrderBy(s => s))
                {
                    unusedLocationsNode.AddChild(new Item { Text = location });
                }
            }
        }
    }
}

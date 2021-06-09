using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using Newtonsoft.Json;
using ResourcesDictionary = System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, string>>;

namespace ResourcesGenerator
{
    public class ResourceCreator
    {
        private static string[] msBuildDlls = new string[]
        {
            "Microsoft.Build.dll",
            "Microsoft.Build.Tasks.Core.dll",
            "Microsoft.Build.Utilities.Core.dll"
        };

        public static string[] ResourceNames = new[]
        {
            "TaskParameterPrefix",
            "OutputItemParameterMessagePrefix",
            "OutputPropertyLogMessage",
            "PropertyGroupLogMessage",
            "ItemGroupIncludeLogMessagePrefix",
            "ItemGroupRemoveLogMessage",
            "GetSDKReferenceFiles.ConflictReferenceSameSDK",
            "GetSDKReferenceFiles.ConflictRedistDifferentSDK",
            "GetSDKReferenceFiles.ConflictReferenceDifferentSDK",
            "ResolveAssemblyReference.SearchPath",
            "ResolveAssemblyReference.UnifiedPrimaryReference",
            "ResolveAssemblyReference.PrimaryReference",
            "ResolveAssemblyReference.Dependency",
            "ResolveAssemblyReference.UnifiedDependency",
            "ResolveAssemblyReference.AssemblyFoldersExSearchLocations",
            "ResolveAssemblyReference.ConflictFound",
            "ResolveAssemblyReference.FoundConflicts",
            "General.GlobalProperties",
            "General.AdditionalProperties",
            "General.OverridingProperties",
            "General.UndefineProperties",
            "General.ProjectUndefineProperties",
            "Copy.FileComment",
            "Copy.HardLinkComment",
            "Copy.DidNotCopyBecauseOfFileMatch",
            "ToolsVersionInEffectForBuild",
            "TaskFoundFromFactory",
            "TaskFound",
            "PropertyReassignment",
            "ProjectImported",
            "ProjectImportSkippedMissingFile",
            "ProjectImportSkippedInvalidFile",
            "ProjectImportSkippedEmptyFile",
            "ProjectImportSkippedFalseCondition",
            "ProjectImportSkippedNoMatches",
            "TaskSkippedFalseCondition",
            "TargetAlreadyCompleteSuccess",
            "TargetSkippedFalseCondition",
            "TargetAlreadyCompleteFailure",
            "TargetSkippedWhenSkipNonexistentTargets",
            "TargetDoesNotExistBeforeTargetMessage",
            "SearchPathsForMSBuildExtensionsPath",
            "OverridingTarget",
            "TryingExtensionsPath",
            "ProjectImported",
            "DuplicateImport",
            "ProjectImportSkippedEmptyFile",
            "ProjectImportSkippedFalseCondition",
            "ProjectImportSkippedNoMatches",
            "ProjectImportSkippedMissingFile",
            "ProjectImportSkippedInvalidFile",
            "PropertyReassignment",
            "EvaluationStarted",
            "EvaluationFinished",
            "CouldNotResolveSdk",
            "ProjectImportSkippedExpressionEvaluatedToEmpty",
            "SkipTargetBecauseOutputsUpToDate"
        };

        public static Dictionary<string, string> Cultures = new Dictionary<string, string>
        {
            { "en", "en-US" },
            { "de", "de-DE" },
            { "it", "it-IT" },
            { "es", "es-ES" },
            { "fr", "fr-FR" },
            { "cs", "cs-CZ" },
            { "ja", "ja-JP" },
            { "ko", "ko-KR" },
            { "ru", "ru-RU" },
            { "pl", "pl-PL" },
            { "pt", "pt-BR" },
            { "tr", "tr-TR" },
            { "zh", "zh-Hans" }
        };

        public static void CreateResourceFile(string msbuildPath)
        {
            var cultureResources = new ResourcesDictionary();

            foreach (KeyValuePair<string, string> culture in Cultures)
            {
                var cultureInfo = CultureInfo.GetCultureInfo(culture.Value);

                Dictionary<string, string> resourcesByCulture = new Dictionary<string, string>();
                cultureResources.Add(culture.Value, resourcesByCulture);

                foreach (string dll in msBuildDlls)
                {
                    var assembly = Assembly.LoadFrom(Path.Combine(msbuildPath, dll));
                    string[] manifestResourceNames = assembly.GetManifestResourceNames();

                    foreach (var manifestResourceName in manifestResourceNames)
                    {
                        int index = manifestResourceName.IndexOf(".resource");
                        if (index > -1)
                        {
                            var resourceManager = new ResourceManager(manifestResourceName.Substring(0, index), assembly);
                            bool tryParents = true;
                            if (manifestResourceName == "System.Design.resources")
                            {
                                tryParents = false;
                            }

                            var myResourceSet = resourceManager.GetResourceSet(cultureInfo, createIfNotExists: true, tryParents: tryParents);
                            if (myResourceSet != null)
                            {
                                foreach (DictionaryEntry res in myResourceSet)
                                {
                                    var key = res.Key.ToString();
                                    if (res.Value is string resourceString && ResourceNames.Contains(key))
                                    {
                                        if (!resourcesByCulture.ContainsKey(key))
                                        {
                                            resourcesByCulture.Add(key, resourceString);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            Save(cultureResources);
        }

        private static void Save(ResourcesDictionary collection)
        {
            var path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var sourcePath = Path.Combine(path, "..", "..", "..", "..", @"src\StructuredLogger\Strings");
            if (Directory.Exists(sourcePath))
            {
                path = Path.GetFullPath(sourcePath);
            }

            path = Path.Combine(path, "Strings.json");
            var text = JsonConvert.SerializeObject(collection, Formatting.Indented);
            File.WriteAllText(path, text);
        }
    }
}

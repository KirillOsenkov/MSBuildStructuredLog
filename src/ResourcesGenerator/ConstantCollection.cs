using System;
using System.Collections.Generic;
using System.Text;

namespace ResourcesGenerator
{
    public class ConstantCollection
    {
        private static List<string> mResourcesNameList;
        public static List<string> ResourcesNameList
        {
            get
            {
                if (mResourcesNameList == null)
                    FillResourcesNames();
                return mResourcesNameList;
            }
        }

        private static Dictionary<string, string> mCultureList;
        public static Dictionary<string, string> CultureList
        {
            get
            {
                if (mCultureList == null)
                    FillCultures();
                return mCultureList;
            }
        }


        //add recources name to find in list
        private static void FillResourcesNames()
        {
            mResourcesNameList = new List<string>();
            mResourcesNameList.Add("TaskParameterPrefix");
            mResourcesNameList.Add("OutputItemParameterMessagePrefix");
            mResourcesNameList.Add("OutputPropertyLogMessage");
            mResourcesNameList.Add("PropertyGroupLogMessage");
            mResourcesNameList.Add("ItemGroupIncludeLogMessagePrefix");
            mResourcesNameList.Add("ItemGroupRemoveLogMessage");
            mResourcesNameList.Add("GetSDKReferenceFiles.ConflictReferenceSameSDK");
            mResourcesNameList.Add("GetSDKReferenceFiles.ConflictRedistDifferentSDK");
            mResourcesNameList.Add("GetSDKReferenceFiles.ConflictReferenceDifferentSDK");
            mResourcesNameList.Add("ResolveAssemblyReference.SearchPath");
            mResourcesNameList.Add("ResolveAssemblyReference.UnifiedPrimaryReference");
            mResourcesNameList.Add("ResolveAssemblyReference.PrimaryReference");
            mResourcesNameList.Add("ResolveAssemblyReference.Dependency");
            mResourcesNameList.Add("ResolveAssemblyReference.UnifiedDependency");
            mResourcesNameList.Add("ResolveAssemblyReference.AssemblyFoldersExSearchLocations");
            mResourcesNameList.Add("General.GlobalProperties");
            mResourcesNameList.Add("General.AdditionalProperties");
            mResourcesNameList.Add("General.OverridingProperties");
            mResourcesNameList.Add("General.UndefineProperties");
            mResourcesNameList.Add("Copy.FileComment");
            mResourcesNameList.Add("Copy.HardLinkComment");
            mResourcesNameList.Add("Copy.DidNotCopyBecauseOfFileMatch");
            mResourcesNameList.Add("ToolsVersionInEffectForBuild");
            mResourcesNameList.Add("TaskFoundFromFactory");
            mResourcesNameList.Add("TaskFound");
            mResourcesNameList.Add("PropertyReassignment");
            mResourcesNameList.Add("ProjectImported");
            mResourcesNameList.Add("ProjectImportSkippedMissingFile");
            mResourcesNameList.Add("ProjectImportSkippedInvalidFile");
            mResourcesNameList.Add("ProjectImportSkippedEmptyFile");
            mResourcesNameList.Add("ProjectImportSkippedFalseCondition");
            mResourcesNameList.Add("ProjectImportSkippedNoMatches");
            mResourcesNameList.Add("TaskSkippedFalseCondition");
            mResourcesNameList.Add("TargetAlreadyCompleteSuccess");
            mResourcesNameList.Add("TargetSkippedFalseCondition");
            mResourcesNameList.Add("TargetAlreadyCompleteFailure");
            mResourcesNameList.Add("TargetSkippedWhenSkipNonexistentTargets");
            mResourcesNameList.Add("TargetDoesNotExistBeforeTargetMessage");
            mResourcesNameList.Add("SearchPathsForMSBuildExtensionsPath");
            mResourcesNameList.Add("OverridingTarget");
            mResourcesNameList.Add("TryingExtensionsPath");
            mResourcesNameList.Add("ProjectImported");
            mResourcesNameList.Add("DuplicateImport");
            mResourcesNameList.Add("ProjectImportSkippedEmptyFile");
            mResourcesNameList.Add("ProjectImportSkippedFalseCondition");
            mResourcesNameList.Add("ProjectImportSkippedNoMatches");
            mResourcesNameList.Add("ProjectImportSkippedMissingFile");
            mResourcesNameList.Add("ProjectImportSkippedInvalidFile");
            mResourcesNameList.Add("PropertyReassignment");
        }

        //add culture to create resources file
        private static void FillCultures()
        {
            mCultureList = new Dictionary<string, string>();
            mCultureList.Add("en", "en-US");
            mCultureList.Add("de", "de-DE");
            mCultureList.Add("it", "it-IT");
            mCultureList.Add("es", "es-ES");
            mCultureList.Add("fr", "fr-FR");
            mCultureList.Add("cs", "cs-CZ");
            mCultureList.Add("ja", "ja-JP");
            mCultureList.Add("ko", "ko-KR");
            mCultureList.Add("ru", "ru-RU");
            mCultureList.Add("pl", "pl-PL");
            mCultureList.Add("pt", "pt-BR");
            mCultureList.Add("tr", "tr-TR");
            mCultureList.Add("zh", "zh-Hans");

        }
    }
}

using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class Strings
    {
        public static StringsSet ResourceSet { get; private set; }

        public static void Initialize(string culture = "en-US")
        {
            if (!StringsSet.ResourcesCollection.ContainsKey(culture))
            {
                culture = "en-US";
            }

            if (ResourceSet == null || ResourceSet.Culture != culture)
            {
                ResourceSet = new StringsSet(culture);
                InitializeRegex();
            }
        }

        public static string GetString(string key)
        {
            return ResourceSet.GetString(key);
        }

        private static void InitializeRegex()
        {
            OutputPropertyMessagePrefix = GetString("OutputPropertyLogMessage").Replace("{0}={1}", "");
            BuildingWithToolsVersionPrefix = new Regex(GetString("ToolsVersionInEffectForBuild").Replace("{0}", ".*?"));
            PropertyGroupMessagePrefix = GetString("PropertyGroupLogMessage").Replace("{0}={1}", "");
            ForSearchPathPrefix = new Regex(GetString("ResolveAssemblyReference.SearchPath").Replace("{0}", ".*?"));
            UnifiedPrimaryReferencePrefix = new Regex(GetString("ResolveAssemblyReference.UnifiedPrimaryReference").Replace("{0}", ".*?"));
            PrimaryReferencePrefix = new Regex(GetString("ResolveAssemblyReference.PrimaryReference").Replace("{0}", ".*?"));
            DependencyPrefix = new Regex(GetString("ResolveAssemblyReference.Dependency").Replace("{0}", ".*?"));
            UnifiedDependencyPrefix = new Regex(GetString("ResolveAssemblyReference.UnifiedDependency").Replace("{0}", ".*?"));
            AssemblyFoldersExLocation = new Regex(GetString("ResolveAssemblyReference.AssemblyFoldersExSearchLocations").Replace("{0}", ".*?"));
            AdditionalPropertiesPrefix = new Regex(GetString("General.AdditionalProperties").Replace("{0}", ".*?"));
            OverridingGlobalPropertiesPrefix = new Regex(GetString("General.OverridingProperties").Replace("{0}", ".*?"));
            TargetAlreadyCompleteSuccess = new Regex(GetString("TargetAlreadyCompleteSuccess").Replace("{0}", @".*?"));
            TargetAlreadyCompleteFailure = new Regex(GetString("TargetAlreadyCompleteFailure").Replace("{0}", @".*?"));
            TargetSkippedWhenSkipNonexistentTargets = new Regex(GetString("TargetSkippedWhenSkipNonexistentTargets").Replace("{0}", @".*?"));
            RemovingProjectProperties = new Regex(GetString("General.ProjectUndefineProperties").Replace("{0}", @".*?"));

            DuplicateImport = new Regex(GetString("SearchPathsForMSBuildExtensionsPath")
                .Replace("{0}", @".*?")
                .Replace("{1}", @".*?")
                .Replace("{2}", @".*?"));

            SearchPathsForMSBuildExtensionsPath = new Regex(GetString("SearchPathsForMSBuildExtensionsPath")
                .Replace("{0}", @".*?")
                .Replace("{1}", @".*?"));

            OverridingTarget = new Regex(GetString("OverridingTarget")
                .Replace("{0}", @".*?")
                .Replace("{1}", @".*?")
                .Replace("{2}", @".*?")
                .Replace("{3}", @".*?"));

            TryingExtensionsPath = new Regex(GetString("TryingExtensionsPath")
                 .Replace("{0}", @".*?")
                 .Replace("{1}", @".*?"));

            ProjectImported = new Regex(GetString("ProjectImported")
                .Replace("{0}", @".*?")
                .Replace("{1}", @".*?")
                .Replace("{2}", @".*?")
                .Replace("{3}", @".*?"));

            TargetSkippedFalseCondition = new Regex(GetString("TargetSkippedFalseCondition")
                .Replace("{0}", @".*?")
                .Replace("{1}", @".*?")
                .Replace("{2}", @".*?")
                );

            TaskSkippedFalseCondition = new Regex(GetString("TaskSkippedFalseCondition")
                .Replace("{0}", @".*?")
                .Replace("{1}", @".*?")
                .Replace("{2}", @".*?")
                );

            TargetDoesNotExistBeforeTargetMessage = new Regex(GetString("TargetDoesNotExistBeforeTargetMessage")
                .Replace("{0}", @".*?")
                .Replace("{1}", @".*?")
                );

            CopyingFileFrom = new Regex(GetString("Copy.FileComment")
                .Replace("{0}", @"(?<From>[^\""]+)")
                .Replace("{1}", @"(?<To>[^\""]+)")
                );

            CreatingHardLink = new Regex(GetString("Copy.HardLinkComment")
                .Replace("{0}", @"(?<From>[^\""]+)")
                .Replace("{1}", @"(?<To>[^\""]+)")
                );

            DidNotCopy = new Regex(GetString("Copy.DidNotCopyBecauseOfFileMatch")
               .Replace("{0}", @"(?<From>[^\""]+)")
               .Replace("{1}", @"(?<To>[^\""]+)")
               .Replace("{2}", ".*?")
               .Replace("{3}", ".*?")
               );

            string importProject = GetString("ProjectImported")
                .Replace("{0}", @"(?<ImportedProject>[^\""]+)")
                .Replace("{1}", @"(?<File>[^\""]+)")
                .Replace("({2},{3})", @"\((?<Line>\d+),(?<Column>\d+)\)\.$");
            ImportingProjectRegex = new Regex(@"^" + importProject.Substring(0, importProject.Length - 1), RegexOptions.Compiled);

            string skippedMissingFile = GetString("ProjectImportSkippedMissingFile")
                .Replace("{0}", @"(?<ImportedProject>[^\""]+)")
                .Replace("{1}", @"(?<File>[^\""]+)")
                .Replace("({2},{3})", @"\((?<Line>\d+),(?<Column>\d+)\)\.");
            ProjectImportSkippedMissingFile = new Regex(@"^" + skippedMissingFile.Substring(0, skippedMissingFile.Length - 1), RegexOptions.Compiled);

            string skippedInvalidFile = GetString("ProjectImportSkippedInvalidFile")
               .Replace("{0}", @"(?<ImportedProject>[^\""]+)")
               .Replace("{1}", @"(?<File>[^\""]+)")
               .Replace("({2},{3})", @"\((?<Line>\d+),(?<Column>\d+)\)\.");
            ProjectImportSkippedInvalidFile = new Regex(@"^" + skippedMissingFile.Substring(0, skippedMissingFile.Length - 1), RegexOptions.Compiled);

            string skippedEmptyFile = GetString("ProjectImportSkippedEmptyFile")
             .Replace("{0}", @"(?<ImportedProject>[^\""]+)")
             .Replace("{1}", @"(?<File>[^\""]+)")
             .Replace("({2},{3})", @"\((?<Line>\d+),(?<Column>\d+)\)\.");
            ProjectImportSkippedEmptyFile = new Regex(@"^" + skippedEmptyFile.Substring(0, skippedEmptyFile.Length - 1), RegexOptions.Compiled);

            string skippedNoMatches = GetString("ProjectImportSkippedNoMatches")
             .Replace("{0}", @"(?<ImportedProject>[^\""]+)")
             .Replace("{1}", @"(?<File>.*)")
             .Replace("({2},{3})", @"\((?<Line>\d+),(?<Column>\d+)\)\.");
            ProjectImportSkippedNoMatches = new Regex(@"^" + skippedNoMatches.Substring(0, skippedNoMatches.Length - 1), RegexOptions.Compiled);

            string propertyReassignment = GetString("PropertyReassignment")
             .Replace(@"$({0})=""{1}"" (", @"\$\(\w+\)=.*? \(")
             .Replace(@"""{2}"")", @".*?""\)")
             .Replace("{3}", @"(?<File>.*) \((?<Line>\d+),(\d+)\)$");
            PropertyReassignmentRegex = new Regex("^" + propertyReassignment, RegexOptions.Compiled | RegexOptions.Singleline);

            string taskFoundFromFactory = GetString("TaskFoundFromFactory")
                .Replace(@"""{0}""", @"\""(?<task>.+)\""")
                .Replace(@"""{1}""", @"\""(?<assembly>.+)\""");
            TaskFoundFromFactory = new Regex("^" + taskFoundFromFactory, RegexOptions.Compiled);

            string taskFound = GetString("TaskFound")
               .Replace(@"""{0}""", @"\""(?<task>.+)\""")
               .Replace(@"""{1}""", @"\""(?<assembly>.+)\""");
            TaskFound = new Regex("^" + taskFound, RegexOptions.Compiled);

            string skippedFalseCondition = GetString("ProjectImportSkippedFalseCondition")
               .Replace("{0}", @"(?<ImportedProject>[^\""]+)")
               .Replace("{1}", @"(?<File>[^\""]+)")
               .Replace("({2},{3})", @"\((?<Line>\d+),(?<Column>\d+)\)")
               .Replace("{4}", "(?<Reason>.+)")
               .Replace("{5}", "(?<Evaluated>.+)");
            ProjectImportSkippedFalseCondition = new Regex(@"^" + skippedFalseCondition.Substring(0, skippedFalseCondition.Length - 1), RegexOptions.Compiled);

            PropertyReassignment = new Regex(GetString("PropertyReassignment")
                .Replace("{0}", ".*?")
                .Replace("{1}", ".*?")
                .Replace("{2}", ".*?")
                .Replace("{3}", ".*?")
                .Replace("$", @"\$")
                .Replace("(", @"\(")
                .Replace(")", @"\)"), RegexOptions.Singleline);

            ConflictReferenceSameSDK = new Regex(GetString("GetSDKReferenceFiles.ConflictReferenceSameSDK")
               .Replace("{0}", ".*?")
               .Replace("{1}", ".*?")
               .Replace("{2}", ".*?")
               );

            ConflictRedistDifferentSDK = new Regex(GetString("GetSDKReferenceFiles.ConflictRedistDifferentSDK")
               .Replace("{0}", ".*?")
               .Replace("{1}", ".*?")
               .Replace("{2}", ".*?")
               .Replace("{3}", ".*?")
               .Replace("{4}", ".*?")
               );

            ConflictReferenceDifferentSDK = new Regex(GetString("GetSDKReferenceFiles.ConflictRedistDifferentSDK")
               .Replace("{0}", ".*?")
               .Replace("{1}", ".*?")
               .Replace("{2}", ".*?")
               .Replace("{3}", ".*?")
               );

            TaskParameterMessagePrefix = GetString("TaskParameterPrefix");
            OutputItemsMessagePrefix = GetString("OutputItemParameterMessagePrefix");
            ItemGroupIncludeMessagePrefix = GetString("ItemGroupIncludeLogMessagePrefix");
            ItemGroupRemoveMessagePrefix = GetString("ItemGroupRemoveLogMessage");
            GlobalPropertiesPrefix = GetString("General.GlobalProperties");
            RemovingPropertiesPrefix = GetString("General.UndefineProperties");
        }

        public static Regex RemovingProjectProperties { get; set; }
        public static Regex DuplicateImport { get; set; }
        public static Regex SearchPathsForMSBuildExtensionsPath { get; set; }
        public static Regex OverridingTarget { get; set; }
        public static Regex TryingExtensionsPath { get; set; }
        public static Regex ProjectImported { get; set; }
        public static Regex BuildingWithToolsVersionPrefix { get; set; }
        public static Regex ForSearchPathPrefix { get; set; }
        public static Regex ProjectImportSkippedMissingFile { get; set; }
        public static Regex ProjectImportSkippedInvalidFile { get; set; }
        public static Regex ProjectImportSkippedEmptyFile { get; set; }
        public static Regex ProjectImportSkippedFalseCondition { get; set; }
        public static Regex ProjectImportSkippedNoMatches { get; set; }
        public static Regex PropertyReassignment { get; set; }
        public static Regex PropertyReassignmentRegex { get; set; }
        public static Regex ImportingProjectRegex { get; set; }
        public static Regex UnifiedPrimaryReferencePrefix { get; set; }
        public static Regex PrimaryReferencePrefix { get; set; }
        public static Regex DependencyPrefix { get; set; }
        public static Regex UnifiedDependencyPrefix { get; set; }
        public static Regex AssemblyFoldersExLocation { get; set; }
        public static Regex ConflictReferenceSameSDK { get; set; }
        public static Regex ConflictRedistDifferentSDK { get; set; }
        public static Regex ConflictReferenceDifferentSDK { get; set; }
        public static Regex AdditionalPropertiesPrefix { get; set; }
        public static Regex OverridingGlobalPropertiesPrefix { get; set; }
        public static Regex CopyingFileFrom { get; set; }
        public static Regex CreatingHardLink { get; set; }
        public static Regex DidNotCopy { get; set; }
        public static Regex TargetDoesNotExistBeforeTargetMessage { get; set; }
        public static Regex TargetAlreadyCompleteSuccess { get; set; }
        public static Regex TargetSkippedFalseCondition { get; set; }
        public static Regex TaskSkippedFalseCondition { get; set; }
        public static Regex TargetAlreadyCompleteFailure { get; set; }
        public static Regex TargetSkippedWhenSkipNonexistentTargets { get; set; }
        public static Regex TaskFoundFromFactory { get; set; }
        public static Regex TaskFound { get; set; }

        public static Match UsingTask(string message)
        {
            if (TaskFoundFromFactory.IsMatch(message))
            {
                return TaskFoundFromFactory.Match(message);
            }

            if (TaskFound.IsMatch(message))
            {
                return TaskFound.Match(message);
            }

            return Match.Empty;
        }

        public static bool IsTargetSkipped(string message)
        {
            if (TargetAlreadyCompleteSuccess.IsMatch(message))
            {
                return true;
            }

            if (TargetSkippedFalseCondition.IsMatch(message))
            {
                return true;
            }

            if (TargetAlreadyCompleteFailure.IsMatch(message))
            {
                return true;
            }

            if (TargetSkippedWhenSkipNonexistentTargets.IsMatch(message))
            {
                return true;
            }
            //TargetAlreadyCompleteSuccess $:$ Target "{0}" skipped.Previously built successfully.
            //TargetSkippedFalseCondition $:$ Target "{0}" skipped, due to false condition; ({1}) was evaluated as ({2}).
            //TargetAlreadyCompleteFailure $:$ Target "{0}" skipped.Previously built unsuccessfully.
            //TargetSkippedWhenSkipNonexistentTargets"><value>Target "{0}" skipped. The target does not exist in the project and SkipNonexistentTargets is set to true.</value></data>

            return false;
        }

        public static bool IsTargetDoesNotExistAndWillBeSkipped(string message)
        {
            return TargetDoesNotExistBeforeTargetMessage.IsMatch(message);
        }

        public static Match ProjectWasNotImportedRegex(string message, out string reason)
        {
            reason = "";

            if (ProjectImportSkippedMissingFile.Match(message).Success)
            {
                reason = "the file not existing";
                return ProjectImportSkippedMissingFile.Match(message);
            }
            else if (ProjectImportSkippedInvalidFile.Match(message).Success)
            {
                reason = "the file being invalid";
                return ProjectImportSkippedInvalidFile.Match(message);
            }
            else if (ProjectImportSkippedEmptyFile.Match(message).Success)
            {
                reason = "the file being empty";
                return ProjectImportSkippedEmptyFile.Match(message);
            }
            else if (ProjectImportSkippedNoMatches.Match(message).Success)
            {
                reason = "no matching files";
                return ProjectImportSkippedNoMatches.Match(message);
            }
            else if (ProjectImportSkippedFalseCondition.Match(message).Success)
            {
                reason = "false condition; ";
                Match match = ProjectImportSkippedFalseCondition.Match(message);
                reason += match.Groups["Reason"].Value;
                reason += " was evaluated as " + match.Groups["Evaluated"].Value;
                return match;
            }

            return Match.Empty;

            //ProjectImportSkippedMissingFile $:$ Project "{0}" was not imported by "{1}" at ({2},{3}), due to the file not existing.
            //ProjectImportSkippedInvalidFile $:$ Project "{0}" was not imported by "{1}" at({ 2},{3}), due to the file being invalid.
            //ProjectImportSkippedEmptyFile $:$ Project "{0}" was not imported by "{1}" at ({2},{3}), due to the file being empty.
            //ProjectImportSkippedFalseCondition $:$ Project "{0}" was not imported by "{1}" at({ 2},{3}), due to false condition; ({4}) was evaluated as ({5}).
            //ProjectImportSkippedNoMatches $:$ Project "{0}" was not imported by "{1}" at({ 2},{3}), due to no matching files.
        }

        public static bool IsThereWasAConflictPrefix(string message)
        {
            if (ConflictReferenceSameSDK.IsMatch(message))
            {
                return true;
            }

            if (ConflictRedistDifferentSDK.IsMatch(message))
            {
                return true;
            }

            if (ConflictReferenceDifferentSDK.IsMatch(message))
            {
                return true;
            }

            return false;
            //GetSDKReferenceFiles.ConflictReferenceSameSDK $:$ There was a conflict between two references with the same file name resolved within the "{0}" SDK. Choosing "{1}" over "{2}" because it was resolved first.
            //GetSDKReferenceFiles.ConflictRedistDifferentSDK $:$ There was a conflict between two files from the redist folder files going to the same target path "{0}" between the "{1}" and "{2}" SDKs. Choosing "{3}" over "{4}" because it was resolved first.
            //GetSDKReferenceFiles.ConflictReferenceDifferentSDK $:$ There was a conflict between two references with the same file name between the "{0}" and "{1}" SDKs. Choosing "{2}" over "{3}" because it was resolved first.
        }

        public static String PropertyGroupMessagePrefix { get; set; }
        public static String OutputPropertyMessagePrefix { get; set; }


        public static string UsedAssemblySearchPathsLocations => "Used AssemblySearchPaths locations";
        public static string UnusedAssemblySearchPathsLocations => "Unused AssemblySearchPaths locations";
        public static string UsedLocations => "Used locations";
        public static string UnusedLocations = "Unused locations";

        public static string TaskParameterMessagePrefix { get; set; }
        public static string OutputItemsMessagePrefix { get; set; }
        public static string ItemGroupIncludeMessagePrefix { get; set; }
        public static string ItemGroupRemoveMessagePrefix { get; set; }
        public static string GlobalPropertiesPrefix { get; set; }
        public static string RemovingPropertiesPrefix { get; set; }

        public static string To => "\" to \"";
        public static string ToFile => "\" to file \"";

        public static string TotalAnalyzerExecutionTime => "Total analyzer execution time:";

        /// <summary>
        /// https://github.com/NuGet/Home/issues/10383
        /// </summary>
        public static string RestoreTask_CheckingCompatibilityFor = "Checking compatibility for";
        public static string RestoreTask_CheckingCompatibilityOfPackages = "Checking compatibility of packages";
        public static string RestoreTask_AcquiringLockForTheInstallation = "Acquiring lock for the installation";
        public static string RestoreTask_AcquiredLockForTheInstallation = "Acquired lock for the installation";
        public static string RestoreTask_CompletedInstallationOf = "Completed installation of";
        public static string RestoreTask_ResolvingConflictsFor = "Resolving conflicts for";

        public static string Evaluation => "Evaluation";
        public static string Environment => "Environment";
        public static string Imports => "Imports";
        public static string DetailedSummary => "Detailed summary";
        public static string Parameters => "Parameters";
        public static string Results => "Results";
        public static string SearchPaths => "SearchPaths";
        public static string Assemblies => "Assemblies";
        public static string TargetOutputs => "TargetOutputs";
        public static string AnalyzerReport => "Analyzer Report";
        public static string Properties => "Properties";

        public static string GetPropertyName(string message) => message.Substring(message.IndexOf("$") + 2, message.IndexOf("=") - message.IndexOf("$") - 3);

        public static bool IsEvaluationMessage(string message)
        {
            return SearchPathsForMSBuildExtensionsPath.IsMatch(message)
                || OverridingTarget.IsMatch(message)
                || TryingExtensionsPath.IsMatch(message)
                || ProjectImported.IsMatch(message)
                || DuplicateImport.IsMatch(message)
                || ProjectImportSkippedEmptyFile.IsMatch(message)
                || ProjectImportSkippedFalseCondition.IsMatch(message)
                || ProjectImportSkippedNoMatches.IsMatch(message)
                || ProjectImportSkippedMissingFile.IsMatch(message)
                || ProjectImportSkippedInvalidFile.IsMatch(message);
            //Project "{0}" was not imported by "{1}" at({ 2},{ 3}), due to the file being empty.
            //ProjectImportSkippedFalseCondition $:$ Project "{0}" was not imported by "{1}" at ({2},{3}), due to false condition; ({4}) was evaluated as ({5}).
            //ProjectImportSkippedNoMatches $:$ Project "{0}" was not imported by "{1}" at ({2},{3}), due to no matching files.
            //ProjectImportSkippedMissingFile $:$ Project "{0}" was not imported by "{1}" at ({2},{3}), due to the file not existing.
            //ProjectImportSkippedInvalidFile $:$ Project "{0}" was not imported by "{1}" at ({2},{3}), due to the file being invalid.
        }
    }
}

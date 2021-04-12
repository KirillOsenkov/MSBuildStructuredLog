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
            TargetAlreadyCompleteSuccess = GetString("TargetAlreadyCompleteSuccess");
            TargetAlreadyCompleteSuccessRegex = new Regex(TargetAlreadyCompleteSuccess.Replace("{0}", @".*?"));
            TargetAlreadyCompleteFailure = GetString("TargetAlreadyCompleteFailure");
            TargetAlreadyCompleteFailureRegex = new Regex(TargetAlreadyCompleteFailure.Replace("{0}", @".*?"));
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

            ProjectImported = GetString("ProjectImported");


            string projectImported = "^" + ProjectImported
                .Replace(".", "\\.")
                .Replace("{0}", @"(?<ImportedProject>[^\""]+)")
                .Replace("{1}", @"(?<File>[^\""]+)")
                .Replace("({2},{3})", @"\((?<Line>\d+),(?<Column>\d+)\)") + "$";
            ProjectImportedRegex = new Regex(projectImported, RegexOptions.Compiled);

            TargetSkippedFalseCondition = GetString("TargetSkippedFalseCondition");

            TargetSkippedFalseConditionRegex = new Regex(TargetSkippedFalseCondition
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

            ProjectImportSkippedMissingFile = GetString("ProjectImportSkippedMissingFile");

            string skippedMissingFile = "^" + ProjectImportSkippedMissingFile
                .Replace(".", "\\.")
                .Replace("{0}", @"(?<ImportedProject>[^\""]+)")
                .Replace("{1}", @"(?<File>[^\""]+)")
                .Replace("({2},{3})", @"\((?<Line>\d+),(?<Column>\d+)\)");
            ProjectImportSkippedMissingFileRegex = new Regex(skippedMissingFile, RegexOptions.Compiled);

            ProjectImportSkippedInvalidFile = GetString("ProjectImportSkippedInvalidFile");

            string skippedInvalidFile = "^" + ProjectImportSkippedInvalidFile
                .Replace(".", "\\.")
                .Replace("{0}", @"(?<ImportedProject>[^\""]+)")
                .Replace("{1}", @"(?<File>[^\""]+)")
                .Replace("({2},{3})", @"\((?<Line>\d+),(?<Column>\d+)\)");
            ProjectImportSkippedInvalidFileRegex = new Regex(skippedInvalidFile, RegexOptions.Compiled);

            ProjectImportSkippedEmptyFile = GetString("ProjectImportSkippedEmptyFile");

            string skippedEmptyFile = "^" + ProjectImportSkippedEmptyFile
                .Replace(".", "\\.")
                .Replace("{0}", @"(?<ImportedProject>[^\""]+)")
                .Replace("{1}", @"(?<File>[^\""]+)")
                .Replace("({2},{3})", @"\((?<Line>\d+),(?<Column>\d+)\)");
            ProjectImportSkippedEmptyFileRegex = new Regex(skippedEmptyFile, RegexOptions.Compiled);

            ProjectImportSkippedNoMatches = GetString("ProjectImportSkippedNoMatches");

            string skippedNoMatches = "^" + ProjectImportSkippedNoMatches
                .Replace(".", "\\.")
                .Replace("{0}", @"(?<ImportedProject>[^\""]+)")
                .Replace("{1}", @"(?<File>.*)")
                .Replace("({2},{3})", @"\((?<Line>\d+),(?<Column>\d+)\)");
            ProjectImportSkippedNoMatchesRegex = new Regex(skippedNoMatches, RegexOptions.Compiled);

            PropertyReassignment = GetString("PropertyReassignment");

            // This was unused??
            //string propertyReassignment = PropertyReassignment
            // .Replace(@"$({0})=""{1}"" (", @"\$\(\w+\)=.*? \(")
            // .Replace(@"""{2}"")", @".*?""\)")
            // .Replace("{3}", @"(?<File>.*) \((?<Line>\d+),(\d+)\)$");
            //PropertyReassignmentRegex = new Regex("^" + propertyReassignment, RegexOptions.Compiled | RegexOptions.Singleline);

            PropertyReassignmentRegex = new Regex(PropertyReassignment
                .Replace("{0}", ".*?")
                .Replace("{1}", ".*?")
                .Replace("{2}", ".*?")
                .Replace("{3}", ".*?")
                .Replace("$", @"\$")
                .Replace("(", @"\(")
                .Replace(")", @"\)"), RegexOptions.Compiled | RegexOptions.Singleline);

            string taskFoundFromFactory = GetString("TaskFoundFromFactory")
                .Replace(@"""{0}""", @"\""(?<task>.+)\""")
                .Replace(@"""{1}""", @"\""(?<assembly>.+)\""");
            TaskFoundFromFactory = new Regex("^" + taskFoundFromFactory, RegexOptions.Compiled);

            string taskFound = GetString("TaskFound")
               .Replace(@"""{0}""", @"\""(?<task>.+)\""")
               .Replace(@"""{1}""", @"\""(?<assembly>.+)\""");
            TaskFound = new Regex("^" + taskFound, RegexOptions.Compiled);

            ProjectImportSkippedFalseCondition = GetString("ProjectImportSkippedFalseCondition");

            string skippedFalseCondition = "^" + ProjectImportSkippedFalseCondition
                .Replace(".", "\\.")
                .Replace("{0}", @"(?<ImportedProject>[^\""]+)")
                .Replace("{1}", @"(?<File>[^\""]+)")
                .Replace("({2},{3})", @"\((?<Line>\d+),(?<Column>\d+)\)")
                .Replace("{4}", "(?<Reason>.+)")
                .Replace("{5}", "(?<Evaluated>.+)");
            ProjectImportSkippedFalseConditionRegex = new Regex(skippedFalseCondition, RegexOptions.Compiled);

            CouldNotResolveSdk = GetString("CouldNotResolveSdk");
            CouldNotResolveSdkRegex = new Regex("^" + CouldNotResolveSdk
                .Replace("{0}", @"(?<Sdk>[^\""]+)"), RegexOptions.Compiled);

            ProjectImportSkippedExpressionEvaluatedToEmpty = GetString("ProjectImportSkippedExpressionEvaluatedToEmpty");

            string emptyCondition = "^" + ProjectImportSkippedExpressionEvaluatedToEmpty
                .Replace(".", "\\.")
               .Replace("{0}", @"(?<ImportedProject>[^\""]+)")
               .Replace("{1}", @"(?<File>[^\""]+)")
               .Replace("({2},{3})", @"\((?<Line>\d+),(?<Column>\d+)\)");
            ProjectImportSkippedExpressionEvaluatedToEmptyRegex = new Regex(emptyCondition, RegexOptions.Compiled);

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
            EvaluationStarted = GetString("EvaluationStarted");
            EvaluationFinished = GetString("EvaluationFinished");
        }

        public static Regex RemovingProjectProperties { get; set; }
        public static Regex DuplicateImport { get; set; }
        public static Regex SearchPathsForMSBuildExtensionsPath { get; set; }
        public static Regex OverridingTarget { get; set; }
        public static Regex TryingExtensionsPath { get; set; }
        public static Regex ProjectImportedRegex { get; set; }
        public static Regex BuildingWithToolsVersionPrefix { get; set; }
        public static Regex ForSearchPathPrefix { get; set; }
        public static Regex ProjectImportSkippedMissingFileRegex { get; set; }
        public static Regex ProjectImportSkippedInvalidFileRegex { get; set; }
        public static Regex ProjectImportSkippedEmptyFileRegex { get; set; }
        public static Regex ProjectImportSkippedFalseConditionRegex { get; set; }
        public static Regex ProjectImportSkippedExpressionEvaluatedToEmptyRegex { get; set; }
        public static Regex ProjectImportSkippedNoMatchesRegex { get; set; }
        public static Regex PropertyReassignmentRegex { get; set; }
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
        public static Regex TargetAlreadyCompleteSuccessRegex { get; set; }
        public static Regex TargetAlreadyCompleteFailureRegex { get; set; }
        public static Regex TargetSkippedFalseConditionRegex { get; set; }
        public static Regex TaskSkippedFalseCondition { get; set; }
        public static Regex TargetSkippedWhenSkipNonexistentTargets { get; set; }
        public static Regex TaskFoundFromFactory { get; set; }
        public static Regex TaskFound { get; set; }
        public static Regex CouldNotResolveSdkRegex { get; set; }

        public static string TargetSkippedFalseCondition { get; set; }
        public static string TargetAlreadyCompleteSuccess { get; set; }
        public static string TargetAlreadyCompleteFailure { get; set; }
        public static string ProjectImported { get; set; }
        public static string ProjectImportSkippedFalseCondition { get; set; }
        public static string CouldNotResolveSdk { get; set; }
        public static string ProjectImportSkippedExpressionEvaluatedToEmpty { get; set; }
        public static string PropertyReassignment { get; set; }
        public static string ProjectImportSkippedNoMatches { get; set; }
        public static string ProjectImportSkippedMissingFile { get; set; }
        public static string ProjectImportSkippedInvalidFile { get; set; }
        public static string ProjectImportSkippedEmptyFile { get; set; }

        public static Match UsingTask(string message)
        {
            if (TaskFoundFromFactory.Match(message) is Match foundFromFactory && foundFromFactory.Success)
            {
                return foundFromFactory;
            }

            if (TaskFound.Match(message) is Match found && found.Success)
            {
                return found;
            }

            return Match.Empty;
        }

        public static bool IsTargetSkipped(string message)
        {
            if (TargetAlreadyCompleteSuccessRegex.IsMatch(message))
            {
                return true;
            }

            if (TargetSkippedFalseConditionRegex.IsMatch(message))
            {
                return true;
            }

            if (TargetAlreadyCompleteFailureRegex.IsMatch(message))
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

            if (ProjectImportSkippedFalseConditionRegex.Match(message) is Match falseCondition && falseCondition.Success)
            {
                reason = $"false condition; {falseCondition.Groups["Reason"].Value} was evaluated as {falseCondition.Groups["Evaluated"].Value}.";
                return falseCondition;
            }
            else if (ProjectImportSkippedMissingFileRegex.Match(message) is Match missingFile && missingFile.Success)
            {
                reason = "the file not existing";
                return missingFile;
            }
            else if (ProjectImportSkippedInvalidFileRegex.Match(message) is Match invalidFile && invalidFile.Success)
            {
                reason = "the file being invalid";
                return invalidFile;
            }
            else if (ProjectImportSkippedEmptyFileRegex.Match(message) is Match emptyFile && emptyFile.Success)
            {
                reason = "the file being empty";
                return emptyFile;
            }
            else if (ProjectImportSkippedNoMatchesRegex.Match(message) is Match noMatches && noMatches.Success)
            {
                reason = "no matching files";
                return noMatches;
            }
            else if (ProjectImportSkippedExpressionEvaluatedToEmptyRegex.Match(message) is Match emptyCondition && emptyCondition.Success)
            {
                reason = $"the expression evaluating to an empty string";
                return emptyCondition;
            }
            else if (CouldNotResolveSdkRegex.Match(message) is Match noSdk && noSdk.Success)
            {
                reason = $@"could not resolve SDK {noSdk.Groups["Sdk"].Value}";
                return noSdk;
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

        public static string PropertyGroupMessagePrefix { get; set; }
        public static string OutputPropertyMessagePrefix { get; set; }

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

        public static string EvaluationStarted { get; set; }
        public static string EvaluationFinished { get; set; }

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
        public static string RestoreTask_AllPackagesAndProjectsAreCompatible = "All packages and projects are compatible";
        public static string RestoreTask_Committing = "Committing restore";

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
        public static string PropertyReassignmentFolder => "Property reassignment";
        public static string Global => "Global";
        public static string EntryTargets => "Entry targets";
        public static string TargetFramework => "TargetFramework";
        public static string TargetFrameworks => "TargetFrameworks";
        public static string AdditionalProperties => "Additional properties";
        public static string OutputItems => "OutputItems";
        public static string OutputProperties => "OutputProperties";
        public static string Items = "Items";
        public static string Statistics = "Statistics";
        public static string Folder = "Folder";
        public static string Inputs => "Inputs";
        public static string Outputs => "Outputs";

        public static string GetPropertyName(string message)
        {
            var dollar = message.IndexOf("$");
            return message.Substring(dollar + 2, message.IndexOf("=") - dollar - 3);
        }

        public static bool IsEvaluationMessage(string message)
        {
            return SearchPathsForMSBuildExtensionsPath.IsMatch(message)
                || OverridingTarget.IsMatch(message)
                || TryingExtensionsPath.IsMatch(message)
                || ProjectImportedRegex.IsMatch(message)
                || DuplicateImport.IsMatch(message)
                || ProjectImportSkippedEmptyFileRegex.IsMatch(message)
                || ProjectImportSkippedFalseConditionRegex.IsMatch(message)
                || ProjectImportSkippedExpressionEvaluatedToEmptyRegex.IsMatch(message)
                || CouldNotResolveSdkRegex.IsMatch(message)
                || ProjectImportSkippedNoMatchesRegex.IsMatch(message)
                || ProjectImportSkippedMissingFileRegex.IsMatch(message)
                || ProjectImportSkippedInvalidFileRegex.IsMatch(message);
            //Project "{0}" was not imported by "{1}" at({ 2},{ 3}), due to the file being empty.
            //ProjectImportSkippedFalseCondition $:$ Project "{0}" was not imported by "{1}" at ({2},{3}), due to false condition; ({4}) was evaluated as ({5}).
            //ProjectImportSkippedNoMatches $:$ Project "{0}" was not imported by "{1}" at ({2},{3}), due to no matching files.
            //ProjectImportSkippedMissingFile $:$ Project "{0}" was not imported by "{1}" at ({2},{3}), due to the file not existing.
            //ProjectImportSkippedInvalidFile $:$ Project "{0}" was not imported by "{1}" at ({2},{3}), due to the file being invalid.
        }
    }
}

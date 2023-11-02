using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;

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

        public static string Escape(string text) => Regex.Escape(text);

        private static void InitializeRegex()
        {
            OutputPropertyMessagePrefix = GetString("OutputPropertyLogMessage").Replace("{0}={1}", "");
            BuildingWithToolsVersionPrefix = CreateRegex(GetString("ToolsVersionInEffectForBuild"), 1);
            PropertyGroupMessagePrefix = GetString("PropertyGroupLogMessage").Replace("{0}={1}", "");
            ForSearchPathPrefix = CreateRegex(GetString("ResolveAssemblyReference.SearchPath"), 1);
            UnifiedPrimaryReferencePrefix = CreateRegex(GetString("ResolveAssemblyReference.UnifiedPrimaryReference"), 1);
            PrimaryReferencePrefix = CreateRegex(GetString("ResolveAssemblyReference.PrimaryReference"), 1);
            DependencyPrefix = CreateRegex(GetString("ResolveAssemblyReference.Dependency"), 1);
            UnifiedDependencyPrefix = CreateRegex(GetString("ResolveAssemblyReference.UnifiedDependency"), 1);
            AssemblyFoldersExLocation = CreateRegex(GetString("ResolveAssemblyReference.AssemblyFoldersExSearchLocations"), 1);
            AdditionalPropertiesPrefix = CreateRegex(GetString("General.AdditionalProperties"), 1);
            OverridingGlobalPropertiesPrefix = CreateRegex(GetString("General.OverridingProperties"), 1);
            TargetAlreadyCompleteSuccess = GetString("TargetAlreadyCompleteSuccess");
            TargetAlreadyCompleteSuccessRegex = CreateRegex(TargetAlreadyCompleteSuccess, 1);
            TargetAlreadyCompleteFailure = GetString("TargetAlreadyCompleteFailure");
            TargetAlreadyCompleteFailureRegex = CreateRegex(TargetAlreadyCompleteFailure, 1);
            TargetSkippedWhenSkipNonexistentTargets = CreateRegex(GetString("TargetSkippedWhenSkipNonexistentTargets"), 1);
            SkipTargetBecauseOutputsUpToDateRegex = CreateRegex(GetString("SkipTargetBecauseOutputsUpToDate"), 1);
            RemovingProjectProperties = CreateRegex(GetString("General.ProjectUndefineProperties"), 1);

            DuplicateImport = CreateRegex(GetString("SearchPathsForMSBuildExtensionsPath"), 3);

            SearchPathsForMSBuildExtensionsPath = CreateRegex(GetString("SearchPathsForMSBuildExtensionsPath"), 2);

            OverridingTarget = CreateRegex(GetString("OverridingTarget"), 4);

            TryingExtensionsPath = CreateRegex(GetString("TryingExtensionsPath"), 2);

            ProjectImported = GetString("ProjectImported");

            string projectImported = "^" + ProjectImported
                .Replace(".", "\\.")
                .Replace("{0}", @"(?<ImportedProject>[^\""]+)")
                .Replace("{1}", @"(?<File>[^\""]+)")
                .Replace("({2},{3})", @"\((?<Line>\d+),(?<Column>\d+)\)") + "$";
            ProjectImportedRegex = new Regex(projectImported, RegexOptions.Compiled);

            TargetSkippedFalseCondition = GetString("TargetSkippedFalseCondition");

            TargetSkippedFalseConditionRegex = CreateRegex(TargetSkippedFalseCondition, 3);

            TaskSkippedFalseCondition = GetString("TaskSkippedFalseCondition");

            TaskSkippedFalseConditionRegex = CreateRegex(TaskSkippedFalseCondition, 3);

            TargetDoesNotExistBeforeTargetMessage = CreateRegex(GetString("TargetDoesNotExistBeforeTargetMessage"), 2);

            string copyingFileFromEscaped = Escape(GetString("Copy.FileComment"));
            CopyingFileFromRegex = new Regex(copyingFileFromEscaped
                .Replace(@"\{0}", @"(?<From>[^\""]+)")
                .Replace(@"\{1}", @"(?<To>[^\""]+)"), RegexOptions.Compiled);

            CreatingHardLinkRegex = new Regex(Escape(GetString("Copy.HardLinkComment"))
                .Replace(@"\{0}", @"(?<From>[^\""]+)")
                .Replace(@"\{1}", @"(?<To>[^\""]+)"), RegexOptions.Compiled);

            DidNotCopyRegex = new Regex(Escape(GetString("Copy.DidNotCopyBecauseOfFileMatch"))
               .Replace(@"\{0}", @"(?<From>[^\""]+)")
               .Replace(@"\{1}", @"(?<To>[^\""]+)")
               .Replace(@"\{2}", ".*?")
               .Replace(@"\{3}", ".*?"), RegexOptions.Compiled);

            RobocopyFileCopiedRegex = new Regex(Escape(RobocopyFileCopiedMessage)
                .Replace(@"\{0}", @"(?<From>[^\""]+)")
                .Replace(@"\{1}", @"(?<To>[^\""]+)"), RegexOptions.Compiled );

            RobocopyFileSkippedRegex = new Regex(Escape(RobocopyFileSkippedMessage)
                .Replace(@"\{0}", @"(?<From>[^\""]+)")
                .Replace(@"\{1}", @"(?<To>[^\""]+)"), RegexOptions.Compiled);

            RobocopyFileFailedRegex = new Regex(Escape(RobocopyFileFailedMessage)
                .Replace(@"\{0}", @"(?<From>[^\""]+)")
                .Replace(@"\{1}", @"(?<To>[^\""]+)"), RegexOptions.Compiled);

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

            string propertyReassignment = "^" + PropertyReassignment
                .Replace(@"$({0})=""{1}"" (", @"\$\((?<Name>\w+)\)="".*"" \(")
                .Replace(@"""{2}"")", @""".*""\)")
                .Replace("{3}", @"(?<File>.*) \((?<Line>\d+),(?<Column>\d+)\)$");
            PropertyReassignmentRegex = new Regex(propertyReassignment, RegexOptions.Compiled | RegexOptions.Singleline);

            // MSBuild 17.6 shipped with this hardcoded to English (the first part of the regex), but it was switched to a different
            // localized message in https://github.com/dotnet/msbuild/pull/8665. Support both here.
            string deferredResponseFile = ("^(?:Included response file: {0}|" + GetString("PickedUpSwitchesFromAutoResponse") + ")$")
                .Replace(@"{0}", @"(?<File>((.:)?[^:\n\r]*?))");
            DeferredResponseFileRegex = new Regex(deferredResponseFile, RegexOptions.Compiled | RegexOptions.Singleline);

            MetaprojectGenerated = GetString("MetaprojectGenerated");
            string messageMetaprojectGeneratedString = MetaprojectGenerated.Replace(@"{0}", @"(?<File>((.:)?[^:\n\r]*?))");

            MessageMetaprojectGenerated = new Regex(messageMetaprojectGeneratedString, RegexOptions.Compiled | RegexOptions.Singleline);

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

            ConflictReferenceSameSDK = CreateRegex(GetString("GetSDKReferenceFiles.ConflictReferenceSameSDK"), 3);

            ConflictRedistDifferentSDK = CreateRegex(GetString("GetSDKReferenceFiles.ConflictRedistDifferentSDK"), 5);

            ConflictReferenceDifferentSDK = CreateRegex(GetString("GetSDKReferenceFiles.ConflictRedistDifferentSDK"), 4);

            ConflictFoundRegex = new Regex(Escape(GetString("ResolveAssemblyReference.ConflictFound"))
                .Replace(@"\{0}", ".*?")
                .Replace(@"\{1}", ".*?")
                + ".*", RegexOptions.Compiled);

            var foundConflictsRaw = GetString("ResolveAssemblyReference.FoundConflicts");
            if (foundConflictsRaw.StartsWith("MSB3277: "))
            {
                foundConflictsRaw = foundConflictsRaw.Substring("MSB3277: ".Length);
            }

            var foundConflictsNormalized = foundConflictsRaw.NormalizeLineBreaks();
            var foundConflictsEscaped = Escape(foundConflictsNormalized);
            FoundConflictsRegex = new Regex(foundConflictsEscaped
                .Replace(@"\{0}", @"(?<Assembly>[^\""]*)")
                .Replace(@"\{1}", @"(?<Details>.*)"), RegexOptions.Compiled | RegexOptions.Singleline);

            TaskParameterMessagePrefix = GetString("TaskParameterPrefix");
            OutputItemsMessagePrefix = GetString("OutputItemParameterMessagePrefix");
            ItemGroupIncludeMessagePrefix = GetString("ItemGroupIncludeLogMessagePrefix");
            ItemGroupRemoveMessagePrefix = GetString("ItemGroupRemoveLogMessage");
            GlobalPropertiesPrefix = GetString("General.GlobalProperties");
            RemovingPropertiesPrefix = GetString("General.UndefineProperties");
            EvaluationStarted = GetString("EvaluationStarted");
            EvaluationFinished = GetString("EvaluationFinished");
        }

        public static Regex CreateRegex(string text, int replacePlaceholders = 0, RegexOptions options = RegexOptions.Compiled)
        {
            text = Regex.Escape(text);
            if (replacePlaceholders > 0)
            {
                for (int i = 0; i < replacePlaceholders; i++)
                {
                    text = text.Replace(@$"\{{{i}}}", ".*?");
                }
            }

            var regex = new Regex(text, options);
            return regex;
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
        public static Regex DeferredResponseFileRegex { get; set; }
        public static Regex MessageMetaprojectGenerated { get; set; }
        public static Regex UnifiedPrimaryReferencePrefix { get; set; }
        public static Regex PrimaryReferencePrefix { get; set; }
        public static Regex DependencyPrefix { get; set; }
        public static Regex UnifiedDependencyPrefix { get; set; }
        public static Regex AssemblyFoldersExLocation { get; set; }
        public static Regex ConflictReferenceSameSDK { get; set; }
        public static Regex ConflictRedistDifferentSDK { get; set; }

        /// <summary>
        /// "There was a conflict between \"{0}\" and \"{1}\"."
        /// </summary>
        public static Regex ConflictFoundRegex { get; set; }

        /// <summary>
        /// "MSB3277: Found conflicts between different versions of \"{0}\" that could not be resolved.\r\n{1}"
        /// </summary>
        public static Regex FoundConflictsRegex { get; set; }
        public static Regex ConflictReferenceDifferentSDK { get; set; }
        public static Regex AdditionalPropertiesPrefix { get; set; }
        public static Regex OverridingGlobalPropertiesPrefix { get; set; }
        public static Regex CopyingFileFromRegex { get; set; }
        public static Regex CreatingHardLinkRegex { get; set; }
        public static Regex DidNotCopyRegex { get; set; }
        public static Regex RobocopyFileCopiedRegex { get; set; }
        public static Regex RobocopyFileSkippedRegex { get; set; }
        public static Regex RobocopyFileFailedRegex { get; set; }
        public static Regex TargetDoesNotExistBeforeTargetMessage { get; set; }
        public static Regex TargetAlreadyCompleteSuccessRegex { get; set; }
        public static Regex TargetAlreadyCompleteFailureRegex { get; set; }
        public static Regex TargetSkippedFalseConditionRegex { get; set; }
        public static Regex TaskSkippedFalseConditionRegex { get; set; }
        public static Regex TargetSkippedWhenSkipNonexistentTargets { get; set; }
        public static Regex SkipTargetBecauseOutputsUpToDateRegex { get; set; }
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
        public static string TaskSkippedFalseCondition { get; set; }
        public static string MetaprojectGenerated { get; set; }

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

        public static TargetSkipReason GetTargetSkipReason(string message)
        {
            // these were emitted as simple messages until binlog version 14
            // (see https://github.com/dotnet/msbuild/pull/6402)
            if (TargetAlreadyCompleteSuccessRegex.IsMatch(message))
            {
                return TargetSkipReason.PreviouslyBuiltSuccessfully;
            }

            if (TargetAlreadyCompleteFailureRegex.IsMatch(message))
            {
                return TargetSkipReason.PreviouslyBuiltUnsuccessfully;
            }

            if (SkipTargetBecauseOutputsUpToDateRegex.IsMatch(message))
            {
                return TargetSkipReason.OutputsUpToDate;
            }

            //if (TargetSkippedWhenSkipNonexistentTargets.IsMatch(message))
            //{
            //    return TargetSkipReason.TargetDoesNotExist;
            //}

            // realistically control never reaches here (for binlog versions 4 and newer)
            // these were converted from Message to TargetSkipped in binlog version 4
            if (TargetSkippedFalseConditionRegex.IsMatch(message))
            {
                return TargetSkipReason.ConditionWasFalse;
            }

            //TargetAlreadyCompleteSuccess $:$ Target "{0}" skipped.Previously built successfully.
            //TargetSkippedFalseCondition $:$ Target "{0}" skipped, due to false condition; ({1}) was evaluated as ({2}).
            //TargetAlreadyCompleteFailure $:$ Target "{0}" skipped.Previously built unsuccessfully.
            //TargetSkippedWhenSkipNonexistentTargets"><value>Target "{0}" skipped. The target does not exist in the project and SkipNonexistentTargets is set to true.</value></data>

            return TargetSkipReason.None;
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
            if (ConflictFoundRegex.IsMatch(message))
            {
                return true;
            }

            return false;
        }

        public static Match IsFoundConflicts(string text)
        {
            return FoundConflictsRegex.Match(text);
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
        public static string TotalGeneratorExecutionTime => "Total generator execution time:";

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
        public static string TruncatedEnvironment => "Starting with MSBuild 17.4, only some environment variables are included in the log: the ones read during the build and the ones prefixed with MSBUILD, DOTNET_ or COMPLUS_.\nDefine MSBUILDLOGALLENVIRONMENTVARIABLES to log all environment variables during the build.";
        public static string Imports => "Imports";
        public static string DetailedSummary => "Detailed summary";
        public static string Parameters => "Parameters";
        public static string Results => "Results";
        public static string SearchPaths => "SearchPaths";
        public static string Assemblies => "Assemblies";
        public static string TargetOutputs => "TargetOutputs";
        public static string AnalyzerReport => "Analyzer Report";
        public static string GeneratorReport => "Generator Report";
        public static string Properties => "Properties";
        public static string PropertyReassignmentFolder => "Property reassignment";
        public static string Global => "Global";
        public static string EntryTargets => "Entry targets";
        public static string TargetFramework => "TargetFramework";
        public static string Platform => "Platform";
        public static string Configuration => "Configuration";
        public static string TargetFrameworks => "TargetFrameworks";
        public static string TargetFrameworkVersion => "TargetFrameworkVersion";
        public static string AdditionalProperties => "Additional properties";
        public static string OutputItems => "OutputItems";
        public static string OutputProperties => "OutputProperties";
        public static string Items = "Items";
        public static string Statistics = "Statistics";
        public static string Folder = "Folder";
        public static string Inputs => "Inputs";
        public static string Outputs => "Outputs";
        public static string Assembly => "Assembly";
        public static string CommandLineArguments => "CommandLineArguments";
        public static string Item => "Item";
        public static string Property => "Property";
        public static string Duration => "Duration";
        public static string Note => "Note";
        public static string DoubleWrites => "DoubleWrites";
        public static string MSBuildVersionPrefix => "MSBuild version = ";
        public static string MSBuildExecutablePathPrefix => "MSBuild executable path = ";
        public static string Warnings = "Warnings";
        public static string NodesReusal = "Reusing node";
        public static string NodesManagementNode = "Nodes Management";

        // These aren't localized, see https://github.com/microsoft/MSBuildSdks/blob/543e965191417dee65471ee57a6702289847b49b/src/Artifacts/Tasks/Robocopy.cs#L66-L77
        private const string RobocopyFileCopiedMessage = "Copied {0} to {1}";
        private const string RobocopyFileSkippedMessage = "Skipped copying {0} to {1}";
        private const string RobocopyFileFailedMessage = "Failed to copy {0} to {1}";

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

using StructuredLogger.Properties;
using System;
using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;
using System.Windows;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class Strings
    {

        public static ResourceSet ResourceSet { get; private set; }

        public static void SetCultureInfo(CultureInfo cultureInfo)
        {
            ResourceSet = Resources.ResourceManager.GetResourceSet(cultureInfo, true, true);
            InitializeRegex();
        }

        private static void InitializeRegex()
        {
            OutputPropertyMessagePrefix = ResourceSet.GetString("OutputPropertyLogMessage").Replace("{0}={1}", "");
            BuildingWithToolsVersionPrefix = new Regex(ResourceSet.GetString("ToolsVersionInEffectForBuild").Replace("{0}", ".*?"));
            PropertyGroupMessagePrefix = ResourceSet.GetString("PropertyGroupLogMessage").Replace("{0}={1}", "");
            ForSearchPathPrefix = new Regex(ResourceSet.GetString("ResolveAssemblyReference.SearchPath").Replace("{0}", ".*?"));
            UnifiedPrimaryReferencePrefix = new Regex(ResourceSet.GetString("ResolveAssemblyReference.UnifiedPrimaryReference").Replace("{0}", ".*?"));
            PrimaryReferencePrefix = new Regex(ResourceSet.GetString("ResolveAssemblyReference.PrimaryReference").Replace("{0}", ".*?"));
            DependencyPrefix = new Regex(ResourceSet.GetString("ResolveAssemblyReference.Dependency").Replace("{0}", ".*?"));
            UnifiedDependencyPrefix = new Regex(ResourceSet.GetString("ResolveAssemblyReference.UnifiedDependency").Replace("{0}", ".*?"));
            AssemblyFoldersExLocation = new Regex(ResourceSet.GetString("ResolveAssemblyReference.AssemblyFoldersExSearchLocations").Replace("{0}", ".*?"));
            AdditionalPropertiesPrefix = new Regex(ResourceSet.GetString("General.AdditionalProperties").Replace("{0}", ".*?"));
            OverridingGlobalPropertiesPrefix = new Regex(ResourceSet.GetString("General.OverridingProperties").Replace("{0}", ".*?"));
            TargetAlreadyCompleteSuccess = new Regex(ResourceSet.GetString("TargetAlreadyCompleteSuccess").Replace("{0}", @".*?"));
            TargetAlreadyCompleteFailure = new Regex(ResourceSet.GetString("TargetAlreadyCompleteFailure").Replace("{0}", @".*?"));
            TargetSkippedWhenSkipNonexistentTargets = new Regex(ResourceSet.GetString("TargetSkippedWhenSkipNonexistentTargets").Replace("{0}", @".*?"));
            
            DuplicateImport = new Regex(ResourceSet.GetString("SearchPathsForMSBuildExtensionsPath")
                .Replace("{0}", @".*?")
                .Replace("{1}", @".*?")
                .Replace("{2}", @".*?"));

            SearchPathsForMSBuildExtensionsPath = new Regex(ResourceSet.GetString("SearchPathsForMSBuildExtensionsPath")
                .Replace("{0}", @".*?")
                .Replace("{1}", @".*?"));
            
            OverridingTarget = new Regex(ResourceSet.GetString("OverridingTarget")
                .Replace("{0}", @".*?")
                .Replace("{1}", @".*?")
                .Replace("{2}", @".*?")
                .Replace("{3}", @".*?"));

            TryingExtensionsPath = new Regex(ResourceSet.GetString("TryingExtensionsPath")
                 .Replace("{0}", @".*?")
                 .Replace("{1}", @".*?"));
            
            ProjectImported = new Regex(ResourceSet.GetString("ProjectImported")
                .Replace("{0}", @".*?")
                .Replace("{1}", @".*?")
                .Replace("{2}", @".*?")
                .Replace("{3}", @".*?"));

            TargetSkippedFalseCondition = new Regex(ResourceSet.GetString("TargetSkippedFalseCondition")
                .Replace("{0}", @".*?")
                .Replace("{1}", @".*?")
                .Replace("{2}", @".*?")
                );

            TargetDoesNotExistBeforeTargetMessage = new Regex(ResourceSet.GetString("TargetDoesNotExistBeforeTargetMessage")
                .Replace("{0}", @".*?")
                .Replace("{1}", @".*?")
                );

            CopyingFileFrom = new Regex(ResourceSet.GetString("Copy.FileComment")
                .Replace("{0}", @"(?<From>[^\""]+)")
                .Replace("{1}", @"(?<To>[^\""]+)")
                );

            CreatingHardLink = new Regex(ResourceSet.GetString("Copy.HardLinkComment")
                .Replace("{0}", @"(?<From>[^\""]+)")
                .Replace("{1}", @"(?<To>[^\""]+)")
                );

            DidNotCopy = new Regex(ResourceSet.GetString("Copy.DidNotCopyBecauseOfFileMatch")
               .Replace("{0}", @"(?<From>[^\""]+)")
               .Replace("{1}", @"(?<To>[^\""]+)")
               .Replace("{2}", ".*?")
               .Replace("{3}", ".*?")
               );

            string taskSkipped = ResourceSet.GetString("TaskSkippedFalseCondition")
                .Replace("{0}", ".*?")
                .Replace("{1}", ".*?")
                .Replace("{2}", ".*?");
            IsTaskSkipped = new Regex(taskSkipped);

            string importProject = ResourceSet.GetString("ProjectImported")
                .Replace("{0}", @"(?<ImportedProject>[^\""]+)")
                .Replace("{1}", @"(?<File>[^\""]+)")
                .Replace("({2},{3})", @"\((?<Line>\d+),(?<Column>\d+)\)\.$");
            ImportingProjectRegex = new Regex(@"^" + importProject.Substring(0, importProject.Length - 1), RegexOptions.Compiled);

            string skippedMissingFile = ResourceSet.GetString("ProjectImportSkippedMissingFile")
                .Replace("{0}", @"(?<ImportedProject>[^\""]+)")
                .Replace("{1}", @"(?<File>[^\""]+)")
                .Replace("({2},{3})", @"\((?<Line>\d+),(?<Column>\d+)\)\.");
            ProjectImportSkippedMissingFile = new Regex(@"^" + skippedMissingFile.Substring(0, skippedMissingFile.Length - 1), RegexOptions.Compiled);

            string skippedInvalidFile = ResourceSet.GetString("ProjectImportSkippedInvalidFile")
               .Replace("{0}", @"(?<ImportedProject>[^\""]+)")
               .Replace("{1}", @"(?<File>[^\""]+)")
               .Replace("({2},{3})", @"\((?<Line>\d+),(?<Column>\d+)\)\.");
            ProjectImportSkippedInvalidFile = new Regex(@"^" + skippedMissingFile.Substring(0, skippedMissingFile.Length - 1), RegexOptions.Compiled);

            string skippedEmptyFile = ResourceSet.GetString("ProjectImportSkippedEmptyFile")
             .Replace("{0}", @"(?<ImportedProject>[^\""]+)")
             .Replace("{1}", @"(?<File>[^\""]+)")
             .Replace("({2},{3})", @"\((?<Line>\d+),(?<Column>\d+)\)\.");
            ProjectImportSkippedEmptyFile = new Regex(@"^" + skippedEmptyFile.Substring(0, skippedEmptyFile.Length - 1), RegexOptions.Compiled);

            string skippedNoMatches = ResourceSet.GetString("ProjectImportSkippedNoMatches")
             .Replace("{0}", @"(?<ImportedProject>[^\""]+)")
             .Replace("{1}", @"(?<File>.*)")
             .Replace("({2},{3})", @"\((?<Line>\d+),(?<Column>\d+)\)\.");
            ProjectImportSkippedNoMatches = new Regex(@"^" + skippedNoMatches.Substring(0, skippedNoMatches.Length - 1), RegexOptions.Compiled);

            string propertyReassignment = ResourceSet.GetString("PropertyReassignment")
             .Replace(@"$({0})=""{1}"" (", @"\$\(\w+\)=.*? \(")
             .Replace(@"""{2}"")", @".*?""\)")
             .Replace("{3}", @"(?<File>.*) \((?<Line>\d+),(\d+)\)$");
            PropertyReassignmentRegex = new Regex("^" + propertyReassignment, RegexOptions.Compiled);

            string taskFoundFromFactory = ResourceSet.GetString("TaskFoundFromFactory")
                .Replace(@"""{0}""", @"\""(?<task>.+)\""")
                .Replace(@"""{1}""", @"\""(?<assembly>.+)\""");
            TaskFoundFromFactory = new Regex("^" + taskFoundFromFactory, RegexOptions.Compiled);

            string taskFound = ResourceSet.GetString("TaskFound")
               .Replace(@"""{0}""", @"\""(?<task>.+)\""")
               .Replace(@"""{1}""", @"\""(?<assembly>.+)\""");
            TaskFound = new Regex("^" + taskFound, RegexOptions.Compiled);

            string skippedFalseCondition = ResourceSet.GetString("ProjectImportSkippedFalseCondition")
           .Replace("{0}", @"(?<ImportedProject>[^\""]+)")
           .Replace("{1}", @"(?<File>[^\""]+)")
           .Replace("({2},{3})", @"\((?<Line>\d+),(?<Column>\d+)\)")
           .Replace("{4}", "(?<Reason>.+)")
           .Replace("{5}", ".*?");
            ProjectImportSkippedFalseCondition = new Regex(@"^" + skippedFalseCondition.Substring(0, skippedFalseCondition.Length - 1), RegexOptions.Compiled);

            PropertyReassignment = new Regex(ResourceSet.GetString("PropertyReassignment")
                .Replace("{0}", ".*?")
                .Replace("{1}", ".*?")
                .Replace("{2}", ".*?")
                .Replace("{3}", ".*?")
                .Replace("$", @"\$")
                .Replace("(", @"\(")
                .Replace(")", @"\)"), RegexOptions.Singleline);

            ConflictReferenceSameSDK = new Regex(ResourceSet.GetString("GetSDKReferenceFiles.ConflictReferenceSameSDK")
               .Replace("{0}", ".*?")
               .Replace("{1}", ".*?")
               .Replace("{2}", ".*?")
               );

            ConflictRedistDifferentSDK = new Regex(ResourceSet.GetString("GetSDKReferenceFiles.ConflictRedistDifferentSDK")
               .Replace("{0}", ".*?")
               .Replace("{1}", ".*?")
               .Replace("{2}", ".*?")
               .Replace("{3}", ".*?")
               .Replace("{4}", ".*?")
               );

            ConflictReferenceDifferentSDK = new Regex(ResourceSet.GetString("GetSDKReferenceFiles.ConflictRedistDifferentSDK")
               .Replace("{0}", ".*?")
               .Replace("{1}", ".*?")
               .Replace("{2}", ".*?")
               .Replace("{3}", ".*?")
               );
        }

        public static Regex DuplicateImport { get; set; }
        public static Regex SearchPathsForMSBuildExtensionsPath { get; set; }
        public static Regex  OverridingTarget { get; set; }
        public static Regex TryingExtensionsPath { get; set; }
        public static Regex ProjectImported { get; set; }
        public static Regex BuildingWithToolsVersionPrefix { get; set; }
        public static Regex ForSearchPathPrefix { get; set; }
        public static Regex IsTaskSkipped { get; set; }
        public static Regex ProjectImportSkippedMissingFile { get; set; }
        public static Regex ProjectImportSkippedInvalidFile { get; set; }
        public static Regex ProjectImportSkippedEmptyFile { get; set; }
        public static Regex ProjectImportSkippedFalseCondition { get; set; }
        public static Regex ProjectImportSkippedNoMatches { get; set; }
        public static Regex PropertyReassignment { get; set; }
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
        public static Regex TargetAlreadyCompleteFailure { get; set; }
        public static Regex TargetSkippedWhenSkipNonexistentTargets { get; set; }
        public static Regex PropertyReassignmentRegex { get; set; }
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
            if (ProjectImportSkippedInvalidFile.Match(message).Success)
            {
                reason = "the file being invalid";
                return ProjectImportSkippedInvalidFile.Match(message);
            }

            if (ProjectImportSkippedEmptyFile.Match(message).Success)
            {
                reason = "the file being empty";
                return ProjectImportSkippedEmptyFile.Match(message);
            }
            if (ProjectImportSkippedNoMatches.Match(message).Success)
            {
                reason = "no matching files";
                return ProjectImportSkippedNoMatches.Match(message);
            }
            if (ProjectImportSkippedFalseCondition.Match(message).Success)
            {
                reason = "false condition; ";
                Match match = ProjectImportSkippedFalseCondition.Match(message);
                reason += match.Groups["Reason"].Value;
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

        public static string TaskParameterMessagePrefix => ResourceSet.GetString("TaskParameterPrefix");
        public static string OutputItemsMessagePrefix => ResourceSet.GetString("OutputItemParameterMessagePrefix");
        public static string ItemGroupIncludeMessagePrefix => ResourceSet.GetString("ItemGroupIncludeLogMessagePrefix");
        public static string ItemGroupRemoveMessagePrefix => ResourceSet.GetString("ItemGroupRemoveLogMessage");
        public static string GlobalPropertiesPrefix => ResourceSet.GetString("General.GlobalProperties");
        public static string RemovingPropertiesPrefix => ResourceSet.GetString("General.UndefineProperties");

        public static string To => "\" to \"";
        public static string ToFile => "\" to file \"";

        public static string TotalAnalyzerExecutionTime => "Total analyzer execution time:";

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

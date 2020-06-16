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

        private static ResourceSet resourceSet;

        public static void SetCultureInfo(CultureInfo cultureInfo)
        {
            resourceSet = Resources.ResourceManager.GetResourceSet(cultureInfo, true, true);
            InitializeRegex();

        }

        private static void InitializeRegex()
        {
            OutputPropertyMessagePrefix = resourceSet.GetString("OutputPropertyLogMessage").Replace("{0}={1}", "");
            BuildingWithToolsVersionPrefix = new Regex(resourceSet.GetString("ToolsVersionInEffectForBuild").Replace("{0}", ".*?"));
            PropertyGroupMessagePrefix = resourceSet.GetString("PropertyGroupLogMessage").Replace("{0}={1}", "");
            ForSearchPathPrefix = new Regex(resourceSet.GetString("ResolveAssemblyReference.SearchPath").Replace("{0}", ".*?"));
            UnifiedPrimaryReferencePrefix = new Regex(resourceSet.GetString("ResolveAssemblyReference.UnifiedPrimaryReference").Replace("{0}", ".*?"));
            PrimaryReferencePrefix = new Regex(resourceSet.GetString("ResolveAssemblyReference.PrimaryReference").Replace("{0}", ".*?"));
            DependencyPrefix = new Regex(resourceSet.GetString("ResolveAssemblyReference.Dependency").Replace("{0}", ".*?"));
            UnifiedDependencyPrefix = new Regex(resourceSet.GetString("ResolveAssemblyReference.UnifiedDependency").Replace("{0}", ".*?"));
            AssemblyFoldersExLocation = new Regex(resourceSet.GetString("ResolveAssemblyReference.AssemblyFoldersExSearchLocations").Replace("{0}", ".*?"));
            AdditionalPropertiesPrefix = new Regex(resourceSet.GetString("General.AdditionalProperties").Replace("{0}", ".*?"));
            OverridingGlobalPropertiesPrefix = new Regex(resourceSet.GetString("General.OverridingProperties").Replace("{0}", ".*?"));
            TargetAlreadyCompleteSuccess = new Regex(resourceSet.GetString("TargetAlreadyCompleteSuccess").Replace("{0}", @".*?"));
            TargetAlreadyCompleteFailure = new Regex(resourceSet.GetString("TargetAlreadyCompleteFailure").Replace("{0}", @".*?"));
            TargetSkippedWhenSkipNonexistentTargets = new Regex(resourceSet.GetString("TargetSkippedWhenSkipNonexistentTargets").Replace("{0}", @".*?"));

            TargetSkippedFalseCondition = new Regex(resourceSet.GetString("TargetSkippedFalseCondition")
                .Replace("{0}", @".*?")
                .Replace("{1}", @".*?")
                .Replace("{2}", @".*?")
                );

            TargetDoesNotExistBeforeTargetMessage = new Regex(resourceSet.GetString("TargetDoesNotExistBeforeTargetMessage")
                .Replace("{0}", @".*?")
                .Replace("{1}", @".*?")
                );

            CopyingFileFrom = new Regex(resourceSet.GetString("Copy.FileComment")
                .Replace("{0}", @"(?<From>[^\""]+)")
                .Replace("{1}", @"(?<To>[^\""]+)")
                );

            CreatingHardLink = new Regex(resourceSet.GetString("Copy.HardLinkComment")
                .Replace("{0}", @"(?<From>[^\""]+)")
                .Replace("{1}", @"(?<To>[^\""]+)")
                );

            DidNotCopy = new Regex(resourceSet.GetString("Copy.DidNotCopyBecauseOfFileMatch")
               .Replace("{0}", @"(?<From>[^\""]+)")
               .Replace("{1}", @"(?<To>[^\""]+)")
               .Replace("{2}", ".*?")
               .Replace("{3}", ".*?")
               );

            string taskSkipped = resourceSet.GetString("TaskSkippedFalseCondition")
                .Replace("{0}", ".*?")
                .Replace("{1}", ".*?")
                .Replace("{2}", ".*?");
            IsTaskSkipped = new Regex(taskSkipped);

            string importProject = resourceSet.GetString("ProjectImported")
                .Replace("{0}", @"(?<ImportedProject>[^\""]+)")
                .Replace("{1}", @"(?<File>[^\""]+)")
                .Replace("({2},{3})", @"\((?<Line>\d+),(?<Column>\d+)\)\.$");
            ImportingProjectRegex = new Regex(@"^" + importProject.Substring(0, importProject.Length - 1), RegexOptions.Compiled);

            string skippedMissingFile = resourceSet.GetString("ProjectImportSkippedMissingFile")
                .Replace("{0}", @"(?<ImportedProject>[^\""]+)")
                .Replace("{1}", @"(?<File>[^\""]+)")
                .Replace("({2},{3})", @"\((?<Line>\d+),(?<Column>\d+)\)\.");
            ProjectImportSkippedMissingFile = new Regex(@"^" + skippedMissingFile.Substring(0, skippedMissingFile.Length - 1), RegexOptions.Compiled);

            string skippedInvalidFile = resourceSet.GetString("ProjectImportSkippedInvalidFile")
               .Replace("{0}", @"(?<ImportedProject>[^\""]+)")
               .Replace("{1}", @"(?<File>[^\""]+)")
               .Replace("({2},{3})", @"\((?<Line>\d+),(?<Column>\d+)\)\.");
            ProjectImportSkippedInvalidFile = new Regex(@"^" + skippedMissingFile.Substring(0, skippedMissingFile.Length - 1), RegexOptions.Compiled);

            string skippedEmptyFile = resourceSet.GetString("ProjectImportSkippedEmptyFile")
             .Replace("{0}", @"(?<ImportedProject>[^\""]+)")
             .Replace("{1}", @"(?<File>[^\""]+)")
             .Replace("({2},{3})", @"\((?<Line>\d+),(?<Column>\d+)\)\.");
            ProjectImportSkippedEmptyFile = new Regex(@"^" + skippedEmptyFile.Substring(0, skippedEmptyFile.Length - 1), RegexOptions.Compiled);

            string skippedNoMatches = resourceSet.GetString("ProjectImportSkippedNoMatches")
             .Replace("{0}", @"(?<ImportedProject>[^\""]+)")
             .Replace("{1}", @"(?<File>.*)")
             .Replace("({2},{3})", @"\((?<Line>\d+),(?<Column>\d+)\)\.");
            ProjectImportSkippedNoMatches = new Regex(@"^" + skippedNoMatches.Substring(0, skippedNoMatches.Length - 1), RegexOptions.Compiled);

            string propertyReassignment = resourceSet.GetString("PropertyReassignment")
             .Replace(@"$({0})=""{1}"" (", @"\$\(\w+\)=.*? \(")
             .Replace(@"""{2}"")", @".*?""\)")
             .Replace("{3}", @"(?<File>.*) \((?<Line>\d+),(\d+)\)$");
            PropertyReassignmentRegex = new Regex("^" + propertyReassignment, RegexOptions.Compiled);

            string taskFoundFromFactory = resourceSet.GetString("TaskFoundFromFactory")
                .Replace(@"""{0}""", @"\""(?<task>.+)\""")
                .Replace(@"""{1}""", @"\""(?<assembly>.+)\""");
            TaskFoundFromFactory = new Regex("^" + taskFoundFromFactory, RegexOptions.Compiled);

            string taskFound = resourceSet.GetString("TaskFound")
               .Replace(@"""{0}""", @"\""(?<task>.+)\""")
               .Replace(@"""{1}""", @"\""(?<assembly>.+)\""");
            TaskFound = new Regex("^" + taskFound, RegexOptions.Compiled);

            string skippedFalseCondition = resourceSet.GetString("ProjectImportSkippedFalseCondition")
           .Replace("{0}", @"(?<ImportedProject>[^\""]+)")
           .Replace("{1}", @"(?<File>[^\""]+)")
           .Replace("({2},{3})", @"\((?<Line>\d+),(?<Column>\d+)\)")
           .Replace("{4}", "(?<Reason>.+)")
           .Replace("{5}", ".*?");
            ProjectImportSkippedFalseCondition = new Regex(@"^" + skippedFalseCondition.Substring(0, skippedFalseCondition.Length - 1), RegexOptions.Compiled);

            PropertyReassignment = new Regex(resourceSet.GetString("PropertyReassignment")
                .Replace("{0}", ".*?")
                .Replace("{1}", ".*?")
                .Replace("{2}", ".*?")
                .Replace("{3}", ".*?")
                .Replace("$", @"\$")
                .Replace("(", @"\(")
                .Replace(")", @"\)"));

            ConflictReferenceSameSDK = new Regex(resourceSet.GetString("GetSDKReferenceFiles.ConflictReferenceSameSDK")
               .Replace("{0}", ".*?")
               .Replace("{1}", ".*?")
               .Replace("{2}", ".*?")
               );

            ConflictRedistDifferentSDK = new Regex(resourceSet.GetString("GetSDKReferenceFiles.ConflictRedistDifferentSDK")
               .Replace("{0}", ".*?")
               .Replace("{1}", ".*?")
               .Replace("{2}", ".*?")
               .Replace("{3}", ".*?")
               .Replace("{4}", ".*?")
               );

            ConflictReferenceDifferentSDK = new Regex(resourceSet.GetString("GetSDKReferenceFiles.ConflictRedistDifferentSDK")
               .Replace("{0}", ".*?")
               .Replace("{1}", ".*?")
               .Replace("{2}", ".*?")
               .Replace("{3}", ".*?")
               );
        }

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
        public static Regex UnifiedPrimaryReferencePrefix { get; set; } //=> "Unified primary reference "; //ResolveAssemblyReference.UnifiedPrimaryReference $:$ Unified primary reference "{0}
        public static Regex PrimaryReferencePrefix { get; set; } //=> "Primary reference "; //ResolveAssemblyReference.PrimaryReference - Primärverweis "{0}".
        public static Regex DependencyPrefix { get; set; } //=> "Dependency "; //ResolveAssemblyReference.Dependency $:$ Dependency "{0}".
        public static Regex UnifiedDependencyPrefix { get; set; } //=> "Unified Dependency "; //ResolveAssemblyReference.UnifiedDependency $:$ Unified Dependency "{0}".
        public static Regex AssemblyFoldersExLocation { get; set; } // => "AssemblyFoldersEx location"; //ResolveAssemblyReference.AssemblyFoldersExSearchLocations $:$ AssemblyFoldersEx location: "{0}"
        public static Regex ConflictReferenceSameSDK { get; set; }
        public static Regex ConflictRedistDifferentSDK { get; set; }
        public static Regex ConflictReferenceDifferentSDK { get; set; }
        public static Regex AdditionalPropertiesPrefix { get; set; } // => "Additional Properties"; //General.AdditionalProperties $:$ Additional Properties for project "{0}":
        public static Regex OverridingGlobalPropertiesPrefix { get; set; } //=> "Overriding Global Properties"; //General.OverridingProperties $:$ Overriding Global Properties for project "{0}" with:
        public static Regex CopyingFileFrom { get; set; } // => "Copying file from \""; //Copy.FileComment $:$ Copying file from "{0}" to "{1}".
        public static Regex CreatingHardLink { get; set; } // => "Creating hard link to copy \""; //Copy.HardLinkComment $:$ Creating hard link to copy "{0}" to "{1}".
        public static Regex DidNotCopy { get; set; } //=> "Did not copy from file \""; //Copy.DidNotCopyBecauseOfFileMatch $:$ Did not copy from file "{0}" to file "{1}" because the "{2}" parameter was set to "{3}"
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
        // = new Regex("Using \"(?<task>.+)\" task from (assembly|the task factory) \"(?<assembly>.+)\"\\.", RegexOptions.Compiled);

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

        public static string TaskParameterMessagePrefix => resourceSet.GetString("TaskParameterPrefix");
        public static string OutputItemsMessagePrefix => resourceSet.GetString("OutputItemParameterMessagePrefix");
        public static string ItemGroupIncludeMessagePrefix => resourceSet.GetString("ItemGroupIncludeLogMessagePrefix");
        public static string ItemGroupRemoveMessagePrefix => resourceSet.GetString("ItemGroupRemoveLogMessage");
        public static string GlobalPropertiesPrefix => resourceSet.GetString("General.GlobalProperties");
        public static string RemovingPropertiesPrefix => resourceSet.GetString("General.UndefineProperties");

        //public static string CopyingFileFrom => "Copying file from \""; //Copy.FileComment $:$ Copying file from "{0}" to "{1}".
        //public static string CreatingHardLink => "Creating hard link to copy \""; //Copy.HardLinkComment $:$ Creating hard link to copy "{0}" to "{1}".
        //public static string DidNotCopy => "Did not copy from file \""; //Copy.DidNotCopyBecauseOfFileMatch $:$ Did not copy from file "{0}" to file "{1}" because the "{2}" parameter was set to "{3}" in the project and the files' sizes and timestamps match.
        public static string To => "\" to \"";
        public static string ToFile => "\" to file \"";

        public static string TotalAnalyzerExecutionTime => "Total analyzer execution time:";

        public static string Evaluation => "Evaluation"; //only node name
        public static string Environment => "Environment"; //only node name
        public static string Imports => "Imports"; //only node name??
        public static string DetailedSummary => "Detailed summary";  //only node name
        public static string Parameters => "Parameters"; //only node name??
        public static string Results => "Results"; //only node name?? used as string in MessageProcessor.cs line 258, 333
        public static string SearchPaths => "SearchPaths"; //only node name??
        public static string Assemblies => "Assemblies"; //only node name??
        public static string TargetOutputs => "TargetOutputs"; //only node name??
        public static string AnalyzerReport => "Analyzer Report"; //only node name??
        public static string Properties => "Properties"; //only node name??



        public static string GetPropertyName(string message) => message.Substring(message.IndexOf("$") + 2, message.IndexOf("=") - message.IndexOf("$") - 3);

        public static bool IsEvaluationMessage(string message)
        {
            return message.StartsWith("Search paths being used") //SearchPathsForMSBuildExtensionsPath $:$ Search paths being used for {0} are {1}
                || message.StartsWith("Overriding target") //OverridingTarget $:$ Overriding target "{0}" in project "{1}" with target "{2}" from project "{3}".
                || message.StartsWith("Trying to import") //TryingExtensionsPath $:$ Trying to import {0} using extensions path {1}
                || message.StartsWith("Importing project") //ProjectImported $:$ Importing project "{0}" into project "{1}" at ({2},{3}).
                || message.Contains("cannot be imported again.") //DuplicateImport $:$ MSB4011: "{0}" cannot be imported again. It was already imported at "{1}". This is most likely a build authoring error. This subsequent import will be ignored. {2}
                || (message.StartsWith("Project \"") && message.Contains("was not imported by"));
            //ProjectImportSkippedEmptyFile $:$ Project "{0}" was not imported by "{1}" at({ 2},{ 3}), due to the file being empty.
            //ProjectImportSkippedFalseCondition $:$ Project "{0}" was not imported by "{1}" at ({2},{3}), due to false condition; ({4}) was evaluated as ({5}).
            //ProjectImportSkippedNoMatches $:$ Project "{0}" was not imported by "{1}" at ({2},{3}), due to no matching files.
            //ProjectImportSkippedMissingFile $:$ Project "{0}" was not imported by "{1}" at ({2},{3}), due to the file not existing.
            //ProjectImportSkippedInvalidFile $:$ Project "{0}" was not imported by "{1}" at ({2},{3}), due to the file being invalid.
        }
    }
}

using System;
using System.Text.RegularExpressions;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class Strings
    {
        public static string UsedAssemblySearchPathsLocations => "Used AssemblySearchPaths locations";
        public static string UnusedAssemblySearchPathsLocations => "Unused AssemblySearchPaths locations";
        public static string UsedLocations => "Used locations";
        public static string UnusedLocations = "Unused locations";

        public static string TaskParameterMessagePrefix => @"Task Parameter:";
        public static string OutputItemsMessagePrefix => @"Output Item(s): ";
        public static string OutputPropertyMessagePrefix => @"Output Property: ";
        public static string PropertyGroupMessagePrefix => @"Set Property: ";
        public static string ItemGroupIncludeMessagePrefix => @"Added Item(s): ";
        public static string ItemGroupRemoveMessagePrefix => @"Removed Item(s): ";

        public static string ThereWasAConflictPrefix => "There was a conflict";
        public static string ForSearchPathPrefix => "For SearchPath";
        public static string UnifiedPrimaryReferencePrefix => "Unified primary reference ";
        public static string PrimaryReferencePrefix => "Primary reference ";
        public static string DependencyPrefix => "Dependency ";
        public static string UnifiedDependencyPrefix => "Unified Dependency ";
        public static string AssemblyFoldersExLocation => "AssemblyFoldersEx location";
        public static string GlobalPropertiesPrefix => "Global Properties";
        public static string AdditionalPropertiesPrefix => "Additional Properties";
        public static string OverridingGlobalPropertiesPrefix => "Overriding Global Properties";
        public static string RemovingPropertiesPrefix => "Removing Properties";

        public static string CopyingFileFrom => "Copying file from \"";
        public static string CreatingHardLink => "Creating hard link to copy \"";
        public static string DidNotCopy => "Did not copy from file \"";
        public static string To => "\" to \"";
        public static string ToFile => "\" to file \"";
        public static string BuildingWithToolsVersionPrefix => "Building with tools version";
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

        public static Regex UsingTaskRegex = new Regex("Using \"(?<task>.+)\" task from (assembly|the task factory) \"(?<assembly>.+)\"\\.", RegexOptions.Compiled);

        public static Regex PropertyReassignmentRegex = new Regex(@"^Property reassignment: \$\(\w+\)=.+ \(previous value: .*\) at (?<File>.*) \((?<Line>\d+),(\d+)\)$", RegexOptions.Compiled);
        public static Regex ImportingProjectRegex = new Regex(
            @"^Importing project ""(?<ImportedProject>[^\""]+)"" into project ""(?<File>[^\""]+)"" at \((?<Line>\d+),(?<Column>\d+)\)\.$", RegexOptions.Compiled);
        public static Regex ProjectWasNotImportedRegex = new Regex(@"^Project ""(?<ImportedProject>[^""]+)"" was not imported by ""(?<File>[^""]+)"" at \((?<Line>\d+),(?<Column>\d+)\), due to (?<Reason>.+)$", RegexOptions.Compiled);

        public static string GetPropertyName(string message) => message.Substring(message.IndexOf("$") + 2, message.IndexOf("=") - message.IndexOf("$") - 3);

        public static bool IsTaskSkipped(string message) => message.StartsWith("Task") && message.Contains("skipped");
        public static bool IsTargetSkipped(string message) => message.StartsWith("Target") && message.Contains("skipped");
        public static bool IsTargetDoesNotExistAndWillBeSkipped(string message) => message.StartsWith("The target") && message.Contains("does not exist in the project, and will be ignored");

        public static bool IsEvaluationMessage(string message)
        {
            return message.StartsWith("Search paths being used")
                || message.StartsWith("Overriding target")
                || message.StartsWith("Trying to import")
                || message.StartsWith("Importing project")
                || message.Contains("cannot be imported again.")
                || (message.StartsWith("Project \"") && message.Contains("was not imported by"));
        }

        public static bool IsPropertyReassignmentMessage(string message)
        {
            return message.StartsWith("Property reassignment");                
        }
    }
}

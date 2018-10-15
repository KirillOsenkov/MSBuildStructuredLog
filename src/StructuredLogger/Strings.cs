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

        public static Regex UsingTaskRegex = new Regex("Using \"(?<task>.+)\" task from (assembly|the task factory) \"(?<assembly>.+)\"\\.", RegexOptions.Compiled);

        public static bool IsTaskSkipped(string message) => message.StartsWith("Task") && message.Contains("skipped");
        public static bool IsTargetSkipped(string message) => message.StartsWith("Target") && message.Contains("skipped");
        public static bool IsTargetDoesNotExistAndWillBeSkipped(string message) => message.StartsWith("The target") && message.Contains("does not exist in the project, and will be ignored");

        public static bool IsEvaluationMessage(string message)
        {
            return message.StartsWith("Search paths being used")
                || message.StartsWith("Overriding target")
                || message.StartsWith("Trying to import")
                || message.StartsWith("Property reassignment")
                || message.StartsWith("Importing project")
                || message.Contains("cannot be imported again.")
                || (message.StartsWith("Project \"") && message.Contains("was not imported by"));
        }
    }
}

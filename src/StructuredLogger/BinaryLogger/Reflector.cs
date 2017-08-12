using System;
using System.Reflection;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Logging
{
    /// <summary>
    /// This class accesses the properties on event args that were added in MSBuild 15.3.
    /// As the StructuredLogger.dll references MSBuild 14.0 and gracefully degrades
    /// when used with MSBuild 14.0, we have to use Reflection to dynamically 
    /// retrieve the values if present and gracefully degrade if we're running with
    /// an earlier MSBuild.
    /// </summary>
    internal class Reflector
    {
        private static Func<BuildEventArgs, string> ProjectFileFromEvaluationStarted;
        private static Func<BuildEventArgs, string> ProjectFileFromEvaluationFinished;
        private static Func<BuildEventArgs, string> UnexpandedProjectGetter;
        private static Func<BuildEventArgs, string> ImportedProjectFileGetter;
        private static Func<BuildEventContext, int> EvaluationIdGetter;

        internal static string GetProjectFileFromEvaluationStarted(BuildEventArgs e)
        {
            if (ProjectFileFromEvaluationStarted == null)
            {
                var type = e.GetType().GetTypeInfo();
                var method = type.GetProperty("ProjectFile").GetGetMethod();
                ProjectFileFromEvaluationStarted = b => method.Invoke(b, null) as string;
            }

            return ProjectFileFromEvaluationStarted(e);
        }

        internal static string GetProjectFileFromEvaluationFinished(BuildEventArgs e)
        {
            if (ProjectFileFromEvaluationFinished == null)
            {
                var type = e.GetType().GetTypeInfo();
                var method = type.GetProperty("ProjectFile").GetGetMethod();
                ProjectFileFromEvaluationFinished = b => method.Invoke(b, null) as string;
            }

            return ProjectFileFromEvaluationFinished(e);
        }

        internal static string GetUnexpandedProject(BuildEventArgs e)
        {
            if (UnexpandedProjectGetter == null)
            {
                var type = e.GetType().GetTypeInfo();
                var method = type.GetProperty("UnexpandedProject").GetGetMethod();
                UnexpandedProjectGetter = b => method.Invoke(b, null) as string;
            }

            return UnexpandedProjectGetter(e);
        }

        internal static string GetImportedProjectFile(BuildEventArgs e)
        {
            if (ImportedProjectFileGetter == null)
            {
                var type = e.GetType().GetTypeInfo();
                var method = type.GetProperty("ImportedProjectFile").GetGetMethod();
                ImportedProjectFileGetter = b => method.Invoke(b, null) as string;
            }

            return ImportedProjectFileGetter(e);
        }

        internal static int GetEvaluationId(BuildEventContext buildEventContext)
        {
            if (EvaluationIdGetter == null)
            {
                var type = buildEventContext.GetType();
                var field = type.GetField("_evaluationId", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null)
                {
                    EvaluationIdGetter = b => (int)field.GetValue(b);
                }
                else
                {
                    EvaluationIdGetter = b => int.MinValue;
                }
            }

            return EvaluationIdGetter(buildEventContext);
        }
    }
}

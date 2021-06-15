using System;

namespace TaskRunner
{
    public class MissingPropertyException : MissingMemberException
    {
        public MissingPropertyException(string className, string propertyName) : base(className, propertyName)
        {
        }

        public string MSBuildVersion { get; set; }

        public override string Message
            => $"Property {MemberName} was not found on type {ClassName}. " +
               $"This probably means that the task being run was recorded with a newer version of MSBuild ({MSBuildVersion ?? "N/A"}) " +
               $"than the version used by MSBuild Structured Log Viewer ({Microsoft.Build.Evaluation.ProjectCollection.Version}).";
    }
}
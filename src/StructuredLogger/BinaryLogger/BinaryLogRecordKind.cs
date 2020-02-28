namespace Microsoft.Build.Logging.StructuredLogger
{
    public enum BinaryLogRecordKind
    {
        EndOfFile = 0,
        BuildStarted,
        BuildFinished,
        ProjectStarted,
        ProjectFinished,
        TargetStarted,
        TargetFinished,
        TaskStarted,
        TaskFinished,
        Error,
        Warning,
        Message,
        TaskCommandLine,
        CriticalBuildMessage,
        ProjectEvaluationStarted,
        ProjectEvaluationFinished,
        ProjectImported,
        ProjectImportArchive,
        TargetSkipped,
        PropertyReassignment,
        UninitializedPropertyRead,
        EnvironmentVariableRead,
        PropertyInitialValueSet,
    }
}

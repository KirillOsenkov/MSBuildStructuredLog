namespace Microsoft.Build.Logging
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
        TargetSkipped
    }
}

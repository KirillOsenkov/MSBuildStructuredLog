namespace Microsoft.Build.Logging.Serialization
{
    public enum LogRecordKind
    {
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
        CustomEvent
    }
}

namespace Microsoft.Build.Logging.StructuredLogger
{
    /// <summary>
    /// MSBuild 15 added a field EvalutionId to BuildEventContext. Since we only compile against MSBuild 14
    /// we need to store that extra field
    /// </summary>
    public class BuildEventContextWithEvaluationId : Framework.BuildEventContext
    {
        public BuildEventContextWithEvaluationId(
            int submissionId,
            int nodeId,
            int projectInstanceId,
            int projectContextId,
            int targetId,
            int taskId,
            int evaluationId) : base(submissionId, nodeId, projectInstanceId, projectContextId, targetId, taskId)
        {
            EvaluationId = evaluationId;
        }

        public int EvaluationId { get; private set; }
    }
}

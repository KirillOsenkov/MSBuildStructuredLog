namespace Microsoft.Build.Logging.StructuredLogger
{
    public class CriticalBuildMessage : AbstractDiagnostic
    {
        public override string TypeName => nameof(CriticalBuildMessage);
    }
}

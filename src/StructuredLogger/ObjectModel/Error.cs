namespace Microsoft.Build.Logging.StructuredLogger
{
    public class Error : AbstractDiagnostic
    {
        public override string TypeName => nameof(Error);
    }

    public class BuildError : Error
    {
        public override string TypeName => "Build Error";
    }
}

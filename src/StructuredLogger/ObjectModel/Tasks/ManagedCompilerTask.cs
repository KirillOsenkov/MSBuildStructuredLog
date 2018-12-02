namespace Microsoft.Build.Logging.StructuredLogger
{
    public class ManagedCompilerTask : Task
    {
        private CompilationWrites? compilationWrites;
        public CompilationWrites? CompilationWrites
        {
            get
            {
                if (!HasChildren)
                {
                    return null;
                }

                if (!compilationWrites.HasValue)
                {
                    compilationWrites = Logging.StructuredLogger.CompilationWrites.TryParse(this);
                }

                return compilationWrites.Value;
            }
        }
    }
}

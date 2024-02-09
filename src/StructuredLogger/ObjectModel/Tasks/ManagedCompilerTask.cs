namespace Microsoft.Build.Logging.StructuredLogger
{
    public class ManagedCompilerTask : Task
    {
        //public override string TypeName => nameof(ManagedCompilerTask);

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
                    if (compilationWrites == null)
                    {
                        return null;
                    }
                }

                return compilationWrites.Value;
            }
        }
    }
}
